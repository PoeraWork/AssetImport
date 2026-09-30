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
