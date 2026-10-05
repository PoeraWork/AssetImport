"""Make the full PMX Auto kit from Packed plus original texture fixtures.

No user models/motions/textures, MMDD, ME or game assemblies are included.
"""
import argparse
import binascii
import hashlib
from pathlib import Path
import struct
import sys
import zipfile
import zlib
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from package import ROOT, write_zip

def pack(fmt, *values):
    return struct.pack('<' + fmt, *values)

def text(value):
    data = value.encode('utf-8')
    return pack('i', len(data)) + data

def pmx(texture):
    b = b'PMX ' + pack('f', 2) + bytes((8, 1, 0, 1, 1, 1, 1, 1, 1))
    b += text('AssetImport original texture fixture') + text('') + text('Original four-color card') + text('')
    vertices = ((-6, 0, 0, 0, 1), (6, 0, 0, 1, 1), (6, 12, 0, 1, 0), (-6, 12, 0, 0, 0))
    b += pack('i', 4)
    for x, y, z, u, v in vertices:
        b += pack('8fBBf', x, y, z, 0, 0, -1, u, v, 0, 0, 1)
    b += pack('i', 6) + bytes((0, 1, 2, 0, 2, 3)) + pack('i', 1) + text(texture)
    b += pack('i', 1) + text('ColorCard') + text('') + pack('4f', 1, 1, 1, 1) + bytes(28)
    b += bytes((1,)) + bytes(20) + pack('bbBBB', 0, -1, 0, 1, 0) + text('') + pack('i', 6)
    b += pack('i', 1) + text('全ての親') + text('Root') + pack('3fbiH3f', 0, 0, 0, -1, 0, 30, 0, 1, 0)
    return b + pack('4i', 0, 0, 0, 0)

def texture_files():
    size = 128
    colors = ((255, 0, 0), (0, 255, 0), (0, 0, 255), (255, 255, 0))
    rgb = b''.join((bytes(colors[y // (size // 2) * 2 + x // (size // 2)]) for y in range(size) for x in range(size)))
    bgr = b''.join((rgb[i:i + 3][::-1] for i in range(0, len(rgb), 3)))
    bmp = b'BM' + pack('IHHI', 54 + len(bgr), 0, 0, 54)
    bmp += pack('IiiHHIIiiII', 40, size, size, 1, 24, 0, len(bgr), 0, 0, 0, 0)
    bmp += b''.join((bgr[y * size * 3:(y + 1) * size * 3] for y in reversed(range(size))))
    tga = pack('BBB5s4HBB', 0, 0, 2, bytes(5), 0, 0, size, size, 24, 32) + bgr
    dds = bytearray(128)
    dds[:4] = b'DDS '
    for offset, value in ((4, 124), (8, 528391), (12, size), (16, size), (20, size * size), (76, 32), (80, 4), (108, 4096)):
        struct.pack_into('<I', dds, offset, value)
    dds[84:88] = b'DXT5'
    for y in range(size // 4):
        for x in range(size // 4):
            r, g, b = colors[y // (size // 8) * 2 + x // (size // 8)]
            dds += bytes((255, 255)) + bytes(6) + pack('HHI', r >> 3 << 11 | g >> 2 << 5 | b >> 3, 0, 0)

    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', binascii.crc32(kind + data) & 4294967295)
    scanlines = b''.join((b'\x00' + rgb[y * size * 3:(y + 1) * size * 3] for y in range(size)))
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', size, size, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(scanlines)) + chunk(b'IEND', b'')
    return {'colors.bmp': bmp, 'colors.tga': tga, 'colors.dds': bytes(dds), 'expected.png': png}

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--packed', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    with zipfile.ZipFile(a.packed) as z:
        files = {name: z.read(name) for name in z.namelist()}
    prefix = 'UserData/AssetImport/PMX_Auto_Test/'
    for name, data in texture_files().items():
        files[prefix + 'Textures/' + name] = data
    for extension in ('bmp', 'tga', 'dds'):
        files[prefix + 'Textures/texture_' + extension.upper() + '.pmx'] = pmx('colors.' + extension)
    instructions = 'AssetImport\nClose the game and Studio before installing. Copy the complete BepInEx and runtimes directories to the matching game root. Open AssetImport with Left Alt + I.\n'
    files[prefix + 'collect_logs.cmd'] = b'@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0collect_logs.ps1"\r\npause\r\n'
    collector = "$ErrorActionPreference = 'Stop'\n$gameRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path\nif (!(Test-Path (Join-Path $gameRoot 'CharaStudio.exe'))) { throw 'Copy UserData into the KK game root before collecting logs.' }\n$report = Join-Path $PSScriptRoot ('logs-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))\nNew-Item -ItemType Directory -Path $report | Out-Null\n$found = @()\nforeach ($relative in @('BepInEx/LogOutput.log','output_log.txt','CharaStudio_Data/output_log.txt')) {\n  $file = Join-Path $gameRoot $relative\n  if (Test-Path $file) {\n    Copy-Item -LiteralPath $file -Destination (Join-Path $report ($relative -replace '[/\\\\]','_'))\n    $found += 'FOUND ' + $relative\n  } else { $found += 'MISSING ' + $relative }\n}\nCopy-Item -LiteralPath (Join-Path $PSScriptRoot 'result.txt') -Destination $report\n$found | Out-File -Encoding UTF8 (Join-Path $report 'files.txt')\nWrite-Host ('Report: ' + $report)\n"
    files[prefix + 'collect_logs.ps1'] = collector.replace('\n', '\r\n').encode('utf-8-sig')
    a.output.parent.mkdir(parents=True, exist_ok=True)
    write_zip(a.output, files)
    a.output.with_suffix('.zip.sha256').write_text(hashlib.sha256(a.output.read_bytes()).hexdigest() + '  ' + a.output.name + '\n')
    print(a.output)
if __name__ == '__main__':
    main()
