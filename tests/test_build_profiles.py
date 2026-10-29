"""Execute production-linked targets with real MSBuild, not an Android build.
PREDICT_MSBUILD_DLL and PREDICT_DOTNET_HOST can select an installed host.
No SDK resolution, restore, signing or Android tooling is involved.
"""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]

class BuildProfiles(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        default = Path(os.environ.get('LOCALAPPDATA', '')) / 'Microsoft/dotnet'
        cls.host = os.environ.get('PREDICT_DOTNET_HOST') or shutil.which('dotnet') or str(default / 'dotnet.exe')
        explicit = os.environ.get('PREDICT_MSBUILD_DLL')
        candidates = sorted((default / 'sdk').glob('*/MSBuild.dll'))
        if explicit:
            cls.msbuild = explicit
        elif candidates:
            cls.msbuild = str(candidates[-1])
        else:
            probe = subprocess.run([cls.host, '--list-sdks'], capture_output=True, text=True, timeout=30, cwd=tempfile.gettempdir())
            rows = probe.stdout.strip().splitlines()
            if not rows:
                raise RuntimeError('MSBuild host unavailable: ' + probe.stdout + probe.stderr)
            version, location = rows[-1].split(' ', 1)
            cls.msbuild = str(Path(location.strip('[]')) / version / 'MSBuild.dll')

    def run_case(self, overrides=None, code=None, symbols='', cli=None, target='Build', outer=False, mutation=None):
        props = dict(PredictBuildProfile='Production', PredictTestAds='false', Configuration='Release',
                     TargetFramework='net10.0-android36.0', TargetPlatformIdentifier='android',
                     TargetPlatformVersion='36.0', SupportedOSPlatformVersion='26.0',
                     RuntimeIdentifier='android-arm64', ApplicationId='hu.dorian.predict',
                     AndroidPackageFormats='aab', AndroidPackageFormat='aab', AndroidKeyStore='false')
        props.update(overrides or {})
        if outer:
            props['IsCrossTargetingBuild'] = 'true'
            props['TargetFramework'] = ''
        with tempfile.TemporaryDirectory(prefix='predict-profile-') as directory:
            path = Path(directory)
            artifact = path / 'artifact.marker'
            properties = ''.join(f'<{k}>{escape(v)}</{k}>' for k,v in props.items() if v is not None)
            (path / 'late.targets').write_text('<Project><PropertyGroup><DefineConstants>'+escape(symbols)+'</DefineConstants></PropertyGroup></Project>')
            guard = escape(str(ROOT / 'build/Predict.BuildProfiles.targets'))
            changed = ''.join(f'<{k}>{escape(v)}</{k}>' for k,v in (mutation or {}).items())
            # Exists permits the initial RED oracle: missing implementation must produce an artifact.
            (path / 'fixture.proj').write_text(f'''<Project>
<PropertyGroup>{properties}</PropertyGroup>
<Import Project="{guard}" Condition="Exists('{guard}')" />
<Import Project="late.targets" />
<Target Name="CoreCompile"><WriteLinesToFile File="{artifact}" Lines="compiled" /></Target>
<Target Name="_BuildApkEmbed"><WriteLinesToFile File="{artifact}" Lines="packaged" /></Target>
<Target Name="_CopyPackage"><WriteLinesToFile File="{artifact}" Lines="copied" /></Target>
<Target Name="Build" DependsOnTargets="CoreCompile;_BuildApkEmbed;_CopyPackage" />
<Target Name="Rebuild" DependsOnTargets="Build" />
<Target Name="Publish" DependsOnTargets="Build" />
<Target Name="SignAndroidPackage" DependsOnTargets="_BuildApkEmbed;_CopyPackage" />
<Target Name="FixtureMutate"><PropertyGroup>{changed}</PropertyGroup></Target>
<Target Name="FixtureRecheck" DependsOnTargets="PredictValidateCompileProfile;FixtureMutate;{target}" />
</Project>''')
            command = [self.host, self.msbuild, str(path / 'fixture.proj'), '-nologo', '-v:minimal', '-t:'+('FixtureRecheck' if mutation else target)]
            command += ['-p:'+k+'='+v.replace(';','%3B') for k,v in (cli or {}).items()]
            result = subprocess.run(command, cwd=directory, capture_output=True, text=True, timeout=30)
            output = result.stdout + result.stderr
            if code:
                self.assertNotEqual(0, result.returncode, output)
                self.assertIn('error '+code+':', output)
                self.assertFalse(artifact.exists(), output)
            else:
                self.assertEqual(0, result.returncode, output)
                self.assertTrue(artifact.exists(), output)

    def test_missing_profile_fails_closed(self):
        self.run_case({'PredictBuildProfile': None}, 'PREDICT001')

    def test_explicit_ads_contract(self):
        for ads in (None, '', 'flase', 'False', 'TRUE', 'true'):
            with self.subTest(ads=ads):
                self.run_case({'PredictTestAds': ads}, 'PREDICT002')
        self.run_case({'PredictBuildProfile':'Dev', 'PredictTestAds':'false'}, 'PREDICT002')
        self.run_case({'PredictBuildProfile':'Dev', 'PredictTestAds':'true'}, symbols='PREDICT_TEST_ADS')
        self.run_case()

    def test_production_configuration_and_exact_symbols(self):
        for config in ('Debug', '', 'release'):
            self.run_case({'Configuration':config}, 'PREDICT003')
        for symbol in ('DEBUG', 'TRACE;DEBUG', 'TRACE;PREDICT_TEST_ADS;OTHER', ' DEBUG ; TRACE '):
            with self.subTest(symbol=symbol):
                self.run_case(symbols=symbol, code='PREDICT004')
                self.run_case(cli={'DefineConstants':symbol}, code='PREDICT004')
        self.run_case(symbols='NOT_DEBUG;PREDICT_TEST_ADS_EXTRA;TRACE')
        self.run_case({'PredictBuildProfile':'Dev','PredictTestAds':'true'}, symbols='TRACE', code='PREDICT005')
        self.run_case({'PredictBuildProfile':'Dev','PredictTestAds':'true','Configuration':'Debug'}, symbols='DEBUG')

    def test_comma_delimited_compiler_defines_fail_closed(self):
        for symbols in ('TRACE,DEBUG', 'PREDICT_TEST_ADS,TRACE', 'TRACE, DEBUG ;OTHER'):
            with self.subTest(symbols=symbols):
                self.run_case(symbols=symbols, code='PREDICT004')

    def test_production_packaging_identity(self):
        for key, values in {
            'AndroidPackageFormats': ('apk','apk;aab','aab;apk','',None),
            'AndroidPackageFormat': ('apk','apk;aab',''),
            'TargetFramework': ('net9.0-android','net10.0-android35.0',''),
            'TargetPlatformIdentifier': ('windows',''),
            'TargetPlatformVersion': ('35.0',''),
            'SupportedOSPlatformVersion': ('25.0','27.0',''),
            'RuntimeIdentifier': ('android-x64','android-arm',''),
            'RuntimeIdentifiers': ('android-arm64;android-x64',),
            'AndroidSupportedAbis': ('arm64-v8a;x86_64',),
            'ApplicationId': ('com.example.app',''),
        }.items():
            for value in values:
                with self.subTest(key=key,value=value):
                    self.run_case({key:value}, 'PREDICT006')
        self.run_case({'RuntimeIdentifiers':'android-arm64','AndroidSupportedAbis':'arm64-v8a'})

    def test_application_id_case_is_exact_at_every_boundary(self):
        for target in ('CoreCompile', '_BuildApkEmbed', '_CopyPackage'):
            for application_id in ('HU.DORIAN.PREDICT', 'hu.Dorian.predict'):
                with self.subTest(target=target, application_id=application_id):
                    self.run_case({'ApplicationId': application_id}, 'PREDICT006', target=target)
            with self.subTest(target=target, application_id='hu.dorian.predict'):
                self.run_case({'ApplicationId': 'hu.dorian.predict'}, target=target)

    def test_profile_grammar_and_package_entrypoints(self):
        for profile in ('', 'dev', 'production', 'Prodution', 'Unknown'):
            self.run_case({'PredictBuildProfile': profile}, 'PREDICT001')
        for target in ('CoreCompile', '_BuildApkEmbed', '_CopyPackage'):
            self.run_case({'PredictTestAds': 'true'}, 'PREDICT002', target=target)
            self.run_case(target=target)
        self.run_case({'PredictBuildProfile': None}, outer=True)
        self.run_case({'_AndroidAdditionalPackageFormats': 'apk'}, 'PREDICT006')

    def test_inner_build_and_modeled_public_entrypoints(self):
        inner = {'IsCrossTargetingBuild':'false', 'InnerBuildProperty':'TargetFramework',
                 'TargetFrameworks':'net10.0-android36.0'}
        self.run_case(inner)
        self.run_case(dict(inner, PredictBuildProfile=''), 'PREDICT001')
        for target in ('Rebuild', 'Publish', 'SignAndroidPackage'):
            with self.subTest(target=target):
                self.run_case(target=target)
                self.run_case({'PredictTestAds':'true'}, 'PREDICT002', target=target)
                self.run_case(symbols='PREDICT_TEST_ADS', code='PREDICT004', target=target)

    def test_packaging_rechecks_after_prior_validation(self):
        for target in ('_BuildApkEmbed', '_CopyPackage'):
            with self.subTest(target=target):
                self.run_case(target=target, mutation={'DefineConstants':'TRACE;DEBUG'}, code='PREDICT004')
                self.run_case(target=target, mutation={'AndroidPackageFormat':'apk'}, code='PREDICT006')
                self.run_case(target=target, mutation={'PredictTestAds':'true'}, code='PREDICT002')
        self.run_case({'PredictBuildProfile':'Dev','PredictTestAds':'true'}, symbols='NOT_DEBUG', code='PREDICT005')
        self.run_case({'PredictBuildProfile':'Dev','PredictTestAds':'true'}, symbols='TRACE,PREDICT_TEST_ADS')

    def test_app_import_and_dev_caller_intent(self):
        import xml.etree.ElementTree as ET
        project = ET.parse(ROOT / 'Predict.csproj')
        self.assertIn('build/Predict.BuildProfiles.targets', [n.get('Project') for n in project.findall('Import')])
        self.assertFalse((ROOT / 'Directory.Build.targets').exists())
        action = (ROOT / '.github/actions/build-test-apk/action.yml').read_text()
        self.assertIn('-p:PredictBuildProfile=Dev', action)
        import importlib.util
        spec = importlib.util.spec_from_file_location('record', ROOT / 'tools/ci/record_source.py')
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        with tempfile.TemporaryDirectory() as folder:
            self.assertEqual('Dev', module.record('ab'*20, 'cd'*20, Path(folder))['build_profile'])

if __name__ == '__main__':
    unittest.main()
