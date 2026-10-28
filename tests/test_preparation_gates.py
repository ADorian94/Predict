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

class GateTests(unittest.TestCase):
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
