"""Unit fixture responses are not actual artifact evidence."""
import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
def load():
    spec = importlib.util.spec_from_file_location('preparation_gates', ROOT/'tools/ci/preparation_gates.py')
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m

# Trusted literal: official Microsoft.Android.Sdk.Windows 36.1.118 package,
# tools/K4os.Compression.LZ4.dll; independently recorded in decoder investigation.
OFFICIAL36_SHA256 = 'c1dc314a38fc53f15ab9fe9a24ced2729ee4cf7dbdd271d2b8782b08f9c86a77'
OLD35_SHA256 = 'bab961bb87d556e004a9f0bbe10a0986e6f8697483fe1884dc86b657b280b79b'

class GateTests(unittest.TestCase):
    def test_sdk36_pin_matches_independent_official_reference(self):
        self.assertEqual(OFFICIAL36_SHA256, load().LZ4_SHA256)

    def decoder_tree(self, tmp):
        root = Path(tmp)
        executable = root/'dotnet.exe'
        executable.write_bytes(b'NONEXECUTABLE existence-only fixture')
        helper = root/'sdk/9.0.319/dotnet.dll'
        helper.parent.mkdir(parents=True)
        helper.write_bytes(b'NONEXECUTABLE existence-only fixture')
        decoder = root/'packs/Microsoft.Android.Sdk.Windows/36.1.118/tools/K4os.Compression.LZ4.dll'
        decoder.parent.mkdir(parents=True)
        return executable, decoder

    def test_decoder_failure_diagnostics_are_exact_and_bounded(self):
        m = load()
        with tempfile.TemporaryDirectory() as tmp:
            executable, decoder = self.decoder_tree(tmp)
            for payload, state in ((None, 'missing'), (b'tampered decoder fixture', 'mismatch')):
                with self.subTest(state=state):
                    if payload is not None:
                        decoder.write_bytes(payload)
                    observed = 'missing' if payload is None else hashlib.sha256(payload).hexdigest()
                    with self.assertRaises(ValueError) as caught:
                        m.inputs(str(executable))
                    self.assertEqual(
                        f'Pinned explicit decoder {state}: dotnetRoot={executable.resolve().parent}; path={decoder.resolve()}; '
                        f'observedSHA={observed}; expectedSHA={OFFICIAL36_SHA256}', str(caught.exception))

    def decoder_hash_seam(self, m, decoder, digest):
        # Offline filesystem/hash seam only, NOT real DLL acceptance/interop.
        # Actual official36/old35 bytes are exercised separately in evidence.
        original = m.sha
        return patch.object(m, 'sha', side_effect=lambda p: digest
                            if Path(p).resolve() == decoder.resolve() else original(p))

    def test_offline36_hash_seam_preserves_exact_path_and_tool_binding(self):
        m = load()
        with tempfile.TemporaryDirectory() as tmp:
            executable, decoder = self.decoder_tree(tmp)
            decoder.write_bytes(b'NONEXECUTABLE decoder seam fixture')
            with self.decoder_hash_seam(m, decoder, OFFICIAL36_SHA256):
                ctx = m.inputs(str(executable))
            self.assertEqual(str(decoder.resolve()), ctx['lz4'])
            self.assertEqual(str(executable.resolve()), ctx['dotnet'])
            self.assertEqual(OFFICIAL36_SHA256, ctx['tools'][str(decoder.resolve())])
            self.assertEqual(m.sha(executable), ctx['tools'][str(executable.resolve())])
            self.assertEqual(7, len(ctx['tools']))

    def test_old35_hash_is_rejected_even_in_exact36_path(self):
        m = load()
        with tempfile.TemporaryDirectory() as tmp:
            executable, decoder = self.decoder_tree(tmp)
            decoder.write_bytes(b'NONEXECUTABLE old35 hash seam fixture')
            with self.decoder_hash_seam(m, decoder, OLD35_SHA256):
                with self.assertRaisesRegex(ValueError, 'mismatch') as caught:
                    m.inputs(str(executable))
            self.assertIn(f'observedSHA={OLD35_SHA256}; expectedSHA={OFFICIAL36_SHA256}', str(caught.exception))

    def test_missing_exact36_path_never_searches_sibling_packs(self):
        m = load()
        with tempfile.TemporaryDirectory() as tmp:
            executable, decoder = self.decoder_tree(tmp)
            sibling = decoder.parents[2]/'35.0.105/tools'/decoder.name
            sibling.parent.mkdir(parents=True)
            sibling.write_bytes(b'NONEXECUTABLE sibling fixture')
            with self.assertRaisesRegex(ValueError, 'missing'):
                m.inputs(str(executable))

    def test_explicit_decoder_preserves_basename_and_helper_version_checks(self):
        m = load()
        with tempfile.TemporaryDirectory() as tmp:
            executable, decoder = self.decoder_tree(tmp)
            wrong_name = decoder.with_name('renamed.dll')
            wrong_name.write_bytes(b'NONEXECUTABLE fixture')
            with self.decoder_hash_seam(m, wrong_name, OFFICIAL36_SHA256):
                with self.assertRaisesRegex(ValueError, 'mismatch'):
                    m.inputs(str(executable), wrong_name)
            (executable.parent/'sdk/9.0.319/dotnet.dll').unlink()
            wrong_sdk = executable.parent/'sdk/9.0.999/dotnet.dll'
            wrong_sdk.parent.mkdir(parents=True)
            wrong_sdk.write_bytes(b'NONEXECUTABLE fixture')
            with self.assertRaisesRegex(ValueError, '9.0.319'):
                m.inputs(str(executable), wrong_name)

    def test_missing_or_tampered_decoder_blocks_cli_before_check_and_export(self):
        import json
        m = load()
        with tempfile.TemporaryDirectory() as tmp:
            executable, decoder = self.decoder_tree(tmp)
            artifact = Path(tmp)/'app.aab'
            artifact.write_bytes(b'protected unchanged fixture')
            for state in ('missing', 'tampered'):
                with self.subTest(state=state):
                    if state == 'tampered':
                        decoder.write_bytes(b'tampered bytes')
                    report = Path(tmp)/(state+'.json')
                    argv = ['preparation_gates', str(artifact), '--dotnet', str(executable), '--report', str(report)]
                    with patch('sys.argv', argv), patch.object(m, 'check') as check:
                        self.assertEqual(1, m.main())
                    check.assert_not_called()
                    result = json.loads(report.read_text(encoding='utf-8'))
                    self.assertEqual('FAIL', result['status'])
                    self.assertFalse(result['release_accepted'])
                    self.assertFalse(result['play_ready'])
                    self.assertNotIn('tool_sha256', result)
                    self.assertEqual(b'protected unchanged fixture', artifact.read_bytes())

    def test_decoder_mutation_after_inputs_is_rejected_before_attestation(self):
        m = load()
        with tempfile.TemporaryDirectory() as tmp:
            executable, decoder = self.decoder_tree(tmp)
            decoder.write_bytes(b'NONEXECUTABLE decoder seam fixture')
            with self.decoder_hash_seam(m, decoder, OFFICIAL36_SHA256):
                ctx = m.inputs(str(executable))
            decoder.write_bytes(b'tampered after binding')
            artifact = Path(tmp)/'app.aab'
            artifact.write_bytes(b'unit fixture')
            with patch.object(m, 'native_gate') as native, patch.object(m, 'module') as module:
                with self.assertRaisesRegex(ValueError, 'provenance'):
                    m.check(artifact, ctx)
            native.assert_not_called()
            module.assert_not_called()

    def test_old35_attestation_digest_is_rejected_against_literal36_binding(self):
        m = load()
        with tempfile.TemporaryDirectory() as tmp:
            artifact = Path(tmp)/'app.aab'
            artifact.write_bytes(b'unit fixture')
            ctx = self.context(m)
            response = self.response(m, artifact, ctx)
            response['lz4_dll_sha256'] = OFFICIAL36_SHA256
            with patch.object(m, 'native_gate', return_value={'status': 'PASS'}):
                self.assertEqual('PASS', m.check(artifact, ctx, attest=lambda *a: response)['status'])
                response['lz4_dll_sha256'] = OLD35_SHA256
                with self.assertRaisesRegex(ValueError, 'binding/tool provenance'):
                    m.check(artifact, ctx, attest=lambda *a: response)

    def test_attestation_fail_inconclusive_hash_and_provenance_mismatch_block(self):
        m = load()
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp)/'app.aab'
            p.write_bytes(b'unit fixture')
            for response in ({'status': 'FAIL'}, {'status': 'INCONCLUSIVE'},
                             {'status': 'PASS', 'artifact_sha256': '0'*64},
                             {'status': 'PASS', 'artifact_sha256': hashlib.sha256(p.read_bytes()).hexdigest()}):
                with self.subTest(response=response), patch.object(m, 'native_gate', return_value={'status':'PASS'}):
                    with self.assertRaises(ValueError):
                        m.check(p, {'tools': {}}, attest=lambda *a: response)

    def test_real_native_checker_rejects_malformed_and_accepts_16k(self):
        import struct
        import zipfile
        m = load()
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp)/'app.aab'
            data = bytearray(120)
            data[:7] = b'\x7fELF\x02\x01\x01'
            struct.pack_into('<HHIQQQIHHHHHH', data, 16, 3,183,1,0,64,0,0,64,56,1,0,0,0)
            struct.pack_into('<IIQQQQQQ', data, 64,1,5,0,0,0,120,120,16384)
            for payload, passes in ((b'broken',False),(bytes(data),True)):
                with zipfile.ZipFile(p,'w') as z: z.writestr('base/lib/arm64-v8a/libfixture.so',payload)
                if passes:
                    self.assertEqual(1,m.native_gate(p)['load_segment_count'])
                else:
                    with self.assertRaises(ValueError): m.native_gate(p)

    def context(self, m):
        paths = [Path(m.__file__), m.HERE/'ads_attestation.py', m.HERE/'verify_android_apk.py', m.HERE/'ads_inspector/Program.cs.txt', m.HERE/'ads_inspector/Inspector.csproj.txt']
        return {'tools':{str(p.resolve()):m.sha(p) for p in paths}, 'dotnet':'fixture-dotnet', 'lz4':'fixture-lz4'}

    def response(self,m,p,ctx):
        return dict(status='PASS',expected_mode='Production',source_hash=None,artifact_sha256=m.sha(p),
                    tool_provenance={Path(k).name:v for k,v in ctx['tools'].items() if Path(k).name in ('ads_attestation.py','Program.cs.txt','Inspector.csproj.txt')},
                    lz4_dll_sha256=m.LZ4_SHA256,dotnet_sdk='9.0.319',
                    container_entry_sha256='1'*64,stored_assembly_sha256='2'*64,extracted_pe_sha256='3'*64,helper_binary_sha256='4'*64)

    def test_positive_bound_result_and_each_ads_failure(self):
        m=load()
        with tempfile.TemporaryDirectory() as tmp:
            p=Path(tmp)/'app.aab';p.write_bytes(b'fixture')
            ctx=self.context(m)
            good=self.response(m,p,ctx)
            with patch.object(m,'native_gate',return_value={'status':'PASS'}):
                self.assertFalse(m.check(p,ctx,attest=lambda *a:good)['splits_verified'])
                for key,value in [('status','FAIL'),('status','INCONCLUSIVE'),('artifact_sha256','0'*64),('expected_mode','Dev'),('tool_provenance',{}),('dotnet_sdk','9.0.999'),('lz4_dll_sha256','0'*64)]:
                    bad=dict(good);bad[key]=value
                    with self.subTest(key=key,value=value), self.assertRaises(ValueError):m.check(p,ctx,attest=lambda *a:bad)
                ctx['tools'][str(Path(m.__file__).resolve())]='0'*64
                with self.assertRaisesRegex(ValueError,'provenance'):m.check(p,ctx,attest=lambda *a:good)

    def test_cli_rejects_report_alias_before_inspection(self):
        import subprocess,sys
        with tempfile.TemporaryDirectory() as tmp:
            p=Path(tmp)/'app.aab';p.write_bytes(b'unchanged')
            result=subprocess.run([sys.executable,str(ROOT/'tools/ci/preparation_gates.py'),str(p),'--report',str(p)],capture_output=True,text=True)
            self.assertNotEqual(0,result.returncode)
            self.assertEqual(b'unchanged',p.read_bytes())

    def test_absent_native_inventory_is_rejected(self):
        import zipfile
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp)/'app.aab'
            with zipfile.ZipFile(p, 'w') as z:
                z.writestr('BundleConfig.pb', b'fixture')
            with self.assertRaisesRegex(ValueError, 'native'):
                load().native_gate(p)
