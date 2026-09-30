# KK 移植验证记录

日期：2026-09-30（Asia/Shanghai）。基于上游提交 `7c5d61ebd5d1726add80007d52b77a2c6024bfed`，版本 4.1.0 preview。

## 已执行

- Apple Silicon macOS，.NET SDK 8.0.425：KK net35、KKS net462 Release 编译通过，0 errors。KK 有 2 条旧文件对话框 API 弃用警告。
- 将源码包解压至干净目录，重新从官方 Release 下载并校验编译引用，以 NuGet locked mode 还原后，两个目标再次编译通过。
- 几何回归通过：网格拆分、索引及通道映射、Morph 差值、4 骨骼权重、节点变换数学，共 292,799 个断言。测试使用生产 helper，不运行 Unity。
- 5 个缓存回归测试通过：OBJ/MTL、glTF 外置 buffer、内容寻址、去重、删除磁盘原件后的关联恢复。未验证 MessagePack 的实际游戏存档序列化。
- MaterialEditor 兼容测试通过：从 KK 5.0 / KKS 3.13.5 的真实 DLL 读取目标签名与 IL，调用生产补丁逻辑；每版本执行 5 个隔离指令片段用例，验证对象、参数、调用顺序、跳转标签及未知布局回退。
- 安装包校验通过：清单 SHA-256、仅含 4 个允许的运行库、托管 DLL 使用 CLR2 元数据、原生 Assimp 为 Windows x64 PE；没有把游戏程序集和编译参考 DLL 打入安装包。
- `git diff --check` 通过。

## 尚未执行

Windows KK/KKS 游戏运行、实际 Unity 渲染、完整 Harmony 安装、卡片/场景保存重载、可选插件组合均待游戏内测试。测试清单见 [KK-TESTING.md](KK-TESTING.md)。本记录不构成白模问题已修复的结论。

## 4.1.1 安装包整理

本次调整针对安装说明和目录布局；用户反馈是看不明白原预览包，并非已确认的加载失败。

- 对照原作者 `AssetImportv4.0.1Packed.zip`，改为游戏根目录中的 `BepInEx/plugins/AssetImport/` 与 `runtimes/win-x64/native/assimp.dll`。KK 加载路径同步改为 `Paths.GameRootPath` 下的该文件。
- KK net35、KKS net462 Release 重新编译通过，0 errors；仍有原来的 2 条文件对话框弃用警告。
- 实际生成并解压 `KK_AssetImportv4.1.1Packed.zip`：顶层只有 `BepInEx`、`runtimes`、`安装说明.txt`，4 个运行 DLL 的位置、文件清单和 SHA-256 均通过检查。中文说明采用 UTF-8 BOM 与 CRLF。
- 测试素材和手工验证说明拆入独立 `TestAssets.zip`；源码包包含新说明且排除了构建缓存。旧 `AssetImportKK` 目录的迁移步骤已写入安装说明，避免双份插件。
- 前述几何、缓存、MaterialEditor 检查为 4.1.0 的运行记录；此次未更改这些功能，未重复执行。Windows 游戏测试状态仍为未执行。

## 4.1.2：MaterialEditor 最低要求降至 4.0.3

- 使用官方 [KK_Plugins v270](https://github.com/IllusionMods/KK_Plugins/releases/tag/v270) 内的 KK MaterialEditor 4.0.3 作为编译引用；发行 ZIP 与 DLL 的 SHA-256 均锁定在 `scripts/dependencies.json`。v271 的 5.0 DLL 保留为单独测试引用。
- KK 的 `BepInDependency` 最低版本显式固定为 `4.0.3`，不再随引用库的 `PluginVersion` 自动提高；KKS 仍为 `3.13.5`。KK net35 与 KKS net462 Release 构建通过，0 errors、2 条既有文件对话框弃用警告。
- 扩展兼容检查读取真实编译产物：KK 的 MaterialEditor AssemblyRef 为 `4.0.3.0`，依赖属性为 `4.0.3`。对 4.0.3、5.0、KKS 3.13.5 均验证了 3 个类型引用、2 个纹理导入方法的完整签名和可见性，以及内联枚举值。
- 三版本的真实 `LoadData` 状态机分别为 `<LoadData>d__35`、`<LoadData>d__146`、`<LoadData>d__30`；各 5 个隔离执行用例、Hook 目标签名及未知布局回退均通过。测试构建与运行无警告。
- 安装说明与清单最低要求同步为 4.0.3；继续使用原项目式 Packed 目录。上述验证不运行 Windows 游戏，也不覆盖实际 Mono 程序集绑定、Unity 渲染或卡片/场景保存恢复，实机测试仍待执行。

## 4.1.3：用户 FBX 日志对应的节点转换修复

日期：2026-10-01（Asia/Shanghai）。用户提供游戏根目录 `output_log.txt`，实际加载 KK_AssetImport 4.1.1、ME 5.0、Unity 5.6.2、Windows x64。ASCII 与 binary 房屋 demo 都已完成 Walls/Roof/Chimney 的顶点、法线、三角面和 UV 转换，随后在 `ConvertTransform → BuildFromNode` 报 `TypeLoadException: Could not load type 'System.IO.InvalidDataException' from assembly 'KK_AssetImport'`。这是异常类型加载失败，不能解释为模型数据触发了普通的无效数据异常，也不是“FBX 完全没有解析”。完整用户日志不纳入仓库或发行包。

- 将几何 helper 的两个 `InvalidDataException` 引用替换为 CLR 2 核心库的 `ArgumentException`，保留非法变换与 BlendShape 数据校验，不改网格算法或 ME 接口。
- 新增针对编译 DLL 的回归：4.1.2 产物先因该类型引用而失败；4.1.3 编译后通过，并确认 KK 的 mscorlib 为 2.0。此检查针对已报告的问题，不宣称覆盖全部 Unity API。
- KK net35 / KKS net462 编译通过，保留两条既有文件对话框弃用警告；几何回归 292,799 个断言通过；KK ME 4.0.3 / 5.0 和 KKS 3.13.5 的 API / Hook 隔离回归通过。
- 日志收集器额外允许复制游戏根目录 `output_log.txt`，白名单从四项增至五项。在 macOS PowerShell 7.6.6 合成目录实际运行并检查：缺少 BepInEx 主日志时仍复制根目录日志，记录插件 4.1.3；不采集无关配置和卡片。Windows PowerShell 5.1 尚未实机运行。
- 提供完整 Packed 包与面向已有 4.1.1 / 4.1.2 安装的 DLL 小更新包；后者保留原 AssimpNet、LitJSON、原生库和 ME。相同 demo 可继续复测，无需再次下载素材。
- 这是针对已捕获错误的代码修复；尚无 4.1.3 游戏成功截图或保存重载验证，不将编译和纯托管测试当作实机通过。LWS 缓存限制与原 zipmod 服装卡白模问题未在本次修复。
