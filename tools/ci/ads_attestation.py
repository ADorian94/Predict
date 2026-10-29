"""Bounded, fail-closed static inspection of ARM64 .NET Android assembly stores."""
import argparse
import hashlib
import json
import os
import re
import struct
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

MAX_STORE = 128 * 1024 * 1024
MAX_ARTIFACT = 1024 * 1024 * 1024

class Unsupported(ValueError):
    pass

def require(ok, reason):
    if not ok:
        raise Unsupported(reason)

def region(data, offset, size):
    require(0 <= offset <= len(data) and 0 <= size <= len(data) - offset, 'out-of-bounds region')
    return data[offset:offset + size]

def extract_predict(elf):
    require(64 <= len(elf) <= MAX_STORE, 'ELF size bound')
    require(elf[:7] == b'\x7fELF\x02\x01\x01', 'requires ELF64 little endian v1')
    require(struct.unpack_from('<HHI', elf, 16) == (3, 183, 1), 'requires AArch64 shared ELF')
    shoff = struct.unpack_from('<Q', elf, 40)[0]
    ehsize, _, _, shsize, count, strings = struct.unpack_from('<6H', elf, 52)
    require(ehsize == 64 and shsize == 64 and 1 <= count <= 4096 and 0 < strings < count, 'unsupported ELF section table')
    region(elf, shoff, count * shsize)
    sections = [struct.unpack_from('<IIQQQQIIQQ', elf, shoff + i * shsize) for i in range(count)]
    st = sections[strings]
    require(st[1] == 3 and st[5] <= 1024 * 1024, 'invalid section string table')
    names = region(elf, st[4], st[5])
    payloads = []
    for s in sections:
        require(s[0] < len(names), 'section name offset')
        end = names.find(b'\0', s[0])
        require(end >= 0, 'unterminated section name')
        if s[1] != 8:
            region(elf, s[4], s[5])
        if names[s[0]:end] == b'payload':
            require(s[1] == 1, 'payload must be PROGBITS')
            payloads.append(region(elf, s[4], s[5]))
    require(len(payloads) == 1, 'requires unique payload section')
    data = payloads[0]
    require(len(data) >= 20, 'truncated store header')
    magic, version, count, indexes, index_size = struct.unpack_from('<5I', data)
    require(magic == 0x41424158 and version == 0x80010003, 'unsupported XABA version/architecture')
    require(1 <= count <= 4096 and indexes == count * 2 and index_size == indexes * 13, 'invalid store counts')
    base = 20 + index_size
    cursor = base + count * 28
    region(data, 20, index_size + count * 28)
    for i in range(indexes):
        _, descriptor, ignore = struct.unpack_from('<QIB', data, 20 + i * 13)
        require(descriptor < count and ignore in (0, 1), 'invalid store index')
    entries = []
    seen = set()
    for i in range(count):
        desc = struct.unpack_from('<7I', data, base + i * 28)
        require(desc[0] == i, 'unsupported mapping index')
        n = struct.unpack('<I', region(data, cursor, 4))[0]
        cursor += 4
        require(1 <= n <= 1024, 'invalid assembly name length')
        name = region(data, cursor, n).decode('utf-8', errors='strict')
        cursor += n
        require(name not in seen and '\0' not in name and '\\' not in name and all(part not in ('', '.', '..') for part in name.split('/')), 'duplicate/invalid assembly name')
        seen.add(name)
        require(desc[2] > 0, 'empty assembly')
        region(data, desc[1], desc[2])
        for off, size in ((desc[3], desc[4]), (desc[5], desc[6])):
            require((off == 0) == (size == 0), 'invalid auxiliary descriptor')
            region(data, off, size)
        entries.append((name, desc))
    ranges = []
    for name, d in entries:
        for off, size in ((d[1], d[2]), (d[3], d[4]), (d[5], d[6])):
            if size:
                require(off >= cursor, 'descriptor overlaps store metadata')
                ranges.append((off, off + size))
    ranges.sort()
    require(all(a[1] <= b[0] for a, b in zip(ranges, ranges[1:])), 'overlapping store data')
    matches = [d for name, d in entries if name == 'Predict.dll']
    require(len(matches) == 1, 'requires exactly one Predict.dll')
    d = matches[0]
    stored = region(data, d[1], d[2])
    require(len(stored) >= 12 and stored[:4] == b'XALZ', 'unsupported assembly compression')
    require(1 <= struct.unpack_from('<I', stored, 8)[0] <= 16 * 1024 * 1024, 'unsafe decompression size')
    return stored

def sha(data):
    return hashlib.sha256(data).hexdigest()

def container_store(path):
    require(path.stat().st_size <= MAX_ARTIFACT, 'artifact exceeds bound')
    kind = path.suffix.lower()
    require(kind in ('.apk', '.aab'), 'expected APK or AAB')
    target = ('base/' if kind == '.aab' else '') + 'lib/arm64-v8a/libassembly-store.so'
    with zipfile.ZipFile(path) as z:
        infos = z.infolist()
        require(len(infos) <= 100000, 'ZIP entry count bound')
        names = [i.filename for i in infos]
        require(len(names) == len(set(names)), 'duplicate ZIP entry')
        stores = [n for n in names if 'assembly-store' in n.lower() or n.lower().endswith('/predict.dll')]
        require(stores == [target], 'unexpected ABI/module/application store inventory')
        info = z.getinfo(target)
        require(info.file_size <= MAX_STORE and not info.flag_bits & 1, 'unsafe ZIP store')
        require(sum(i.file_size for i in infos) <= 2 * MAX_ARTIFACT and all(i.file_size <= MAX_ARTIFACT and not i.flag_bits & 1 for i in infos), 'unsafe ZIP expansion/encryption')
        require(z.testzip() is None, 'ZIP CRC failure')
        store = z.read(info)
    return target, store

