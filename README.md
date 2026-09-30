# AssetImport — KK port preview 4.1.4

AssetImport 将外部 3D 模型导入 Koikatsu（KK）或 Koikatsu Sunshine（KKS），用作角色饰品或 Studio 物体。本仓库基于 [Njaecha/AssetImport](https://github.com/Njaecha/AssetImport)，增加 KK 支持，并保留 KKS 构建目标。

**KK 用户请下载 `KK_AssetImportv4.1.4Packed.zip`。** 这是供 Windows KK 使用的预览版，不适用于 KKS；源码包和测试素材包不用于安装插件。

4.1.4 修复大网格拆分后材质编号顺移的问题：原零件拆成多块时保留同一材质/renderer 名称，避免加载 KKS 场景时树、飘带、风筝等贴图串位。另为新导入模型解析同目录/相对子目录的贴图，并修正安装包时间戳导致 BepInEx 缓存旧版本的问题，启动时记录真实 Runtime build。包含 4.1.3 的节点转换类型修复。

已装完整 4.1.1 / 4.1.2 / 4.1.3 的用户可用 `KK_AssetImportv4.1.4_DLL_Update.zip`，退出游戏后只覆盖其中 DLL，保留 ME 与运行库。复测场景请重开原始 KKS 场景 PNG；在错位版本里重新保存过的场景可能已有错误编辑，不能自动修复这些记录。新导入房屋 demo 时关闭 Material per Renderer；该选项会跳过源材质/贴图。

**2026-10-01：用户确认 4.1.4 已解决本次 KKS 场景在 KK 中的材质串位问题。** 该结果对应本次报告的场景；完整格式矩阵、保存重载及其他跨游戏 shader / 场景插件组合仍待验证。详细记录见 [验证记录](docs/VALIDATION.md)。

## 安装 KK 预览版

需要 Windows x64 的 KK，以及以下插件：

- BepInEx 5.4.22 或更新的 5.x 版本。
- KKAPI 1.45.1+。
- KK MaterialEditor 4.0.3+。
- LoadFileLimitedFix（IllusionFixes 的 KK 版本）。

**使用 MaterialEditor 4.0.3 的用户请换用本次 4.1.4 包。** 旧的 4.1.0 / 4.1.1 包要求 MaterialEditor 5.0；最低要求继续保持 4.0.3（从 4.1.2 起支持）。MaterialEditor 4.0.3 来自 [KK_Plugins 官方 v270 发布](https://github.com/IllusionMods/KK_Plugins/releases/tag/v270)。

1. 退出游戏，打开游戏根目录，即 `Koikatu.exe` 所在的文件夹。
2. 解压 `KK_AssetImportv4.1.4Packed.zip`，把里面的 **`BepInEx` 和 `runtimes` 两个文件夹一起**放入游戏根目录，合并同名文件夹并覆盖本插件的同名文件。不要只复制 DLL，也不要把 ZIP 放进 `mods`。
3. 启动角色编辑器或 Studio，按 **左 Alt + I** 打开导入窗口。角色编辑器中先选一个已有饰品，再将模型导入该槽位。

安装后的目录与原项目打包方式一致：

```text
游戏根目录/
├── Koikatu.exe
├── BepInEx/plugins/AssetImport/
│   ├── KK_AssetImport.dll
│   ├── AssimpNet.dll
│   ├── LitJSON.dll
│   └── licenses/
└── runtimes/win-x64/native/assimp.dll
```

**从 4.1.0 预览版升级：**先退出游戏，将旧的 `BepInEx/plugins/AssetImportKK` 整个文件夹移出游戏目录，再安装新包，避免同时加载两份插件。仅在 `plugins` 内改文件夹名不能停用旧版。如果曾在 KK 中误装 KKS 版，先将那一版的 `AssetImport` 插件文件夹移出游戏目录；不要删除整个 `BepInEx` 或 `runtimes` 文件夹。

**从 4.1.1 / 4.1.2 / 4.1.3 升级：**退出游戏后，按上面的安装步骤直接覆盖即可，目录结构相同。

KK 包使用的模型解析库与原 KKS 包不同，所以没有 `IndexRange.dll` 和 `System.Resources.ResourceManager.dll`，也不需要从 KKS 包补入它们。简短说明见 [安装说明](docs/INSTALL-KK.txt)，安装包根目录也附有 `安装说明.txt`。

ABMX 5.4、KKPE 2.21.5、DynamicBoneEditor 1.1 属于可选兼容项。安装它们时使用 KK 版本，分别测试后再组合使用。安装包不包含这些插件或游戏 DLL。

## Windows 游戏内验证

完整对照包为 `KK_AssetImport4.1.4_ME403_ME500_TestKit.zip`：解压后打开 `index.html`，选择随包的 ME 4.0.3 或 5.0 安装环境，再测试 19 个模型（覆盖文件选择器全部 18 种扩展名，FBX 分 ASCII / binary）。两套使用同一个 AssetImport DLL，便于隔离 ME 版本差异。包内有参考图、76 项结果表、模型预检和日志收集脚本；ME 官方许可和对应源码随安装包提供。参考图为离线渲染，不是游戏测试通过的截图。构建步骤见 [测试包维护说明](docs/TESTKIT.md)。

**已知限制：**4.1.4 的 LWS 内存导入未缓存配套 LWO，可能返回占位骨架而不是场景几何。预检会把此项标为失败；直接读取 LWO 成功不代表 LWS 通过。原生解析预检的其余 37 项通过也不替代 Windows 游戏内显示、ME 编辑和保存重载测试。

测试素材单独放在 `KK_AssetImportv4.1.4TestAssets.zip`，内含 `TestAssets/` 和 `Windows测试说明.md`，可解压到任意方便的位置。正常安装插件不需要这个包。

4.1.4 基于 MaterialEditor 4.0.3 编译，并保留 5.0 的兼容回归检查。本次场景材质修复已获用户实测确认，其余验证范围见 [Windows 测试说明](docs/KK-TESTING.md)。目前没有 KKS 服装白模已解决的实机验证结论，也不承诺所有 KK/KKS 角色卡、服装卡或场景可以互通。

## 在 macOS 上构建

安装 **.NET SDK 8** 和 **Python 3.9+**，在仓库根目录运行：

```sh
python3 scripts/prepare_dependencies.py

dotnet build src/AssetImport/AssetImport.csproj -c Release

python3 scripts/package.py --output artifacts
```

不需要安装 Unity 编辑器。C# 编译可在 macOS、Windows 或 Linux 进行；游戏、Windows 原生 Assimp 库和插件联动仍需在 Windows KK 中验证。

工程构建两个目标：

| 目标 | 框架 | 输出 |
| --- | --- | --- |
| KK | .NET Framework 3.5 | `src/AssetImport/bin/Release/net35/KK_AssetImport.dll` |
| KKS | .NET Framework 4.6.2 | `src/AssetImport/bin/Release/net462/KKS_AssetImport.dll` |

本轮打包脚本只生成 KK 安装包。KKS 保留源码与编译验证，不提供新的独立 KKS 发行包；不要把单独编译出的 KKS DLL 当作完整安装包。

依赖主要来自两个官方 NuGet 源：[nuget.org](https://api.nuget.org/v3/index.json) 和 [IllusionMods](https://pkgs.dev.azure.com/IllusionMods/Nuget/_packaging/IllusionMods/nuget/v3/index.json)。NuGet 配置同时提供 [BepInEx 官方源](https://nuget.bepinex.dev/v3/index.json)。准备脚本另从对应插件的官方 GitHub Releases 获取编译引用，并从 BepInEx 官方 Release 恢复已无法从源获取的分析器包。所有这些下载均以 SHA-256 锁定；NuGet 依赖版本记录在 `packages.lock.json`。

独立回归检查：

```sh
dotnet run --project tests/AssetImport.Cache.Tests -c Release
dotnet run --project tests/AssetImport.Geometry.Tests -c Release
dotnet run --project tests/AssetImport.Hierarchy.Tests -c Release
dotnet run --project tests/AssetImport.Textures.Tests -c Release
dotnet run --project tests/AssetImport.Compatibility.Tests -c Release
```

这些检查覆盖缓存、几何转换、生产层级构建的拆分材质绑定、贴图路径和真实 MaterialEditor DLL 的 IL 布局；兼容性测试调用生产补丁逻辑并执行隔离的指令片段。它们不运行游戏，不能验证 Unity 渲染、游戏保存恢复或整个 Harmony 运行环境。

## KK 适配差异

- Unity 5.6 使用 16 位网格索引，大网格拆分为最多 65,535 顶点的多个网格，并同步映射 UV、骨骼权重及 BlendShape。
- 每顶点保留权重最高的 4 根骨骼并重新归一化；超过 4 根骨骼影响的模型可能与 KKS 外观不同。
- BlendShape 差值在 CPU 上计算，避免加载 Unity 2019 制作的计算着色器资源包。
- KK 使用 AssimpNet `5.0.0-beta1` 与其配套的原生 Assimp `5.0.1`；FBX、glTF 等格式的结果可能与 KKS 使用的 Assimp 6 不同。
- 节点本身带显式剪切变换时会警告并近似为 Unity 的位置、旋转、缩放；建议先在建模软件烘焙这类变换。普通父子变换层级保留。
- 如果其他插件已加载不同路径的 Assimp 原生库，KK AssetImport 会记录错误并停止初始化，不会强行替换已加载的库。

KK 安装包包含 `KK_AssetImport.dll`、AssimpNet、LitJSON 0.19 和配套 Windows x64 原生库，许可证位于插件文件夹的 `licenses/`。依赖清单和 SHA-256 校验文件放在安装 ZIP 外，供核验使用。

## 来源与致谢

- 原插件：[Njaecha/AssetImport](https://github.com/Njaecha/AssetImport)。
- 模型解析：[Assimp](https://assimp.org/)；KK 使用 [AssimpNet 5.0.0-beta1](https://www.nuget.org/packages/AssimpNet/5.0.0-beta1)。
- KKS 保留原作者的 [AssimpNetter .NET 4.6.2 backport](https://github.com/Njaecha/AssimpNetter)，源自 [Saalvage/AssimpNetter](https://github.com/Saalvage/AssimpNetter)。
- 游戏 API 和相关插件：[IllusionMods](https://github.com/IllusionMods)。
