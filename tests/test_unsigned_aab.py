"""WP3 stdlib fixtures only: never evidence of a real Android build."""
import hashlib
import subprocess
import zipfile
from unittest.mock import patch
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]

def tool(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / 'tools/ci' / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

class WorkflowFixtureTests(unittest.TestCase):
    def test_registered_manual_workflow_adds_isolated_opt_in_without_changing_dev(self):
        text = (ROOT/'.github/workflows/dev-apk.yml').read_text()
        self.assertIn('artifact_kind:', text)
        self.assertIn('default: test-apk', text)
        self.assertIn("if: ${{ inputs.artifact_kind == 'unsigned-production-aab' }}", text)
        self.assertIn("if: ${{ inputs.artifact_kind == 'test-apk' || inputs.artifact_kind == '' }}", text)
        prod = text.split('  prepare-unsigned-aab:',1)[1]
        for value in ('windows-latest','contents: read','persist-credentials: false','prepare_unsigned_aab.py','run_regressions.py','workflow-directory: workflow','hu.dorian.predict.aab','SHA256SUMS','preparation-report.json'):
            self.assertIn(value, prod)
        self.assertLess(prod.index('actions/setup-android'), prod.index('run_regressions.py'))
        for value in ('secrets.', 'environment:', 'actions/cache', '-Signed.aab', 'BuildingInsideVisualStudio'):
            self.assertNotIn(value, prod)

class ProvenanceTests(unittest.TestCase):
    def test_explicit_artifact_enum_keeps_old_dev_default(self):
        record = tool('record_source').record
        with tempfile.TemporaryDirectory() as d:
            p = Path(d)
            self.assertEqual('test-apk', record('a1'*20, 'b2'*20, p)['artifact_kind'])
            prod = record('a1'*20, 'b2'*20, p, artifact_kind='unsigned-production-aab')
            self.assertEqual('Production', prod['build_profile'])
            self.assertFalse(prod['test_ads'])
            self.assertEqual('unsigned', prod['signing'])
            with self.assertRaises(ValueError):
                record('a1'*20, 'b2'*20, p, artifact_kind='release')

class BuilderFixtureTests(unittest.TestCase):
    fixture_version = '1'

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.base = Path(self.tmp.name)
        self.source = self.base / 'source'
        self.source.mkdir()
        for name in ('Predict.csproj', 'global.json'):
            (self.source / name).write_bytes((ROOT / name).read_bytes())
        project_path = self.source / 'Predict.csproj'
        project = ET.parse(project_path)
        project.find('.//ApplicationVersion').text = self.fixture_version
        project.write(project_path, encoding='utf-8')
        self.jar = self.base / 'bundletool.jar'
        self.jar.write_bytes(b'FIXTURE-NOT-A-REAL-JAR')
        self.dest = self.base / 'export'
        self.commands = []

    def runner(self, command, **kwargs):
        self.commands.append((command, kwargs))
        if command[0] == 'git':
            if '--show-toplevel' in command:
                return subprocess.CompletedProcess(command,0,str(kwargs['cwd'])+'\n','')
            return subprocess.CompletedProcess(command, 0, ('a1' if Path(kwargs['cwd']).resolve() == self.source.resolve() else 'b2')*20 + '\n', '')
        if command[0] == 'dotnet':
            p = self.source / 'bin/Release/net10.0-android36.0/android-arm64/hu.dorian.predict.aab'
            p.parent.mkdir(parents=True)
            with zipfile.ZipFile(p, 'w') as z:
                for name in ('BundleConfig.pb','base/manifest/AndroidManifest.xml','base/dex/classes.dex','base/lib/arm64-v8a/libfixture.so'):
                    z.writestr(name, b'FIXTURE')
            p.with_name('hu.dorian.predict-Signed.aab').write_bytes(b'debug side output fixture')
            return subprocess.CompletedProcess(command, 0, '', '')
        manifest = f'<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="hu.dorian.predict" android:versionCode="{self.fixture_version}" android:versionName="1.0"><uses-sdk android:minSdkVersion="26" android:targetSdkVersion="36"/></manifest>'
        return subprocess.CompletedProcess(command, 0, manifest if 'dump' in command else 'FIXTURE validation', '')

    def test_static_gate_rejects_same_application_and_workflow_checkout(self):
        builder = tool('prepare_unsigned_aab')
        with self.assertRaisesRegex(ValueError, 'distinct trusted workflow'):
            builder.prepare(self.source, self.source, self.dest, self.jar, runner=self.runner)
        self.assertFalse(self.dest.exists())

    def test_copy_mutation_cannot_be_handed_off(self):
        import shutil
        original = shutil.copyfile
        def corrupt(source,destination):
            original(source,destination)
            Path(destination).write_bytes(b'changed during copy fixture')
        with patch.object(shutil,'copyfile',side_effect=corrupt):
            with self.assertRaisesRegex(ValueError,'Export artifact hash mismatch'):
                self.prepare()
        self.assertFalse(self.dest.exists())

    def test_static_gate_failure_prevents_export_creation(self):
        builder = tool('prepare_unsigned_aab')
        with patch.object(builder, 'BUNDLETOOL_SHA256', hashlib.sha256(self.jar.read_bytes()).hexdigest()), \
             patch.object(builder, 'gate_inputs', return_value={}), \
             patch.object(builder, 'artifact_gates', side_effect=ValueError('gate blocked')):
            with self.assertRaisesRegex(ValueError, 'gate blocked'):
                builder.prepare(self.source, ROOT, self.dest, self.jar, runner=self.runner)
        self.assertFalse(self.dest.exists())

    def prepare(self, runner=None):
        builder = tool('prepare_unsigned_aab')
        def accepted(path, context, source_sha):
            self.assertFalse(self.dest.exists())
            return {'status': 'PASS', 'artifact_sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'fixture_only': True}
        with patch.object(builder, 'gate_inputs', return_value={}), patch.object(builder, 'artifact_gates', side_effect=accepted), patch.object(builder, 'BUNDLETOOL_SHA256', hashlib.sha256(self.jar.read_bytes()).hexdigest()):
            return builder.prepare(self.source, ROOT, self.dest, self.jar, runner=runner or self.runner)

    def test_rejects_bad_candidate_archives_and_ambiguity_without_export(self):
        import shutil
        import warnings
        for mutation in ('signed','duplicate','empty','missing','crc','mixed','extra-aab','stale','absent'):
            with self.subTest(mutation=mutation):
                shutil.rmtree(self.source/'bin', ignore_errors=True)
                shutil.rmtree(self.dest, ignore_errors=True)
                def bad(command, **kwargs):
                    result = self.runner(command, **kwargs)
                    if command[0] == 'dotnet':
                        p = self.source/'bin/Release/net10.0-android36.0/android-arm64/hu.dorian.predict.aab'
                        if mutation == 'signed':
                            with zipfile.ZipFile(p, 'a') as z: z.writestr('META-INF/CERT.RSA', b'sig')
                        elif mutation == 'duplicate':
                            with warnings.catch_warnings():
                                warnings.simplefilter('ignore')
                                with zipfile.ZipFile(p, 'a') as z: z.writestr('BundleConfig.pb', b'again')
                        elif mutation == 'empty': p.write_bytes(b'')
                        elif mutation == 'missing':
                            with zipfile.ZipFile(p, 'w') as z: z.writestr('BundleConfig.pb', b'only')
                        elif mutation == 'crc': p.write_bytes(p.read_bytes().replace(b'FIXTURE', b'BROKEN!', 1))
                        elif mutation == 'mixed': p.with_suffix('.apk').write_bytes(b'apk')
                        elif mutation == 'extra-aab': p.with_name('other.aab').write_bytes(b'aab')
                        elif mutation == 'stale':
                            import os
                            os.utime(p, (1,1))
                        elif mutation == 'absent': p.unlink()
                    return result
                with self.assertRaises((ValueError, zipfile.BadZipFile)):
                    self.prepare(bad)
                self.assertFalse(self.dest.exists())
                shutil.rmtree(self.source/'bin')

    def test_rejects_non_root_checkout_and_workflow_sha_mismatch(self):
        import os
        def nested(command, **kwargs):
            if '--show-toplevel' in command:
                return subprocess.CompletedProcess(command,0,str(self.base)+'\n','')
            return self.runner(command, **kwargs)
        with self.assertRaises(ValueError): self.prepare(nested)
        with patch.dict(os.environ, EXPECTED_WORKFLOW_SHA='c3'*20):
            with self.assertRaises(ValueError): self.prepare()
        self.assertFalse(any(c[0]=='dotnet' for c,k in self.commands))

    def test_rejects_invalid_resolved_sha_before_build(self):
        def invalid(command, **kwargs):
            if command[0] == 'git': return subprocess.CompletedProcess(command,0,'main\n','')
            return self.runner(command, **kwargs)
        with self.assertRaises(ValueError): self.prepare(invalid)
        self.assertFalse(any(c[0]=='dotnet' for c,k in self.commands))

    def test_cli_emits_actual_bound_sha_to_github_output(self):
        import os
        builder = tool('prepare_unsigned_aab')
        output = self.base/'github-output'
        report = {'source_sha': 'a1'*20}
        argv = ['prepare_unsigned_aab.py','--source',str(self.source),'--workflow',str(ROOT),'--destination',str(self.dest),'--bundletool',str(self.jar)]
        with patch('sys.argv', argv), patch.object(builder,'prepare',return_value=report), patch.dict(os.environ, GITHUB_OUTPUT=str(output)):
            builder.main()
        self.assertEqual('source_sha='+'a1'*20+'\n',output.read_text())

    def test_rejects_wrong_bundletool_before_build(self):
        builder = tool('prepare_unsigned_aab')
        with self.assertRaises(ValueError):
            builder.prepare(self.source, ROOT, self.dest, self.jar, runner=self.runner)
        self.assertEqual([], self.commands)

    def test_rejects_manifest_identity_version_sdk_and_abi_drift(self):
        import shutil
        for value in ('package','version','min','target','abi'):
            with self.subTest(value=value):
                shutil.rmtree(self.source/'bin', ignore_errors=True)
                shutil.rmtree(self.dest, ignore_errors=True)
                def bad(command, **kwargs):
                    result = self.runner(command, **kwargs)
                    if command[0] == 'java' and 'dump' in command:
                        replacements = {'package': ('hu.dorian.predict','hu.other'), 'version': (f'versionCode="{self.fixture_version}"',f'versionCode="{int(self.fixture_version) + 1}"'), 'min': ('minSdkVersion="26"','minSdkVersion="25"'), 'target': ('targetSdkVersion="36"','targetSdkVersion="35"')}
                        if value in replacements:
                            result.stdout = result.stdout.replace(*replacements[value])
                    if command[0] == 'dotnet' and value == 'abi':
                        p = self.source/'bin/Release/net10.0-android36.0/android-arm64/hu.dorian.predict.aab'
                        with zipfile.ZipFile(p, 'a') as z: z.writestr('base/lib/x86/libwrong.so', b'wrong')
                    return result
                with self.assertRaises(ValueError): self.prepare(bad)
                self.assertFalse(self.dest.exists())

    def test_failed_subprocess_stops_export_and_validation(self):
        import sys
        def fail(command, **kwargs):
            if command[0] == 'dotnet':
                return subprocess.run([sys.executable, '-c', 'raise SystemExit(17)'], check=True)
            return self.runner(command, **kwargs)
        with self.assertRaises(subprocess.CalledProcessError) as error: self.prepare(fail)
        self.assertEqual(17, error.exception.returncode)
        self.assertFalse(self.dest.exists())
        self.assertFalse(any(c[0]=='java' for c,k in self.commands))

    def test_rejects_stale_output_before_any_subprocess(self):
        (self.source/'bin').mkdir()
        with self.assertRaises(ValueError):
            self.prepare()
        self.assertEqual([], self.commands)
        self.assertFalse(self.dest.exists())

    def test_source_path_alias_preserves_distinct_checkout_shas(self):
        # Exercise the normalization boundary without symlink privileges or 8.3 names.
        canonical_source = self.source.resolve()
        self.source = self.source / '..' / 'source'
        self.assertNotEqual(self.source, canonical_source)
        self.assertEqual(self.source.resolve(), canonical_source)
        result = self.prepare()
        head_cwds = [Path(k['cwd']) for c, k in self.commands
                     if c == ['git', 'rev-parse', '--verify', 'HEAD']]
        self.assertEqual([canonical_source, ROOT.resolve()], head_cwds)
        expected = {'source_sha': 'a1'*20, 'workflow_sha': 'b2'*20}
        manifest = json.loads((self.dest/'source-manifest.json').read_text())
        for record in (result, manifest):
            with self.subTest(record='report' if record is result else 'manifest'):
                self.assertEqual(expected, {key: record[key] for key in expected})

    def test_fixture_build_exports_exact_unsigned_allowlist_with_honest_report(self):
        result = self.prepare()
        self.assertEqual({'hu.dorian.predict.aab','SHA256SUMS','source-manifest.json','preparation-report.json'}, {p.name for p in self.dest.iterdir()})
        manifest = json.loads((self.dest/'source-manifest.json').read_text())
        self.assertEqual('a1'*20, manifest['source_sha'])
        self.assertEqual('b2'*20, manifest['workflow_sha'])
        self.assertEqual('unsigned-production-aab', manifest['artifact_kind'])
        self.assertEqual('Production', manifest['build_profile'])
        self.assertEqual('Release', manifest['configuration'])
        self.assertEqual({'package': 'hu.dorian.predict', 'version_code': self.fixture_version, 'version_name': '1.0', 'min_sdk': 26, 'target_sdk': 36, 'abis': ['arm64-v8a']}, result['verified_identity'])
        self.assertFalse(result['release_accepted'])
        self.assertEqual('PASS', result['artifact_gates']['status'])
        self.assertIn('16kb-splits-and-runtime', result['pending_gates'])
        self.assertFalse(list(self.source.rglob('*-Signed.aab')))
        command = next(c for c,k in self.commands if c[0]=='dotnet')
        for value in ('-p:PredictBuildProfile=Production','-p:PredictTestAds=false','-p:AndroidPackageFormats=aab','-p:AndroidPackageFormat=aab','-p:AndroidKeyStore=false','-r','android-arm64'):
            self.assertIn(value, command)
        self.assertTrue(any('validate' in c for c,k in self.commands))
        self.assertTrue(any('dump' in c for c,k in self.commands))

class FixtureVersionRegressionTests(unittest.TestCase):
    def check_source_versions(self, method):
        for version in ('1', '2', '17'):
            with self.subTest(source_version=version):
                class ExplicitVersionFixture(BuilderFixtureTests):
                    fixture_version = version

                    def setUp(self):
                        super().setUp()
                        project_path = self.source / 'Predict.csproj'
                        project = ET.parse(project_path)
                        project.find('.//ApplicationVersion').text = self.fixture_version
                        project.write(project_path, encoding='utf-8')

                # Explicit method selection avoids CLI -k filtering nested coverage away.
                result = unittest.TestResult()
                ExplicitVersionFixture(method).run(result)
                self.assertEqual(1, result.testsRun)
                self.assertEqual([], result.skipped)
                self.assertTrue(result.wasSuccessful(), '\n'.join(
                    detail for _, detail in result.failures + result.errors))

    def test_happy_handoff_with_explicit_source_versions(self):
        self.check_source_versions('test_fixture_build_exports_exact_unsigned_allowlist_with_honest_report')

    def test_manifest_drift_rejected_without_output_for_source_versions(self):
        self.check_source_versions('test_rejects_manifest_identity_version_sdk_and_abi_drift')

