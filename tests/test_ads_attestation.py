import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location('ads_attestation', ROOT / 'tools/ci/ads_attestation.py')

class AttestationTests(unittest.TestCase):
    def test_real_store_elf_and_predict(self):
        self.assertTrue(SPEC.origin and Path(SPEC.origin).exists(), 'actual-artifact verifier is missing')
        mod = importlib.util.module_from_spec(SPEC)
        SPEC.loader.exec_module(mod)
        import zipfile
        apk = Path('C:/Users/corta/Shared/Predict-api36-testads-20261027.apk')
        if not apk.exists(): self.skipTest('local reference APK absent')
        with zipfile.ZipFile(apk) as z:
            data = z.read('lib/arm64-v8a/libassembly-store.so')
        stored = mod.extract_predict(data)
        self.assertEqual(stored[:4], b'XALZ')

class RealExecutionTests(unittest.TestCase):
    def test_actual_apk_dev_and_production(self):
        mod = importlib.util.module_from_spec(SPEC)
        SPEC.loader.exec_module(mod)
        apk = Path('C:/Users/corta/Shared/Predict-api36-testads-20261027.apk')
        dotnet = Path.home() / 'AppData/Local/Microsoft/dotnet/dotnet.exe'
        lz4 = dotnet.parent / 'packs/Microsoft.Android.Sdk.Windows/35.0.105/tools/K4os.Compression.LZ4.dll'
        if not all(p.exists() for p in (apk, dotnet, lz4)): self.skipTest('reference environment absent')
        for mode, expected in [('Dev', 'PASS'), ('Production', 'FAIL')]:
            result = mod.attest(apk, mode, str(dotnet), lz4)
            self.assertEqual(result['status'], expected, result)
            self.assertEqual(result['extracted_pe_sha256'], 'eeffd2bd7768180f1009a279ccf5dd66d0dff10108f7c4ef9532ae31b34825b7')
            (ROOT.parent / ('ads-case-real-' + mode + '.json')).write_text(__import__('json').dumps(result, indent=2))

class BoundsAndControlTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.mod = importlib.util.module_from_spec(SPEC)
        SPEC.loader.exec_module(cls.mod)
        import zipfile
        cls.apk = Path('C:/Users/corta/Shared/Predict-api36-testads-20261027.apk')
        if not cls.apk.exists(): raise unittest.SkipTest('reference APK absent')
        with zipfile.ZipFile(cls.apk) as z: cls.store = z.read('lib/arm64-v8a/libassembly-store.so')

    def test_malformed_elf_and_store_matrix(self):
        import struct
        shoff = struct.unpack_from('<Q', self.store, 40)[0]
        es, count, si = struct.unpack_from('<HHH', self.store, 58)
        sections = [struct.unpack_from('<IIQQQQIIQQ', self.store, shoff+i*es) for i in range(count)]
        st = sections[si]; names = self.store[st[4]:st[4]+st[5]]
        pi = next(i for i,s in enumerate(sections) if names[s[0]:].split(b'\0')[0] == b'payload')
        off = sections[pi][4]
        mutations = [(40, '<Q', len(self.store)+1), (60, '<H', 0), (shoff+pi*64+32, '<Q', len(self.store)), (off+4, '<I', 0x80010004), (off+8, '<I', 0xffffffff), (off+20+8, '<I', 999999)]
        header = struct.unpack_from('<5I', self.store, off)
        base = off+20+header[4]
        mutations += [(base+4, '<I', 1), (base+8, '<I', 0xffffffff)]
        for at, fmt, value in mutations:
            with self.subTest(at=at):
                b = bytearray(self.store); struct.pack_into(fmt,b,at,value)
                with self.assertRaises(self.mod.Unsupported): self.mod.extract_predict(b)
        for size in [0, 63, 100, len(self.store)-1]:
            with self.assertRaises(self.mod.Unsupported): self.mod.extract_predict(self.store[:size])

    def test_zip_inventory_duplicate_crc_and_fixture_aab(self):
        import tempfile, zipfile, warnings
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp)/'fixture.apk'
            for extra in ['lib/x86/libassembly-store.so','feature/lib/arm64-v8a/libassembly-store.so','lib/arm64-v8a/libassembly-store.so']:
                with warnings.catch_warnings():
                    warnings.simplefilter('ignore')
                    with zipfile.ZipFile(path,'w') as z:
                        z.writestr('lib/arm64-v8a/libassembly-store.so', self.store)
                        z.writestr(extra, self.store)
                with self.assertRaises(self.mod.Unsupported): self.mod.container_store(path)
            path.write_bytes(b'not a ZIP')
            with self.assertRaises(zipfile.BadZipFile): self.mod.container_store(path)
            with zipfile.ZipFile(path,'w') as z:
                z.writestr('lib/arm64-v8a/libassembly-store.so', self.store)
                z.writestr('other.txt', b'CRC-CONTROL')
            b = path.read_bytes().replace(b'CRC-CONTROL', b'BAD-CONTROL', 1); path.write_bytes(b)
            with self.assertRaises((self.mod.Unsupported, zipfile.BadZipFile)): self.mod.container_store(path)
            aab = Path(tmp)/'constructed-fixture.aab'
            with zipfile.ZipFile(aab,'w') as z: z.writestr('base/lib/arm64-v8a/libassembly-store.so',self.store)
            entry, store = self.mod.container_store(aab)
            self.assertEqual(store,self.store)
            self.assertEqual(entry,'base/lib/arm64-v8a/libassembly-store.so')

    def test_actual_pe_changed_value_and_missing_consumer(self):
        import struct
        dotnet = Path.home()/'AppData/Local/Microsoft/dotnet/dotnet.exe'
        lz4 = dotnet.parent/'packs/Microsoft.Android.Sdk.Windows/35.0.105/tools/K4os.Compression.LZ4.dll'
        pefile = ROOT.parent/'spikes/wp2/Predict.dll'
        if not all(p.exists() for p in (dotnet,lz4,pefile)): self.skipTest('local helper/control PE absent')
        original = pefile.read_bytes()
        old = 'ca-app-pub-3940256099942544/6300978111'.encode('utf-16le')
        self.assertGreaterEqual(original.count(old),1)
        cases = [('changed-consumer-value',old,old[:-2] + '2'.encode('utf-16le'),'FAIL'),('missing-consumer',b'set_AdsId\0',b'set_XdsId\0','INCONCLUSIVE')]
        for name, old, new, expected in cases:
            self.assertIn(old,original)
            pe = original.replace(old,new)
            n = len(pe)-15
            literal = bytes([0xf0]) + b'\xff'*(n//255)+bytes([n%255])+pe
            stored = b'XALZ'+struct.pack('<II',0,len(pe))+literal
            result = self.mod.inspect(stored,'Dev',str(dotnet),lz4)
            self.assertEqual(result['status'],expected,result)
            (ROOT.parent/('ads-case-control-'+name+'.json')).write_text(__import__('json').dumps(result,indent=2))

    def test_invalid_source_hash_never_passes(self):
        for source in ['', '0'*40, 'a'*39, 'g'*40, 'a'*40+'\n']:
            r = self.mod.attest(self.apk,'Dev','not-used',Path('not-used'),source)
            self.assertEqual(r['status'],'INCONCLUSIVE')
            self.assertIn('source hash',r['reason'])

class ReportSafetyTests(unittest.TestCase):
    def test_collision_precedes_inspection_for_all_protected_inputs(self):
        import sys, tempfile
        from unittest.mock import patch
        mod = importlib.util.module_from_spec(SPEC); SPEC.loader.exec_module(mod)
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); artifact = root/'copy.apk'; artifact.write_bytes(b'invalid APK')
            decoder = root/'K4os.Compression.LZ4.dll'; decoder.write_bytes(b'decoder sentinel')
            helper = root/'dotnet.exe'; helper.write_bytes(b'helper sentinel')
            templates = Path(SPEC.origin).with_name('ads_inspector')
            for source in [artifact, decoder, helper, Path(SPEC.origin), *templates.glob('*.txt')]:
                with self.subTest(source=source):
                    before = source.read_bytes()
                    argv = ['ads', str(artifact), '--expected-mode', 'Dev', '--lz4-dll', str(decoder),
                            '--dotnet', str(helper), '--report', str(source)]
                    with patch.object(sys, 'argv', argv), patch.object(mod, 'attest') as inspect, \
                         patch.object(mod.tempfile, 'NamedTemporaryFile') as temporary:
                        with self.assertRaises(SystemExit) as exc: mod.main()
                        self.assertEqual(exc.exception.code, 2)
                        inspect.assert_not_called(); temporary.assert_not_called()
                    self.assertEqual(source.read_bytes(), before)

    def test_atomic_report_success_and_failure_cleanup(self):
        import sys, tempfile, json
        from unittest.mock import patch, Mock
        mod = importlib.util.module_from_spec(SPEC); SPEC.loader.exec_module(mod)
        for failure in ('serialize', 'write', 'replace', None):
            with self.subTest(failure=failure), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp); artifact = root/'copy.apk'; artifact.write_bytes(b'APK sentinel')
                report = root/'report.json'; report.write_bytes(b'prior report sentinel')
                argv = ['ads', str(artifact), '--expected-mode', 'Dev', '--lz4-dll', str(root/'decoder'),
                        '--report', str(report)]
                real_temp = tempfile.NamedTemporaryFile
                def temporary(*args, **kwargs):
                    handle = real_temp(*args, **kwargs)
                    if failure == 'write': handle.write = Mock(side_effect=OSError('injected write failure'))
                    return handle
                with patch.object(sys, 'argv', argv), patch.object(mod, 'attest', return_value={'status':'PASS'}), \
                     patch.object(mod.tempfile, 'NamedTemporaryFile', side_effect=temporary):
                    if failure == 'serialize':
                        with patch.object(mod.json, 'dumps', side_effect=TypeError('injected serialization failure')):
                            code = mod.main()
                    elif failure == 'replace':
                        with patch('os.replace', side_effect=OSError('injected replace failure')):
                            code = mod.main()
                    else: code = mod.main()
                self.assertEqual(code, 2 if failure else 0)
                self.assertEqual(artifact.read_bytes(), b'APK sentinel')
                if failure: self.assertEqual(report.read_bytes(), b'prior report sentinel')
                else: self.assertEqual(json.loads(report.read_text()), {'status':'PASS'})
                self.assertEqual(sorted(p.name for p in root.iterdir()), ['copy.apk', 'report.json'])

    def test_cli_rejects_artifact_aliases_without_changing_bytes(self):
        import os, subprocess, sys, tempfile
        for alias in ('same', 'relative', 'resolved', 'hardlink'):
            with self.subTest(alias=alias), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                artifact = root/'copy.apk'
                sentinel = b'invalid disposable APK: preserve these bytes'
                artifact.write_bytes(sentinel)
                report = artifact
                if alias == 'relative': report = Path('copy.apk')
                if alias == 'resolved':
                    (root/'child').mkdir()
                    report = root/'child/../copy.apk'
                if alias == 'hardlink':
                    report = root/'report.json'
                    try: os.link(artifact, report)
                    except OSError as exc: self.skipTest('hardlinks unsupported: '+str(exc))
                result = subprocess.run([sys.executable, '-B', SPEC.origin, str(artifact),
                    '--expected-mode', 'Dev', '--lz4-dll', str(root/'unused.dll'),
                    '--report', str(report)], cwd=root, capture_output=True, text=True)
                self.assertEqual(artifact.read_bytes(), sentinel, result.stdout+result.stderr)
                self.assertEqual((root/report).read_bytes(), sentinel)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn('report destination aliases protected input', result.stderr)
                self.assertEqual(sorted(p.name for p in root.iterdir()),
                    ['child', 'copy.apk'] if alias == 'resolved' else
                    ['copy.apk', 'report.json'] if alias == 'hardlink' else ['copy.apk'])

class PortableReportSafetyTests(unittest.TestCase):
    def test_report_safety_without_hermes_home(self):
        import tempfile
        from unittest.mock import patch
        with tempfile.TemporaryDirectory() as tmp:
            home = Path(tmp) / 'non-hermes-home'
            hermes = home / 'AppData/Local/hermes/cache/scratch'
            self.assertFalse(home.exists())
            with patch.object(Path, 'home', return_value=home):
                for name in (
                    'test_collision_precedes_inspection_for_all_protected_inputs',
                    'test_atomic_report_success_and_failure_cleanup',
                    'test_cli_rejects_artifact_aliases_without_changing_bytes',
                ):
                    with self.subTest(test=name):
                        ReportSafetyTests(name).debug()
            self.assertFalse(hermes.exists())
            self.assertFalse(home.exists())

if __name__ == '__main__': unittest.main()
