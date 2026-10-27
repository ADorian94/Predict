"""Fail-closed APK static gates; no claim about device/runtime compatibility."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import sys
import zipfile
import zlib

PAGE = 16384

def inspect_elf(data, name):
    def fail(message):
        raise ValueError(name + ": " + message)
    if len(data) < 64 or data[:7] != b"\x7fELF\x02\x01\x01":
        fail("expected ELF64 little-endian version 1")
    header = struct.unpack_from("<HHIQQQIHHHHHH", data, 16)
    kind, machine, version, _, phoff, _, _, ehsize, entsize, count, _, _, _ = header
    if kind != 3 or machine != 183 or version != 1 or ehsize != 64 or entsize != 56 or not count or count == 65535:
        fail("invalid ARM64 shared object/program-header table")
    if phoff < 64 or phoff + count * entsize > len(data):
        fail("truncated program-header table")
    loads = []
    for i in range(count):
        ptype, flags, offset, vaddr, _, filesz, memsz, align = struct.unpack_from("<IIQQQQQQ", data, phoff + i * entsize)
        if ptype != 1:
            continue
        if align < PAGE or align & (align - 1):
            fail("PT_LOAD alignment must be a power of two >= 16384")
        if offset % PAGE != vaddr % PAGE or offset % align != vaddr % align:
            fail("PT_LOAD offset/virtual-address incongruence")
        if filesz > memsz or offset + filesz > len(data):
            fail("PT_LOAD file range outside ELF or larger than memory")
        loads.append({"index": i, "offset": offset, "vaddr": vaddr, "file_size": filesz,
                      "memory_size": memsz, "flags": flags, "align": align})
    if not loads:
        fail("missing PT_LOAD segments")
    return loads

def inspect_apk(path):
    report = {"libraries": [], "errors": []}
    try:
        with zipfile.ZipFile(path) as archive, open(path, "rb") as raw:
            seen = set()
            for info in archive.infolist():
                if not info.filename.lower().endswith(".so"):
                    continue
                entry = {"name": info.filename, "compression": info.compress_type, "errors": []}
                report["libraries"].append(entry)
                try:
                    if info.filename in seen:
                        raise ValueError("duplicate native entry")
                    seen.add(info.filename)
                    if not re.fullmatch(r"lib/arm64-v8a/[^/]+\.so", info.filename):
                        raise ValueError("unexpected ABI/native library path")
                    raw.seek(info.header_offset)
                    header = raw.read(30)
                    if len(header) != 30 or header[:4] != b"PK\x03\x04":
                        raise ValueError("invalid local ZIP header")
                    fields = struct.unpack("<4s5H3I2H", header)
                    flags, method, name_length, extra_length = fields[2], fields[3], fields[-2], fields[-1]
                    if method != info.compress_type or flags != info.flag_bits or flags & 1:
                        raise ValueError("local/central ZIP mismatch or encrypted library")
                    name_bytes = raw.read(name_length)
                    if name_bytes.decode("utf-8" if flags & 0x800 else "cp437") != info.filename:
                        raise ValueError("local/central filename mismatch")
                    offset = info.header_offset + 30 + name_length + extra_length
                    entry["data_offset"] = offset
                    if offset + info.compress_size > path.stat().st_size:
                        raise ValueError("native entry exceeds ZIP bounds")
                    if method == zipfile.ZIP_STORED:
                        entry["zip_alignment"] = "16 KB aligned" if offset % PAGE == 0 else "MISALIGNED"
                        if offset % PAGE:
                            error = "uncompressed native data offset is not 16 KB aligned"
                            entry["errors"].append(error)
                            report["errors"].append(info.filename + ": " + error)
                    elif method == zipfile.ZIP_DEFLATED:
                        entry["zip_alignment"] = "compressed (not mmap alignment gated)"
                    else:
                        raise ValueError("unsupported native compression method")
                    entry["load_segments"] = inspect_elf(archive.read(info), info.filename)
                except (ValueError, OSError, struct.error, zipfile.BadZipFile, zlib.error, RuntimeError) as error:
                    entry["errors"].append(str(error))
                    report["errors"].append(info.filename + ": " + str(error))
            if not report["libraries"]:
                report["errors"].append("APK has no native .so libraries")
    except (OSError, ValueError, zipfile.BadZipFile, zlib.error, RuntimeError) as error:
        report["errors"].append(str(error))
    report["passed"] = not report["errors"]
    return report

def parse_badging(text):
    def one(pattern):
        matches = re.findall(pattern, text, re.MULTILINE)
        if len(matches) != 1:
            raise ValueError("Missing or duplicate aapt field: " + pattern)
        return matches[0]
    minimum = int(one(r"^sdkVersion:'([0-9]+)'\s*$"))
    target = int(one(r"^targetSdkVersion:'([0-9]+)'\s*$"))
    abis = re.findall(r"'([^']+)'", one(r"^native-code:(.+)$"))
    if minimum != 26 or target != 36 or abis != ["arm64-v8a"]:
        raise ValueError(f"Unexpected APK manifest/ABI: min={minimum}, target={target}, abis={abis}")
    return {"min_sdk": minimum, "target_sdk": target, "abis": abis}


def verify(apk, commands):
    report = inspect_apk(apk)
    report.update({"apk": str(apk.resolve()), "commands": [], "runtime_16kb_verified": False})
    try:
        report["sha256"] = hashlib.sha256(apk.read_bytes()).hexdigest()
    except OSError as error:
        report["errors"].append(str(error))
    arguments = {"aapt": ["dump", "badging", str(apk)],
                 "zipalign": ["-c", "-P", "16", "-v", "4", str(apk)],
                 "apksigner": ["verify", "--verbose", "--print-certs", str(apk)]}
    for name, args in arguments.items():
        command = commands[name] + args
        record = {"tool": name, "command": command}
        report["commands"].append(record)
        try:
            result = subprocess.run(command, check=True, capture_output=True, text=True, timeout=180)
            record.update({"returncode": result.returncode, "stdout": result.stdout, "stderr": result.stderr})
            if name == "aapt":
                report["manifest"] = parse_badging(result.stdout)
        except subprocess.CalledProcessError as error:
            record.update({"returncode": error.returncode, "stdout": error.stdout, "stderr": error.stderr})
            report["errors"].append(f"{name} failed with exit {error.returncode}")
        except (OSError, ValueError, subprocess.TimeoutExpired) as error:
            record["error"] = str(error)
            report["errors"].append(name + ": " + str(error))
    report["passed"] = not report["errors"]
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apk", required=True, type=Path)
    parser.add_argument("--build-tools", required=True, type=Path)
    parser.add_argument("--report", required=True, type=Path)
    parser.add_argument("--summary", required=True, type=Path)
    args = parser.parse_args()
    windows = os.name == "nt"
    commands = {name: [str((args.build_tools / (name + suffix)).resolve())]
                for name, suffix in (("aapt", ".exe" if windows else ""),
                                     ("zipalign", ".exe" if windows else ""),
                                     ("apksigner", ".bat" if windows else ""))}
    report = verify(args.apk.resolve(), commands)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    summary = ("# APK static verification: " + ("PASS" if report["passed"] else "FAIL") + "\n\n"
               + f"APK: `{args.apk.name}`\n\nSHA-256: `{report.get('sha256', 'unavailable')}`\n\n"
               + f"Native libraries inspected: {len(report['libraries'])}\n\n"
               + "Manifest: " + json.dumps(report.get("manifest", {})) + "\n\n"
               + "16 KB ELF/ZIP, official zipalign and signature are static gates only. "
                 "16 KB ARM64 device runtime acceptance remains REQUIRED.\n"
               + "".join("\n- " + error for error in report["errors"]) + "\n")
    args.summary.parent.mkdir(parents=True, exist_ok=True)
    args.summary.write_text(summary, encoding="utf-8")
    print(summary)
    return 0 if report["passed"] else 1

if __name__ == "__main__":
    sys.exit(main())