def inspect(stored, mode, dotnet, lz4):
    templates = Path(__file__).with_name('ads_inspector')
    require(lz4.is_file(), 'explicit LZ4 DLL missing')
    require(lz4.name == 'K4os.Compression.LZ4.dll', 'unexpected LZ4 DLL name')
    with tempfile.TemporaryDirectory(prefix='ads-inspector-') as tmp:
        root = Path(tmp)
        # Independent directory deliberately avoids the app global.json and Compile glob.
        (root / 'global.json').write_text('{"sdk":{"version":"9.0.100","rollForward":"latestFeature"}}')
        (root / 'Program.cs').write_bytes((templates / 'Program.cs.txt').read_bytes())
        (root / 'Inspector.csproj').write_bytes((templates / 'Inspector.csproj.txt').read_bytes())
        (root / 'input.bin').write_bytes(stored)
        build = subprocess.run([dotnet, 'build', str(root / 'Inspector.csproj'), '--nologo', '-v:q', '-p:Lz4Path=' + str(lz4.resolve())], cwd=root, capture_output=True, text=True, timeout=120)
        require(build.returncode == 0, 'helper build failed: ' + build.stdout[-4000:] + build.stderr[-1000:])
        run = subprocess.run([dotnet, str(root / 'bin/Debug/net9.0/Inspector.dll'), str(root / 'input.bin'), mode], cwd=root, capture_output=True, text=True, timeout=30)
        result = json.loads(run.stdout)
        require(result.get('status') in ('PASS', 'FAIL', 'INCONCLUSIVE'), 'invalid helper status')
        require(run.returncode == {'PASS': 0, 'FAIL': 1, 'INCONCLUSIVE': 2}[result['status']], 'helper status/exit disagreement')
        result['helper_binary_sha256'] = sha((root / 'bin/Debug/net9.0/Inspector.dll').read_bytes())
        result['dotnet_sdk'] = subprocess.check_output([dotnet, '--version'], cwd=root, text=True, timeout=15).strip()
        return result

def attest(path, mode, dotnet, lz4, source_hash=None):
    report = dict(status='INCONCLUSIVE', expected_mode=mode, source_hash=source_hash)
    try:
        require(mode in ('Dev', 'Production'), 'invalid expected mode')
        require(source_hash is None or re.fullmatch(r'[0-9a-fA-F]{40}', source_hash) and int(source_hash, 16) != 0, 'invalid source hash; requires nonzero 40 hex')
        report['artifact_sha256'] = sha(path.read_bytes()) if path.stat().st_size <= MAX_ARTIFACT else None
        entry, store = container_store(path)
        report.update(container_entry=entry, container_entry_sha256=sha(store))
        stored = extract_predict(store)
        report['stored_assembly_sha256'] = sha(stored)
        report['tool_provenance'] = {str(p.name): sha(p.read_bytes()) for p in [Path(__file__), *Path(__file__).with_name('ads_inspector').glob('*.txt')]}
        report['lz4_dll_sha256'] = sha(lz4.read_bytes())
        report.update(inspect(stored, mode, dotnet, lz4))
    except (Exception,) as e:
        report.update(status='INCONCLUSIVE', reason=str(e))
    return report

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('artifact', type=Path)
    p.add_argument('--expected-mode', choices=['Dev', 'Production'], required=True)
    p.add_argument('--source-hash')
    p.add_argument('--dotnet', default='dotnet')
    p.add_argument('--lz4-dll', type=Path, required=True)
    p.add_argument('--report', type=Path, required=True)
    a = p.parse_args()
    protected = [a.artifact, a.lz4_dll, Path(__file__),
                 *Path(__file__).with_name('ads_inspector').glob('*.txt')]
    dotnet = Path(a.dotnet)
    if dotnet.is_file():
        protected.append(dotnet)
    for source in protected:
        if (a.report.resolve() == source.resolve() or
                (a.report.exists() and source.exists() and a.report.samefile(source))):
            p.error('report destination aliases protected input: ' + str(source))
    report = attest(a.artifact, a.expected_mode, a.dotnet, a.lz4_dll, a.source_hash)
    temporary = None
    try:
        serialized = json.dumps(report, indent=2) + '\n'
        with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8',
                dir=a.report.parent, prefix='.' + a.report.name + '.', suffix='.tmp',
                delete=False) as handle:
            temporary = Path(handle.name)
            handle.write(serialized)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, a.report)
        temporary = None
    except (OSError, TypeError, ValueError) as exc:
        print('report publication failed: ' + str(exc), file=sys.stderr)
        return 2
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)
    print(serialized.strip())
    return {'PASS': 0, 'FAIL': 1, 'INCONCLUSIVE': 2}[report['status']]

if __name__ == '__main__':
    sys.exit(main())
