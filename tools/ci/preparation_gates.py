"""Unsigned preparation static gates. Not split, signing or runtime acceptance."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import shutil
import sys
import zipfile

HERE = Path(__file__).resolve().parent
LZ4_SHA256 = 'c1dc314a38fc53f15ab9fe9a24ced2729ee4cf7dbdd271d2b8782b08f9c86a77'

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def module(name):
    spec = importlib.util.spec_from_file_location('trusted_' + name, HERE/(name+'.py'))
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result

def inputs(dotnet=None, lz4=None):
    executable = Path(dotnet or shutil.which('dotnet') or '').resolve()
    root = executable.parent
    decoder = Path(lz4).resolve() if lz4 else root/'packs/Microsoft.Android.Sdk.Windows/36.1.118/tools/K4os.Compression.LZ4.dll'
    if not executable.is_file() or not (root/'sdk/9.0.319/dotnet.dll').is_file():
        raise ValueError('Explicit dotnet root must contain verified helper SDK 9.0.319')
    observed = sha(decoder) if decoder.is_file() else 'missing'
    if decoder.name != 'K4os.Compression.LZ4.dll' or observed != LZ4_SHA256:
        state = 'missing' if observed == 'missing' else 'mismatch'
        raise ValueError(f'Pinned explicit decoder {state}: dotnetRoot={root}; path={decoder}; '
                         f'observedSHA={observed}; expectedSHA={LZ4_SHA256}')
    paths = [Path(__file__), HERE/'ads_attestation.py', HERE/'verify_android_apk.py',
             HERE/'ads_inspector/Program.cs.txt', HERE/'ads_inspector/Inspector.csproj.txt', executable, decoder]
    return {'dotnet': str(executable), 'lz4': str(decoder), 'tools': {str(p.resolve()): sha(p) for p in paths}}

def check(artifact, context, source_hash=None, attest=None):
    artifact = Path(artifact).resolve()
    tools = context['tools']
    required = [Path(__file__), HERE/'ads_attestation.py', HERE/'verify_android_apk.py',
                HERE/'ads_inspector/Program.cs.txt', HERE/'ads_inspector/Inspector.csproj.txt']
    def stable():
        if any(str(p.resolve()) not in tools for p in required) or any(sha(p) != h for p,h in tools.items()):
            raise ValueError('Trusted tool provenance mismatch')
    stable()
    digest = sha(artifact)
    native = native_gate(artifact)
    ads = (attest or module('ads_attestation').attest)(artifact, 'Production', context['dotnet'], Path(context['lz4']), source_hash)
    expected_tools = {p.name: tools[str(p.resolve())] for p in required if p.name in ('ads_attestation.py','Program.cs.txt','Inspector.csproj.txt')}
    if (ads.get('status') != 'PASS' or ads.get('expected_mode') != 'Production'
        or ads.get('artifact_sha256') != digest or ads.get('source_hash') != source_hash
        or ads.get('tool_provenance') != expected_tools
        or ads.get('lz4_dll_sha256') != LZ4_SHA256 or ads.get('dotnet_sdk') != '9.0.319'
        or any(not re.fullmatch('[0-9a-f]{64}', str(ads.get(k,''))) for k in
               ('container_entry_sha256','stored_assembly_sha256','extracted_pe_sha256','helper_binary_sha256'))):
        raise ValueError('Production ads attestation failed/inconclusive or binding/tool provenance mismatch')
    stable()
    if sha(artifact) != digest:
        raise ValueError('Artifact changed during static gates')
    return {'status':'PASS', 'artifact_sha256':digest, 'tool_sha256':tools, 'ads':ads, 'native_elf_16kb':native,
            'splits_verified':False, 'runtime_verified':False}

def native_gate(artifact):
    checker = module('verify_android_apk')
    entries = []
    with zipfile.ZipFile(artifact) as z:
        infos = z.infolist()
        if len(infos) > 100000 or len({i.filename.casefold() for i in infos}) != len(infos):
            raise ValueError('Duplicate or excessive ZIP entries')
        if sum(i.file_size for i in infos) > 2 * 1024**3 or any(i.flag_bits & 1 for i in infos):
            raise ValueError('Unsafe archive expansion/encryption')
        for info in infos:
            if not info.filename.lower().endswith('.so'):
                continue
            if not re.fullmatch(r'[A-Za-z0-9_]+/lib/arm64-v8a/[^/\\]+\.so', info.filename) or info.file_size > 128 * 1024**2:
                raise ValueError('Unexpected native path/ABI/size')
            data = z.read(info)
            entries.append({'name': info.filename, 'sha256': hashlib.sha256(data).hexdigest(),
                            'load_segments': checker.inspect_elf(data, info.filename)})
        if not entries:
            raise ValueError('Missing native libraries')
        if z.testzip() is not None:
            raise ValueError('ZIP CRC failure')
    return {'status': 'PASS', 'libraries': entries, 'library_count': len(entries),
            'load_segment_count': sum(len(e['load_segments']) for e in entries),
            'scope': 'all AAB native ELF PT_LOAD; NOT APK ZIP alignment or splits'}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('artifact', type=Path)
    parser.add_argument('--dotnet')
    parser.add_argument('--lz4-dll', type=Path)
    parser.add_argument('--source-hash')
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    if args.report.exists() or args.report.resolve() == args.artifact.resolve():
        parser.error('Report must be absent and distinct from protected inputs')
    try:
        context = inputs(args.dotnet, args.lz4_dll)
        if args.report.resolve() in {Path(p).resolve() for p in context['tools']}:
            parser.error('Report aliases protected tool input')
        result = check(args.artifact, context, args.source_hash)
        code = 0
    except Exception as exc:
        result = {'status':'FAIL', 'reason':str(exc), 'release_accepted':False, 'play_ready':False}
        code = 1
    with args.report.open('x',encoding='utf-8') as out:
        out.write(json.dumps(result,indent=2)+'\n')
    print(json.dumps({'status':result['status'], 'report':str(args.report)}))
    return code

if __name__ == '__main__':
    sys.exit(main())
