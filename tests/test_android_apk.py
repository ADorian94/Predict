"""Synthetic ELF/ZIP fixtures only: these are not Android builds."""
import importlib.util
from pathlib import Path
import struct
import tempfile
import unittest
import zipfile
import io
ROOT = Path(__file__).resolve().parents[1]
def tool():
    path = ROOT / "tools/ci/verify_android_apk.py"
    if not path.exists():
        raise AssertionError("Missing production APK verifier")
    spec = importlib.util.spec_from_file_location("apk_check", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

def elf(align=16384, offset=0, vaddr=0, machine=183):
    ident = b"\x7fELF" + bytes([2, 1, 1]) + bytes(9)
    header = struct.pack("<HHIQQQIHHHHHH", 3, machine, 1, 0, 64, 0, 0, 64, 56, 1, 0, 0, 0)
    segment = struct.pack("<IIQQQQQQ", 1, 5, offset, vaddr, 0, 120, 120, align)
    return ident + header + segment

class ElfTests(unittest.TestCase):
    def test_all_load_segments_are_checked(self):
        check = tool()
        segments = check.inspect_elf(elf(), "lib/arm64-v8a/good.so")
        self.assertEqual(16384, segments[0]["align"])
        for data in (elf(4096), elf(24576), elf(vaddr=4096), elf(machine=62), b"bad", elf()[:70]):
            with self.subTest(data=data.hex()):
                with self.assertRaises(ValueError):
                    check.inspect_elf(data, "bad.so")
        data = bytearray(elf())
        struct.pack_into("<H", data, 56, 2)
        data += struct.pack("<IIQQQQQQ", 1, 5, 0, 4096, 0, 120, 120, 4096)
        with self.assertRaises(ValueError):
            check.inspect_elf(data, "second-load-bad.so")

def apk_bytes(compression=zipfile.ZIP_DEFLATED, aligned=False, data=None, name="lib/arm64-v8a/libtest.so"):
    target = io.BytesIO()
    with zipfile.ZipFile(target, "w") as archive:
        info = zipfile.ZipInfo(name)
        info.compress_type = compression
        if aligned:
            padding = (16384 - 30 - len(name.encode()) - 4) % 16384
            info.extra = struct.pack("<HH", 0xCAFE, padding) + bytes(padding)
        archive.writestr(info, elf() if data is None else data)
    return target.getvalue()

class ZipTests(unittest.TestCase):
    def inspect(self, data):
        with tempfile.TemporaryDirectory(dir=ROOT) as folder:
            path = Path(folder) / "fixture.apk"
            path.write_bytes(data)
            return tool().inspect_apk(path)

    def test_stored_alignment_uses_local_header_and_compressed_is_reported(self):
        self.assertTrue(callable(getattr(tool(), "inspect_apk", None)), "Missing APK ZIP gate")
        good = self.inspect(apk_bytes(zipfile.ZIP_STORED, aligned=True))
        self.assertTrue(good["passed"], good)
        self.assertEqual(16384, good["libraries"][0]["data_offset"])
        compressed = self.inspect(apk_bytes())
        self.assertTrue(compressed["passed"], compressed)
        self.assertEqual("compressed (not mmap alignment gated)", compressed["libraries"][0]["zip_alignment"])
        for data in (apk_bytes(zipfile.ZIP_STORED), apk_bytes(data=b"broken"),
                     apk_bytes(name="lib/x86_64/bad.so"), apk_bytes(name="assets/not-native.txt"),
                     b"not a zip", b"BAD!" + apk_bytes()[4:]):
            with self.subTest(length=len(data)):
                report = self.inspect(data)
                self.assertFalse(report["passed"], report)
                self.assertTrue(report["errors"])

class SdkGateTests(unittest.TestCase):
    def test_exact_manifest_values_and_no_missing_or_duplicate_claims(self):
        check = tool()
        self.assertTrue(callable(getattr(check, "parse_badging", None)), "Missing manifest gate")
        good = "sdkVersion:'26'\ntargetSdkVersion:'36'\nnative-code: 'arm64-v8a'\n"
        self.assertEqual({"min_sdk":26, "target_sdk":36, "abis":["arm64-v8a"]}, check.parse_badging(good))
        for value in (good.replace("'36'", "'35'"), good.replace("'26'", "'25'"),
                      good.replace("'arm64-v8a'", "'arm64-v8a' 'x86_64'"), "", good + "sdkVersion:'26'\n"):
            with self.assertRaises(ValueError):
                check.parse_badging(value)

    def test_official_command_arguments_and_real_subprocess_failure_fail_gate(self):
        import sys
        check = tool()
        self.assertTrue(callable(getattr(check, "verify", None)), "Missing external command gates")
        with tempfile.TemporaryDirectory(dir=ROOT) as folder:
            apk = Path(folder) / "fixture.apk"
            apk.write_bytes(apk_bytes())
            # Test-only subprocesses emulate command outputs, not Android packaging.
            badging = "sdkVersion:'26'\ntargetSdkVersion:'36'\nnative-code: 'arm64-v8a'"
            commands = {
                "aapt": [sys.executable, "-c", "import sys; assert sys.argv[1:3] == ['dump','badging']; print(" + repr(badging) + ")"],
                "zipalign": [sys.executable, "-c", "import sys; assert sys.argv[1:6] == ['-c','-P','16','-v','4']"],
                "apksigner": [sys.executable, "-c", "import sys; assert sys.argv[1:4] == ['verify','--verbose','--print-certs']"],
            }
            result = check.verify(apk, commands)
            self.assertTrue(result["passed"], result)
            self.assertEqual(3, len(result["commands"]))
            for name in commands:
                failing = dict(commands, **{name:[sys.executable,"-c","raise SystemExit(7)"]})
                failed = check.verify(apk, failing)
                self.assertFalse(failed["passed"], failed)
                self.assertTrue(any(c["returncode"] == 7 for c in failed["commands"]))

class DetailedReportTests(unittest.TestCase):
    def test_misaligned_zip_still_records_elf_segments_and_checks_every_library(self):
        check = tool()
        with tempfile.TemporaryDirectory(dir=ROOT) as folder:
            path = Path(folder) / "fixture.apk"
            with zipfile.ZipFile(path, "w") as archive:
                archive.writestr("lib/arm64-v8a/one.so", elf())
                archive.writestr("lib/arm64-v8a/two.so", elf(4096), compress_type=zipfile.ZIP_DEFLATED)
            report = check.inspect_apk(path)
            self.assertFalse(report["passed"])
            self.assertEqual(2, len(report["libraries"]))
            self.assertIn("load_segments", report["libraries"][0])
            self.assertTrue(report["libraries"][1]["errors"])

    def test_cli_failure_writes_machine_report_and_summary(self):
        import subprocess
        import sys
        import json
        with tempfile.TemporaryDirectory(dir=ROOT) as folder:
            folder = Path(folder)
            apk = folder / "malformed.apk"
            apk.write_bytes(b"malformed")
            report, summary = folder / "report.json", folder / "report.md"
            result = subprocess.run([sys.executable, str(ROOT / "tools/ci/verify_android_apk.py"),
                "--apk", str(apk), "--build-tools", str(folder / "missing-sdk"),
                "--report", str(report), "--summary", str(summary)], capture_output=True, text=True)
            self.assertEqual(1, result.returncode, result.stderr)
            saved = json.loads(report.read_text())
            self.assertFalse(saved["passed"])
            self.assertFalse(saved["runtime_16kb_verified"])
            self.assertEqual(3, len(saved["commands"]))
            self.assertIn("FAIL", summary.read_text())
