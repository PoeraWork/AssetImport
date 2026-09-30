#!/usr/bin/env python3
"""Build the offline KK/MaterialEditor A/B test kit from verified inputs."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import urllib.request
import zipfile

from package import ROOT, NATIVE, PLUGIN, write_zip
from prepare_dependencies import checked


def fetch(entry, cache):
    path = cache / entry["archive"]
    if path.exists():
        return checked(path.read_bytes(), entry["sha256"], path.name)
    print("Downloading:", entry["url"], flush=True)
    request = urllib.request.Request(entry["url"], headers={"User-Agent": "AssetImport-testkit"})
    with urllib.request.urlopen(request, timeout=120) as response:
        data = checked(response.read(), entry["sha256"], path.name)
    path.write_bytes(data)
    return data


def read_zip(data):
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        return {name: archive.read(name) for name in archive.namelist() if not name.endswith("/")}


def zip_bytes(files):
    stream = io.BytesIO()
    write_zip(stream, files)
    return stream.getvalue()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plugin-package", type=Path, required=True,
                        help="KK_AssetImportvX.Y.ZPacked.zip produced by scripts/package.py")
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts")
    parser.add_argument("--cache", type=Path, default=ROOT / ".build-downloads")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    args.cache.mkdir(parents=True, exist_ok=True)
    package_manifest = json.loads(args.plugin_package.with_suffix(".manifest.json").read_text())
    version = package_manifest["version"]
    plugin = read_zip(args.plugin_package.read_bytes())
    if set(plugin) != set(package_manifest["sha256"]):
        raise ValueError("Plugin package and manifest have different contents")
    for name, digest in package_manifest["sha256"].items():
        checked(plugin[name], digest, name)
    # Only ship maintained kit files, never runtime reports or local hidden files.
    files = {}
    for path in sorted((ROOT / "testkit").rglob("*")):
        rel = path.relative_to(ROOT / "testkit")
        if (not path.is_file() or any(part.startswith(".") for part in rel.parts)
                or rel.parts[0] in {"Reports", "CollectedLogs", "Diagnostic", "Installers"}):
            continue
        data = path.read_bytes()
        if path.suffix in {".cmd", ".ps1"}:
            data = data.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")
        files[rel.as_posix()] = data
    model_manifest = json.loads(files["models.json"])
    if model_manifest["plugin"] != version:
        raise ValueError("Update the test-kit instructions and model manifest for this plugin version before packaging")
    demos = model_manifest["demos"]
    if len(demos) != 19 or len({demo["format"] for demo in demos}) != 18:
        raise ValueError("Expected 19 demos covering 18 UI extensions")
    for demo in demos:
        for required in [demo["entry"], "Reference/" + demo["id"] + ".png"]:
            if required not in files:
                raise ValueError("Missing demo input/reference: " + required)
    dependencies = json.loads((ROOT / "scripts/dependencies.json").read_text())
    profiles = json.loads((ROOT / "scripts/testkit/me-sources.json").read_text())
    for profile in profiles:
        release = next(item for item in dependencies if item["archive"] == "KK_Plugins_" + profile["tag"] + ".zip")
        inner_name = "KK_MaterialEditor_v" + profile["version"] + ".zip"
        release_data = read_zip(fetch(release, args.cache))
        me_files = read_zip(release_data[inner_name])
        reference = next(item for item in release["files"] if item.get("inner_archive") == inner_name)
        checked(me_files[reference["member"]], reference["sha256"], inner_name)
        source = fetch(profile, args.cache)
        with zipfile.ZipFile(io.BytesIO(source)) as archive:
            license_name = next(name for name in archive.namelist() if name.endswith("/LICENSE") and name.count("/") == 1)
            license_data = archive.read(license_name)
        install = dict(plugin)
        install.update(me_files)
        install["Licenses/MaterialEditor/LICENSE.txt"] = license_data
        install["Licenses/MaterialEditor/" + profile["archive"]] = source
        install["Licenses/MaterialEditor/SOURCE.txt"] = (
            "Unmodified official KK MaterialEditor " + profile["version"] + ".\n"
            "License: GPL-3.0. Corresponding source is included beside this file.\n"
            + profile["url"] + "\nSHA256 " + profile["sha256"] + "\n"
            "Binary release: " + release["url"] + "\n").encode("utf-8")
        instructions = """KK AssetImport {version} + MaterialEditor {me} 测试环境

只适用于 Windows 64 位 KK / Koikatu 与 CharaStudio，不适用于 KKS。
本安装包包含 AssetImport、它的 AssimpNet/LitJSON/原生 Assimp 库，以及官方 ME {me} 完整小包。

