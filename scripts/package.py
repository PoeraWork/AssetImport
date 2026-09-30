#!/usr/bin/env python3
"""Package the KK build with only its redistributable runtime dependencies."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PLUGIN = "BepInEx/plugins/AssetImport/"
NATIVE = "runtimes/win-x64/native/assimp.dll"


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
    parser.add_argument("--test-assets", action="store_true", help="Also create a separate optional test-assets archive")
    parser.add_argument("--dll-update", action="store_true", help="Also package only the plugin DLL for existing full 4.1.1/4.1.2 installs")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    project = ROOT / "src" / "AssetImport"
    build = project / "bin" / args.configuration / "net35"
    version = re.search(r'const string Version = "([^"]+)"', (project / "AssetImport.cs").read_text(encoding="utf-8-sig")).group(1)
    names = ["KK_AssetImport.dll", "AssimpNet.dll", "LitJSON.dll", NATIVE]
    for name in names:
        if not (build / name).is_file():
            raise SystemExit("Missing {}. First run dotnet build src/AssetImport/AssetImport.csproj -c {}".format(name, args.configuration))
    source_times = [p.stat().st_mtime for p in project.rglob("*.cs") if "obj" not in p.parts and "bin" not in p.parts]
    source_times.append((project / "AssetImport.csproj").stat().st_mtime)
    if max(source_times) > (build / "KK_AssetImport.dll").stat().st_mtime:
        raise SystemExit("The KK DLL is older than its sources. Rebuild before packaging.")
    files = {(name if name == NATIVE else PLUGIN + name): (build / name).read_bytes() for name in names}
    for name in ["AssimpNet.txt", "Assimp.txt", "LitJson.txt"]:
        files[PLUGIN + "licenses/" + name] = (ROOT / "docs" / "licenses" / name).read_bytes()
    # Windows Notepad-friendly instructions; testing and build metadata stay outside the install ZIP.
    instructions = (ROOT / "docs" / "INSTALL-KK.txt").read_text(encoding="utf-8-sig")
    files["安装说明.txt"] = instructions.replace("\n", "\r\n").encode("utf-8-sig")
    manifest = {
        "plugin": "KK_AssetImport", "version": version, "status": "preview; Windows game validation pending",
        "target": "Koikatsu / Unity 5.6 / .NET 3.5 / Windows x64",
        "dependencies": {"AssimpNet": "5.0.0-beta1", "native Assimp": "5.0.1", "LitJSON": "0.19.0"},
        "requiredPlugins": {"BepInEx": "5.4.22+", "KKAPI": "1.45.1+", "KK_MaterialEditor": "4.0.3+", "LoadFileLimitedFix": "KK build"},
        "sha256": {name: hashlib.sha256(data).hexdigest() for name, data in sorted(files.items())}
    }
    package = output / ("KK_AssetImportv" + version + "Packed.zip")
    write_zip(package, files)
    package.with_suffix(".manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    created = [package]
    if args.dll_update:
        update_instructions = (
            "KK AssetImport " + version + " — 已安装 4.1.1 / 4.1.2 用户的小更新包\n\n"
            "退出 KK 和 Studio，把本包 BepInEx 文件夹复制到 Koikatu.exe 所在的游戏目录并覆盖。\n"
            "实际替换：BepInEx/plugins/AssetImport/KK_AssetImport.dll。\n"
            "不要把新 DLL 与旧 DLL 并排放，也不要只把旧 DLL 改名留在 plugins 里。\n"
            "保留现有 AssimpNet、LitJSON、runtimes 和 ME；无需更换 ME 5.0 或重新下载 demo。\n"
            "此包仅用于已装完整 4.1.1 / 4.1.2 的 KK 环境，首次安装请用 Packed 完整包。\n\n"
            "修复目标：日志中 InvalidDataException 类型无法加载导致的节点转换中断。\n"
            "修复已通过编译、几何及 DLL 兼容回归；游戏内显示仍需复测。\n"
            "重新启动 Studio，导入原来的两个 FBX 小房子；日志启动段应显示 KK_AssetImport " + version + "。\n"
            "若仍失败，发游戏根目录 output_log.txt 或 BepInEx/LogOutput.log。\n"
        )
        update = output / ("KK_AssetImportv" + version + "_DLL_Update.zip")
        write_zip(update, {PLUGIN + "KK_AssetImport.dll": files[PLUGIN + "KK_AssetImport.dll"],
                           "更新说明.txt": update_instructions.replace("\n", "\r\n").encode("utf-8-sig")})
        created.append(update)
    if args.test_assets:
        fixtures = {"Windows测试说明.md": (ROOT / "docs" / "KK-TESTING.md").read_bytes()}
        for fixture in sorted((ROOT / "tests" / "fixtures").rglob("*")):
            if fixture.is_file() and not fixture.name.startswith("."):
                fixtures["TestAssets/" + fixture.relative_to(ROOT / "tests" / "fixtures").as_posix()] = fixture.read_bytes()
        test_zip = output / ("KK_AssetImportv" + version + "TestAssets.zip")
        write_zip(test_zip, fixtures)
        created.append(test_zip)
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
