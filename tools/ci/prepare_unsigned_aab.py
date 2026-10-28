"""Prepare an unsigned AAB, NOT release acceptance. Uses ordinary CLI/debug defaults."""
import argparse
import hashlib
import importlib.util
import json
import os
import re
from pathlib import Path
import shutil
import subprocess
import time
import zipfile
import xml.etree.ElementTree as ET

BUNDLETOOL_SHA256 = 'a099cfa1543f55593bc2ed16a70a7c67fe54b1747bb7301f37fdfd6d91028e29'
BUNDLETOOL_URL = 'https://github.com/google/bundletool/releases/download/1.18.3/bundletool-all-1.18.3.jar'
OUTPUT = Path('bin/Release/net10.0-android36.0/android-arm64')
NAME = 'hu.dorian.predict.aab'

_gate_spec = importlib.util.spec_from_file_location('trusted_preparation_gates', Path(__file__).with_name('preparation_gates.py'))
_gates = importlib.util.module_from_spec(_gate_spec)
_gate_spec.loader.exec_module(_gates)
gate_inputs = _gates.inputs
artifact_gates = _gates.check

def prepare(source, workflow, destination, bundletool, runner=subprocess.run):
    source, workflow, destination, bundletool = [Path(p).resolve() for p in (source, workflow, destination, bundletool)]
    if source == workflow or Path(__file__).resolve().parent != workflow / 'tools/ci':
        raise ValueError('Require distinct trusted workflow checkout owning the running builder')
    if any((source / name).exists() for name in ('bin', 'obj')) or destination.exists():
        raise ValueError('Fresh source bin/obj and absent export directory required')
    spec = importlib.util.spec_from_file_location('trusted_record_source', Path(__file__).with_name('record_source.py'))
    provenance = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(provenance)
    provenance.verify_source(source)
    if hashlib.sha256(bundletool.read_bytes()).hexdigest() != BUNDLETOOL_SHA256:
        raise ValueError('Bundletool 1.18.3 official SHA256 mismatch')
    project = ET.parse(source / 'Predict.csproj')
    version = project.findtext('.//ApplicationVersion')
    display = project.findtext('.//ApplicationDisplayVersion')
    if project.findtext('.//ApplicationId') != 'hu.dorian.predict' or not version or not version.isascii() or not version.isdecimal() or not 0 < int(version) <= 2100000000 or not display:
        raise ValueError('Invalid package identity/version')
    def run(command, cwd=source):
        result = runner(command, cwd=cwd, check=True, capture_output=True, text=True)
        result.check_returncode()
        return result.stdout.strip()
    for checkout in (source, workflow):
        if Path(run(['git', 'rev-parse', '--show-toplevel'], checkout)).resolve() != checkout:
            raise ValueError('Expected checkout root, not nested path/parent repository')
    source_sha = run(['git', 'rev-parse', '--verify', 'HEAD'])
    workflow_sha = run(['git', 'rev-parse', '--verify', 'HEAD'], workflow)
    for sha in (source_sha, workflow_sha):
        if not re.fullmatch(r'[0-9a-f]{40}', sha) or sha == '0'*40:
            raise ValueError('Expected actual nonzero lowercase full checkout SHA')
    if os.environ.get('EXPECTED_WORKFLOW_SHA', workflow_sha) != workflow_sha:
        raise ValueError('Trusted workflow checkout SHA mismatch')
    # Snapshot trusted gate inputs BEFORE executing application build code.
    gate_context = gate_inputs()
    started = time.time_ns()
    run(['dotnet', 'build', 'Predict.csproj', '-c', 'Release', '-f', 'net10.0-android36.0', '-r', 'android-arm64',
         '-p:PredictBuildProfile=Production', '-p:PredictTestAds=false', '-p:AndroidPackageFormats=aab',
         '-p:AndroidPackageFormat=aab', '-p:AndroidKeyStore=false', '-p:PublishTrimmed=true',
         '-p:WarningsAsErrors=XA0141', '--nologo', '-v:minimal'])
    expected = source / OUTPUT / NAME
    signed = expected.with_name('hu.dorian.predict-Signed.aab')
    candidates = {p for p in (source / 'bin').rglob('*') if p.suffix.lower() in ('.aab', '.apk', '.apks')}
    if not expected.is_file() or not candidates <= {expected, signed}:
        raise ValueError('Missing exact expected AAB or ambiguous/mixed package output')
    # Windows filesystem timestamp granularity can precede time_ns by a tick.
    # Absent bin/obj is the primary freshness proof; timestamp is an extra stale guard.
    if expected.is_symlink() or expected.stat().st_size == 0 or expected.stat().st_mtime_ns < started - 1_000_000_000:
        raise ValueError('Empty, linked or stale AAB')
    with zipfile.ZipFile(expected) as archive:
        names = archive.namelist()
        if len(names) != len(set(n.casefold() for n in names)):
            raise ValueError('Duplicate ZIP entries')
        if any(n.upper().startswith('META-INF/') for n in names):
            raise ValueError('Signature/metadata entries forbidden in unsigned AAB')
        required = {'BundleConfig.pb', 'base/manifest/AndroidManifest.xml', 'base/dex/classes.dex'}
        if not required <= set(names) or any(archive.getinfo(n).file_size == 0 for n in required):
            raise ValueError('Missing or empty AAB structure')
        abis = {n.split('/')[2] for n in names if '/lib/' in n and n.endswith('.so')}
        if abis != {'arm64-v8a'}:
            raise ValueError('Expected only ARM64 native libraries')
        if archive.testzip() is not None:
            raise ValueError('ZIP CRC failure')
    run(['java', '-jar', str(bundletool), 'validate', '--bundle='+str(expected)])
    manifest_xml = run(['java', '-jar', str(bundletool), 'dump', 'manifest', '--bundle='+str(expected), '--module=base'])
    manifest = ET.fromstring(manifest_xml)
    android = '{http://schemas.android.com/apk/res/android}'
    sdk = manifest.find('uses-sdk')
    if (manifest.tag != 'manifest' or manifest.get('package') != 'hu.dorian.predict'
        or manifest.get(android+'versionCode') != version or manifest.get(android+'versionName') != display
        or sdk is None or sdk.get(android+'minSdkVersion') != '26' or sdk.get(android+'targetSdkVersion') != '36'):
        raise ValueError('Bundletool manifest identity/version/min/target mismatch')
    signed = expected.with_name('hu.dorian.predict-Signed.aab')
    if signed.exists():
        signed.unlink()
    gates = artifact_gates(expected, gate_context, source_sha)
    if gates.get('status') != 'PASS' or gates.get('artifact_sha256') != hashlib.sha256(expected.read_bytes()).hexdigest():
        raise ValueError('Static gate result binding mismatch')
    destination.mkdir(parents=True)
    shutil.copyfile(expected, destination / NAME)
    if hashlib.sha256((destination / NAME).read_bytes()).hexdigest() != gates['artifact_sha256']:
        shutil.rmtree(destination)
        raise ValueError('Export artifact hash mismatch')
    provenance.record(source_sha, workflow_sha, destination, artifact_kind='unsigned-production-aab')
    digest = hashlib.sha256((destination / NAME).read_bytes()).hexdigest()
    (destination / 'SHA256SUMS').write_text(digest + '  ' + NAME + '\n', encoding='utf-8')
    report = {'source_sha': source_sha, 'workflow_sha': workflow_sha, 'status': 'preparation-only', 'release_accepted': False, 'play_ready': False,
              'verified_identity': {'package': 'hu.dorian.predict', 'version_code': version, 'version_name': display, 'min_sdk': 26, 'target_sdk': 36, 'abis': sorted(abis)},
              'sha256': digest, 'bundletool_version': '1.18.3',
              'builder_signing': 'SDK creates runner-debug signed side output; only unsigned AAB exported',
              'artifact_gates': gates,
              'pending_gates': ['actual-hosted-build-review', '16kb-splits-and-runtime', 'approved-production-signing', 'release-version-policy']}
    (destination / 'preparation-report.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    return report

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'workflow', 'destination', 'bundletool'):
        parser.add_argument('--'+name, type=Path, required=True)
    args = parser.parse_args()
    report = prepare(args.source, args.workflow, args.destination, args.bundletool)
    print(json.dumps(report, indent=2))
    if os.environ.get('GITHUB_OUTPUT'):
        with open(os.environ['GITHUB_OUTPUT'], 'a', encoding='utf-8') as output:
            output.write('source_sha=' + report['source_sha'] + '\n')

if __name__ == '__main__':
    main()
