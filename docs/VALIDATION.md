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

## 4.1.4：大网格材质错配、外置贴图路径与插件元数据缓存

用户确认更新后模型可以加载，但两个房屋 demo 全白；复杂模型的问题发生在加载来自 KKS 的场景 PNG 时。日志记录两个 demo 导入成功，并在场景恢复时将 Body 的 97,848 个顶点拆成两块。随后用户提供原始 KKS 场景 PNG，本地提取其 AssetImport 文件/对象记录和 MaterialEditor 材质记录，确定存在拆分引起的名称错配。用户场景、内嵌 FBX、贴图和完整日志不纳入仓库或发行包。

- 新鲜 FBX/OBJ 等文件导入完成几何解析后，贴图路径以源模型目录解析，保留嵌套目录；失效的作者机器路径可匹配模型旁同名文件。不会递归搜索其他目录，也不会在场景/卡片恢复时重新读取作者机器的贴图。PNG/JPG/JPEG 后缀不区分大小写。
- 路径解析后的贴图在预载界面自动勾选，手动取消勾选仍会保留。增加 Texture ready/unavailable 日志；Material per Renderer 保持原有空白材质语义，并明确提示它会跳过源贴图。
- 13 项直接运行生产路径/勾选逻辑的回归通过，包括中文/空格路径、Windows 分隔符、同名游戏工作目录文件干扰、相对子目录、失效作者路径、手动取消勾选与内嵌纹理占位。测试不解码图片或运行 Unity。
- BepInEx 5.4.23.5 的 [TypeLoader.cs](https://github.com/BepInEx/BepInEx/blob/v5.4.23.5/BepInEx/Bootstrap/TypeLoader.cs) 只比较 DLL 修改时间来复用插件元数据。旧打包器跨版本使用同一时间，足以让新代码继续显示旧元数据；改用本次构建时间。需要可复现 ZIP 时设置每次发行独立的 SOURCE_DATE_EPOCH，不能跨版本复用。插件自身新增 Runtime build 与实际程序集版本日志。
- 场景使用 perRendererMaterials=true；FBX 由原生 Assimp 5.0.1 按 MakeLeftHanded|Triangulate 从内存读取，产生 16 个均名为 Body 的源网格。树为第 3 个网格、97,848 顶点，原场景材质键是 2_Body，后面的飘带是 3_Body。旧代码对树的每个 KK 拆分块分别领取唯一名称，第二块错误领取 3_Body，后续零件依次顺移，最后产生没有原始贴图记录的 16_Body。
- 将 renderer/材质的命名和创建移到源网格实例这一层，每块保留同一原始名称，不占用下一零件编号。MaterialEditor 4.0.3 与 5.0 的官方 MaterialAPI 按名称遍历全部匹配材质/renderer；因此分块能同时接收贴图、shader、renderer 属性及重命名，后面的材质副本源也不再错指。没有翻转 UV 或修改三角索引、分块算法。
- 将实际生产 BuildFromNode 分离到 partial 文件供测试直接编译调用；测试引擎替身只提供对象、材质和层级容器，不模拟 Unity 渲染。修复前测试失败于 source mesh 2 chunk 1: renderer became 3_Body, expected 2_Body；修复后本地读取原 FBX 的实际三角索引，调用生产 Partition 和 BuildFromNode，78 项检查通过；公开测试仅含数值摘要和合成三角面，不包含用户资产。检查覆盖 16 个源材质、17 个 renderer、树两块的同名绑定、后续铃铛材质副本源、静态/蒙皮/BlendShape 分支及重复节点实例。
- KK net35 / KKS net462 Release 编译通过，保留 2 条既有文件对话框弃用警告；几何 292,799 项、路径 13 项及 ME 4.0.3 / 5.0 / KKS 3.13.5 API 与 Hook 隔离检查通过。
- 复测应使用原始 KKS PNG，而非在错位版本里编辑重存的副本。材质编号缺陷已经代码修复并离线验证，Windows 游戏内最终显示、透明 shader、其他场景插件和保存重载仍待验证。
