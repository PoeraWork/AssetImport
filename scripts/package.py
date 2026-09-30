#!/usr/bin/env python3
"""Package the KK build with only its redistributable runtime dependencies."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PLUGIN = "BepInEx/plugins/AssetImportKK/"


def write_zip(path, files):
    # Stable ordering and timestamps make identical builds produce identical ZIPs.
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name, data in sorted(files.items()):
            info = zipfile.ZipInfo(name, (2026, 9, 30, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            archive.writestr(info, data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts")
    parser.add_argument("--configuration", default="Release", choices=["Debug", "Release"])
    parser.add_argument("--source", action="store_true", help="Also create a source archive (no generated build references)")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    project = ROOT / "src" / "AssetImport"
    build = project / "bin" / args.configuration / "net35"
    version = re.search(r'const string Version = "([^"]+)"', (project / "AssetImport.cs").read_text(encoding="utf-8-sig")).group(1)
    names = ["KK_AssetImport.dll", "AssimpNet.dll", "LitJSON.dll", "runtimes/win-x64/native/assimp.dll"]
    for name in names:
        if not (build / name).is_file():
            raise SystemExit("Missing {}. First run dotnet build src/AssetImport/AssetImport.csproj -c {}".format(name, args.configuration))
    source_times = [p.stat().st_mtime for p in project.rglob("*.cs") if "obj" not in p.parts and "bin" not in p.parts]
    source_times.append((project / "AssetImport.csproj").stat().st_mtime)
    if max(source_times) > (build / "KK_AssetImport.dll").stat().st_mtime:
        raise SystemExit("The KK DLL is older than its sources. Rebuild before packaging.")
    files = {PLUGIN + name: (build / name).read_bytes() for name in names}
    for relative in ["docs/KK-TESTING.md", "docs/licenses/AssimpNet.txt", "docs/licenses/Assimp.txt", "docs/licenses/LitJson.txt"]:
        files["AssetImportKK/" + relative.removeprefix("docs/")] = (ROOT / relative).read_bytes()
    for fixture in sorted((ROOT / "tests" / "fixtures").rglob("*")):
        if fixture.is_file() and not fixture.name.startswith("."):
            files["AssetImportKK/TestAssets/" + fixture.relative_to(ROOT / "tests" / "fixtures").as_posix()] = fixture.read_bytes()
    manifest = {
        "plugin": "KK_AssetImport", "version": version, "status": "preview; Windows game validation pending",
        "target": "Koikatsu / Unity 5.6 / .NET 3.5 / Windows x64",
        "dependencies": {"AssimpNet": "5.0.0-beta1", "native Assimp": "5.0.1", "LitJSON": "0.19.0"},
        "requiredPlugins": {"BepInEx": "5.4.22+", "KKAPI": "1.45.1+", "KK_MaterialEditor": "5.0+", "LoadFileLimitedFix": "KK build"},
        "sha256": {name: hashlib.sha256(data).hexdigest() for name, data in sorted(files.items())}
    }
    files["AssetImportKK/manifest.json"] = (json.dumps(manifest, indent=2, ensure_ascii=False) + "\n").encode()
    package = output / ("KK_AssetImport_" + version + "_preview.zip")
    write_zip(package, files)
    created = [package]
    if args.source:
        sources = {}
        for path in ROOT.rglob("*"):
            relative = path.relative_to(ROOT)
            if not path.is_file() or any(part in {".git", "bin", "obj", "artifacts", ".build-downloads", "dotnet-home", "__pycache__"} for part in relative.parts):
                continue
            if output == path.parent or output in path.parents:
                continue
            if relative.parts[:3] == ("src", "Custom Dependencies", "KK") and path.suffix == ".dll":
                continue
            sources["AssetImport/" + relative.as_posix()] = path.read_bytes()
        source_zip = output / ("AssetImport_" + version + "_KK_source.zip")
        write_zip(source_zip, sources)
        created.append(source_zip)
    for path in created:
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        path.with_suffix(path.suffix + ".sha256").write_text(digest + "  " + path.name + "\n")
        print(path)


if __name__ == "__main__":
    main()
