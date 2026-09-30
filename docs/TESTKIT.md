# 双 MaterialEditor 测试包

`testkit/` 保存用户指南、19 个静态样例、参考图、日志收集器和待填写 CSV。它覆盖 AssetImport 文件选择器的 18 种扩展名，FBX ASCII / binary 分开测试。骨骼、动画、BlendShape、复杂 shader 与格式的全部历史版本不在这组基础 smoke test 的通过声明内。

两套安装环境使用同一个 KK AssetImport 4.1.4，分别包含官方 KK MaterialEditor 4.0.3（v270）和 5.0（v271）完整小包。这使用户对照只改变 ME，不需要维护功能相同的两份 AssetImport 实现。ME 的 GPL 许可与对应版本源码随各自安装包提供。第三方模型来自 Assimp v5.0.1 的 `test/models`，保留许可和 IFC 额外署名；自建房屋、DXF、UV 测试图的来源见 `testkit/Sources`。

## 构建

按主 README 准备依赖并构建 KK 插件，然后执行：

```sh
dotnet build tools/AssetImport.DemoCheck/AssetImport.DemoCheck.csproj -c Release
python3 scripts/package.py --output artifacts
python3 scripts/package_testkit.py --plugin-package artifacts/KK_AssetImportv4.1.4Packed.zip --output artifacts
```

打包器复核插件 manifest、官方 ME 发布包与源码 SHA-256，并在 `.build-downloads` 缓存缺失的官方下载。源码版本锁定在 `scripts/testkit/me-sources.json`；二进制锁定在 `scripts/dependencies.json`。不打包游戏 DLL、本地日志、卡片或构建引用。Windows 用户只需解压总包、打开 `index.html`；不要把总包直接安装进游戏。

## 重建参考素材

已有模型和图已纳入版本管理，普通打包无需重建。若需要重新生成自建房屋：

```sh
python3 scripts/testkit/generate_common.py --assimp /path/to/assimp-5.0.1-cli --output /path/to/empty-folder
```

该生成器额外产生一个 GLB 实验文件；它不属于当前 UI 的 18 种入口，本包没有纳入该例。不要将它当作已验证支持。二进制 FBX 通过同版本 Assimp 导出，不是将 ASCII 文件改后缀。

使用源版本 Assimp 5.0.1（commit `8f0c6b04b2257a520aaab38421b2e090204b69df`）构建本机原生库后，可运行检查器的 net8.0 目标：

```sh
dotnet tools/AssetImport.DemoCheck/bin/Release/net8.0/AssetImport.DemoCheck.dll \
  --kit testkit --native /path/to/native-assimp-library \
  --report artifacts/native-check --scene-output artifacts/reference-scenes
python3 scripts/testkit/render_references.py --scenes artifacts/reference-scenes
```

渲染脚本需要 Pillow、numpy 和中文字体，可用 `--font` 指定字体。它对真实导入场景应用节点变换、UV 和纹理，用深度缓冲生成参考图。图中亮暗、镜头、材质高光不承诺与 Unity 一致，SMD 参考为方便识别采用双面显示。每张图明确标注“非游戏截图”；LWS 额外标明当前失败。

## 本次验证与限制

- 检查器 net462 / net8.0 构建通过，无警告。Windows 包中的 exe 使用 .NET Framework 4.6.2+，加载与插件完全相同的托管 / Windows x64 原生依赖。
- macOS ARM64 上从官方 Assimp v5.0.1 源码构建原生库，保持解析器源码不变、链接系统 zlib。两种 IO 模式合计 38 项，37 PASS；唯一 FAIL 为 LWS `plugin-io`。两种 FBX 的两条路径均通过。公开记录见 `testkit/Validation/native-preflight-macos.json`。
- LWS 的直接文件读取返回真实立方体，内存读取丢失 LWO 后生成 `SkeletonMaterial` 占位骨架并标记 `SceneFlags.Incomplete`。检查器拒绝这一假阳性；此处只暴露和记录缺陷，没有更改插件的缓存实现。
- 全部贴图严格解码通过。上游 AC 示例原 JPEG 不可解码，已换为自建 CC0 经纬 UV 测试图，并保留修改和来源记录。
- 19 张参考图和总览经视觉核对，离线参考页链接有效。CSV 的 76 行覆盖两版 ME × Studio/Maker × 19 例，初始状态仍是未测。
- PowerShell 收集器在 macOS PowerShell 7.6.6 的合成游戏目录实际运行，验证五文件白名单（含游戏根目录 output_log.txt）、插件版本识别、重复 ME 提示、缺失日志和不收集其他配置/卡片。按 Windows PowerShell 5.1 语法编写，但没有 Windows PS5.1 / CMD 实机执行结果。
- 本机未运行 Windows 原生 DLL、Unity/Mono 或游戏。2026-10-01，用户确认 4.1.4 已解决本次 KKS 场景在 KK 中的材质串位；这不是 19 个样例的完整测试矩阵，ME 编辑和保存重载仍需验证。原来的 zipmod 服装卡白模问题没有本轮已解决的结论。
