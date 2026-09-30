# AssetImport — KK port preview 4.1.0

AssetImport 将外部 3D 模型导入 Koikatsu（KK）或 Koikatsu Sunshine（KKS），用作角色饰品或 Studio 物体。本仓库基于 [Njaecha/AssetImport](https://github.com/Njaecha/AssetImport)，增加 KK 支持，并保留 KKS 构建目标。

**当前是供 Windows 游戏内验证的 KK 预览版。** 自动化检查和编译不能替代游戏测试；待验证项目见 [KK 安装与测试说明](docs/KK-TESTING.md)。本轮不处理服装白模定位，也不承诺 KK/KKS 角色卡、服装卡或场景可以互通。

## 安装 KK 预览版

需要 Windows x64 的 KK，以及以下插件：

- BepInEx 5.4.22 或更新的 5.x 版本。
- KKAPI 1.45.1+。
- KK MaterialEditor 5.0+。
- LoadFileLimitedFix（IllusionFixes 的 KK 版本）。

将 KK 安装包解压到游戏根目录。插件及自带库应位于 `BepInEx/plugins/AssetImportKK/`；保留包内 `runtimes/win-x64/native/assimp.dll` 的相对目录。进入角色编辑器或 Studio 后，按 **左 Alt + I** 打开导入窗口。角色编辑器中先选一个已有饰品，再将模型导入该槽位。

ABMX 5.4、KKPE 2.21.5、DynamicBoneEditor 1.1 属于可选兼容项。安装它们时使用 KK 版本，分别测试后再组合使用。安装包不包含这些插件或游戏 DLL。

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
dotnet run --project tests/AssetImport.Compatibility.Tests -c Release
```

这些检查覆盖缓存、几何转换和真实 MaterialEditor DLL 的 IL 布局；兼容性测试调用生产补丁逻辑并执行隔离的指令片段。它们不运行游戏，不能验证 Unity 渲染、游戏保存恢复或整个 Harmony 运行环境。

## KK 适配差异

- Unity 5.6 使用 16 位网格索引，大网格拆分为最多 65,535 顶点的多个网格，并同步映射 UV、骨骼权重及 BlendShape。
- 每顶点保留权重最高的 4 根骨骼并重新归一化；超过 4 根骨骼影响的模型可能与 KKS 外观不同。
- BlendShape 差值在 CPU 上计算，避免加载 Unity 2019 制作的计算着色器资源包。
- KK 使用 AssimpNet `5.0.0-beta1` 与其配套的原生 Assimp `5.0.1`；FBX、glTF 等格式的结果可能与 KKS 使用的 Assimp 6 不同。
- 节点本身带显式剪切变换时会警告并近似为 Unity 的位置、旋转、缩放；建议先在建模软件烘焙这类变换。普通父子变换层级保留。
- 如果其他插件已加载不同路径的 Assimp 原生库，KK AssetImport 会记录错误并停止初始化，不会强行替换已加载的库。

KK 安装包包含 `KK_AssetImport.dll`、AssimpNet、LitJSON 0.19 和配套 Windows x64 原生库。依赖声明和许可随包保留。

## 来源与致谢

- 原插件：[Njaecha/AssetImport](https://github.com/Njaecha/AssetImport)。
- 模型解析：[Assimp](https://assimp.org/)；KK 使用 [AssimpNet 5.0.0-beta1](https://www.nuget.org/packages/AssimpNet/5.0.0-beta1)。
- KKS 保留原作者的 [AssimpNetter .NET 4.6.2 backport](https://github.com/Njaecha/AssimpNetter)，源自 [Saalvage/AssimpNetter](https://github.com/Saalvage/AssimpNetter)。
- 游戏 API 和相关插件：[IllusionMods](https://github.com/IllusionMods)。