1. 完全退出游戏。备份旧插件、配置与测试卡片；推荐在游戏副本里测试。
2. 将原有 KK_AssetImport 和 KK_MaterialEditor 的旧副本移到 BepInEx/plugins 以外。
   只重命名 DLL 或挪到 plugins 子目录仍会重复加载。不要同时放入两版 ME。
3. 解压本包，将 BepInEx、runtimes 两个目录复制到含 Koikatu.exe 的游戏根目录并合并。
   不要直接把压缩包或整个外层测试包丢进 plugins；不要只复制一个 DLL。
4. 启动游戏后查看 BepInEx/LogOutput.log，确认 AssetImport {version}、MaterialEditor {me} 成功加载。
5. 测试步骤、19 个模型、参考效果和日志工具在外层测试包中；先打开 index.html。

已有游戏环境仍需：BepInEx 5.4.22+（5.x）、KKAPI 1.45.1+、LoadFileLimitedFix 的 KK 版。
两轮统一使用满足 ME 4.0.3 的前置：ResourceRedirector 1.2.0+、ExtendedSave/Sideloader 21.0+。
这些游戏环境前置没有随包捆绑；若启动报 missing dependency，请按日志具体 GUID 补齐。
ABMX、KKPE、DynamicBoneEditor 是可选联动，并非本套静态 demo 的基础依赖。

两套包的 KK_AssetImport DLL 完全相同，只替换 ME，用于对照测试。
本包是预览测试包；尚无本机 Windows 游戏内验证。插件能载入不代表模型、贴图与保存重载均通过。
LWS 在本版关联 LWO 的内存导入流程有已知问题，见外层指南。
Licenses/MaterialEditor 提供官方许可和对应源码，不需要复制到游戏目录。
""".format(version=version, me=profile["version"])
        install["安装说明.txt"] = instructions.replace("\n", "\r\n").encode("utf-8-sig")
        installer_name = "KK_AssetImport" + version + "_" + profile["profile"] + "_Packed.zip"
        files["Installers/" + installer_name] = zip_bytes(install)
        profile["installer"] = "Installers/" + installer_name
        profile["material_editor_sha256"] = reference["sha256"]
    checker = ROOT / "tools/AssetImport.DemoCheck/bin/Release/net462"
    checker_sources = ROOT / "tools/AssetImport.DemoCheck"
    for name in ["Program.cs", "App.config", "AssetImport.DemoCheck.csproj"]:
        if (checker_sources / name).stat().st_mtime > (checker / "AssetImport.DemoCheck.exe").stat().st_mtime:
            raise ValueError("Rebuild the model checker before packaging: " + name + " has changed")
    for name in ["AssetImport.DemoCheck.exe", "AssetImport.DemoCheck.exe.config", "AssimpNet.dll", "LitJSON.dll"]:
        files["Diagnostic/" + name] = (checker / name).read_bytes()
    for name in ["AssimpNet.dll", "LitJSON.dll"]:
        if files["Diagnostic/" + name] != plugin[PLUGIN + name]:
            raise ValueError("Checker uses a different managed dependency: " + name)
    files["Diagnostic/" + NATIVE] = plugin[NATIVE]
    for name in ["AssimpNet.txt", "Assimp.txt", "LitJson.txt"]:
        files["Diagnostic/licenses/" + name] = plugin[PLUGIN + "licenses/" + name]
    manifest = {
        "plugin_version": version,
        "status": "preview; Windows game validation pending",
        "profiles": profiles,
        "same_assetimport_binary_in_both_profiles": True,
        "assetimport_sha256": hashlib.sha256(plugin[PLUGIN + "KK_AssetImport.dll"]).hexdigest(),
        "scope": "19 static demos / 18 UI extensions; not all historical format features",
        "known_failures": [{"demo": "lws", "mode": "plugin-io", "reason": "Companion LWO missing from cache; incomplete skeleton placeholder"}],
        "sha256": {name: hashlib.sha256(data).hexdigest() for name, data in sorted(files.items())}
    }
    files["kit-manifest.json"] = (json.dumps(manifest, indent=2, ensure_ascii=False) + "\n").encode("utf-8")
    files["SHA256SUMS.txt"] = "".join(hashlib.sha256(data).hexdigest() + "  " + name + "\n" for name, data in sorted(files.items())).encode("utf-8")
    target = args.output / ("KK_AssetImport" + version + "_ME403_ME500_TestKit.zip")
    write_zip(target, {"KK_AssetImport_TestKit/" + name: data for name, data in files.items()})
    target.with_suffix(target.suffix + ".sha256").write_text(hashlib.sha256(target.read_bytes()).hexdigest() + "  " + target.name + "\n")
    print(target)
    print("{} files, {:.1f} MiB".format(len(files), target.stat().st_size / 1024 ** 2))


if __name__ == "__main__":
    main()
