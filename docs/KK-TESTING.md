# KK 预览版 4.1.4：Windows 测试

此文档是**待执行的手工验证清单**，不表示游戏内测试已经通过。请使用备份卡片、场景或单独的测试存档。当前任务只验证 KK 移植，不包含 KKS 服装白模的复现或修复结论。

## 1. 安装与启动

准备 Windows x64 KK、BepInEx 5.4.22+（5.x）、KKAPI 1.45.1+、KK MaterialEditor 4.0.3+、LoadFileLimitedFix。LoadFileLimitedFix 可从 [IllusionFixes Releases](https://github.com/IllusionMods/IllusionFixes/releases) 的 KK 包获取。不要把 KKS 版依赖装入 KK。

4.1.4 基于 [官方 MaterialEditor 4.0.3（v270）](https://github.com/IllusionMods/KK_Plugins/releases/tag/v270) 编译，并保留 5.0 的兼容回归检查；Windows 实机仍待测试。建议分别使用 4.0.3 和 5.0 执行下方清单并记录实际版本。旧 AssetImport 4.1.0 / 4.1.1 包要求 MaterialEditor 5.0，测试 4.0.3 时须换用本次 4.1.4 包。

退出游戏后，将 `KK_AssetImportv4.1.4Packed.zip` 内的 `BepInEx` 和 `runtimes` 两个文件夹一起解压至游戏根目录（`Koikatu.exe` 所在处），合并同名文件夹。4.1.1 用户可直接覆盖。若安装过 4.1.0 预览版，先将旧 `BepInEx/plugins/AssetImportKK` 整个文件夹移出游戏目录，避免双份加载。检查目录结构：

```text
游戏根目录/
├── BepInEx/plugins/AssetImport/
│   ├── KK_AssetImport.dll
│   ├── AssimpNet.dll
│   ├── LitJSON.dll
│   └── licenses/
└── runtimes/win-x64/native/assimp.dll
```

首次测试暂不启用其他 Assimp 模型导入插件。若日志提示另一插件已经加载不同路径的 Assimp 库，退出游戏、调整插件组合后重启；AssetImport 会停止初始化，不会替换已经加载的库。安装文件覆盖与运行时加载是两回事：根目录 `runtimes/win-x64/native/assimp.dll` 若供其他插件共用，应先保留其备份。

启动 KK 角色编辑器和 Studio，分别确认日志加载 `KK_AssetImport 4.1.4`，左 Alt + I 能打开窗口。如未加载，先查看 `BepInEx/LogOutput.log` 的缺失依赖或原生库错误。

ABMX 5.4、KKPE 2.21.5、DynamicBoneEditor 1.1 为可选兼容项，先完成基础导入，再按第 6 节组合测试。

## 2. 小模型与纹理

将单独的 `KK_AssetImportv4.1.4TestAssets.zip` 解压到任意方便的位置，内含本说明和 `TestAssets/`。`TestAssets/`（仓库中为 `tests/fixtures/`）含 `textured-cube.obj`、同名 `.mtl` 和 `fixture.png`；保持三者放在同一文件夹。这是一个 1 单位边长的立方体，每面使用相同的四彩棋盘纹理。测试素材不包含在插件安装包中。

- [ ] **角色编辑器：**选择一个已有饰品槽位，打开导入窗口，选择 OBJ。建议缩放设为 0.1；这个模型没有骨骼或 BlendShape。
- [ ] 在纹理预览页确认 Diffuse 路径指向 `fixture.png`；若临时导入路径无法找到纹理，用“Common”或文件按钮定位原始 fixture 文件夹，再点 Finish。
- [ ] 检查立方体形状、四种颜色、棋盘边界和 UV；旋转观察各面，确认没有缺面或全部变成纯色。
- [ ] **Studio：**重复导入，检查物体树、可见性、移动、旋转和缩放。建议缩放设为 1。
- [ ] 分别打开、关闭“Material per Renderer”，确认同一素材能完成导入。

## 3. 大模型

使用一个确实包含**单个超过 65,535 顶点网格**的模型；多个小网格组成的大文件不足以验证拆分。

- [ ] 在角色编辑器和 Studio 导入，确认全部几何出现，没有截断、裂口或材质错位。
- [ ] 使用有 UV 的模型观察拆分边界；有骨骼、BlendShape 的大模型还需完成下一节。
- [ ] 记录源模型的网格数、最大网格顶点数、拆分后的表现与导入时间。

## 4. 蒙皮、BlendShape 与格式

- [ ] 导入带骨骼模型，检查骨骼层级、姿态和动态骨骼链。
- [ ] 导入每顶点超过 4 根骨骼影响的样例，确认保留最高 4 个权重并归一化后的姿态可接受。
- [ ] 导入带多个 BlendShape 的模型，逐个改变权重，检查位移、法线表现及拆分网格的一致性。
- [ ] 分别验证实际使用的 OBJ、FBX、glTF。glTF 外置 `.bin` 文件应和模型按原始相对路径放置。
- [ ] 比较已知参考效果，记录 Assimp 5 与 Assimp 6 的 FBX 变换、材质、骨骼或 glTF 差异。

KK 的 BlendShape 差值使用 CPU 计算，首轮导入速度可能与 KKS 不同。不要仅以“没有异常日志”判定形变正确。

## 5. 保存与重开

使用前面已经确认外观正确的模型。测试纹理加载并点 Finish 后再保存。

- [ ] 角色卡：保存，退出游戏，重启后载入；检查模型、材质、缩放、骨骼和 BlendShape。
- [ ] 服装卡：保存包含导入饰品的服装，切换服装后重新载入；检查槽位和关联数据。
- [ ] Studio 场景：保存、退出、重开；验证复制物体、导入另一场景后的结果。
- [ ] 退出游戏后将原始模型文件夹临时改名，再重新打开保存内容，确认内嵌模型和已保存材质能恢复；随后恢复文件夹名称。
- [ ] 测试饰品槽位复制、转移、替换以及跨服装复制，确认没有遗留或错位模型。

这些测试仅在同一游戏 KK 内进行；本预览版不承诺 KK/KKS 卡片或场景互通。

## 6. 可选插件组合

在基础测试通过后，分别启用 KKABMX 5.4、KKPE 2.21.5、KK DynamicBoneEditor 1.1，再测试同时启用的组合。使用更新版本时记录实际版本。

- [ ] ABMX：编辑、刷新骨骼后导入模型，再保存重开。
- [ ] KKPE：Studio 中刷新骨骼列表、编辑姿态和 BlendShape，再复制物体及重开场景。
- [ ] DynamicBoneEditor：编辑导入模型的动态骨骼，切换服装、载入服装卡及重开角色卡。
- [ ] 同时启用三个可选插件后，重复一轮基础导入和保存恢复。

## 报告结果

每个失败用例记录：游戏环境、必需和可选插件版本、使用的模型格式、网格/顶点/骨骼/BlendShape 信息、操作步骤、预期结果和实际结果。保留当次 `BepInEx/LogOutput.log`，同时提供能够复现问题的模型及其关联文件。可用下表记录：

| 用例 | Maker / Studio | 插件组合 | 结果 | 日志或截图 |
| --- | --- | --- | --- | --- |
| OBJ + 纹理 | 待测 | 基础依赖 | 未执行 | |
| 单网格超过 65,535 顶点 | 待测 | 基础依赖 | 未执行 | |
| 蒙皮 + BlendShape | 待测 | 基础依赖 | 未执行 | |
| 保存、退出、重开 | 待测 | 基础依赖 | 未执行 | |
| 可选插件组合 | 待测 | 逐个及组合 | 未执行 | |
