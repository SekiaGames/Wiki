# War3 Asset Wiki（Godot 版）

只读资源预览器。数据源是仓库根目录的 `extract.pck`（模型 / 材质 / 动画 / 音效 / 贴图）和 `Extract/extract`
（表与文本）。不写入、不复制、不生成索引。

## 运行

**双击 `Run.command`**（macOS）或 `Run.bat`（Windows）：`dotnet build` → 启动 `Godot_mono`。

macOS 用 `GODOT_BIN` 指定 Godot 路径，默认找 `~/Applications/Godot_mono.app`。

## 资源来源

全部资源来自 `../extract.pck`，本工程不自带任何资源副本：

- `.tscn` 引用的是 `res://extract/.../X.glb`、`X.mat0.tres`，这些在 pck 里是**已导入的二进制**（`.scn` / `.ctex`）
- `res://assets/shaders/*.gdshader` 与 `res://assets/fallback/missing_texture.png` 也在 pck 里

启动时 `ProjectSettings.LoadResourcePack("../extract.pck")` 挂载；表与文本为了读到原文，直接走磁盘
`../Extract/extract`。pck 的导出预设 `Extract/export_presets.cfg` 必须带
`include_filter="extract/*,assets/*"`，只写 `extract/*` 会让材质引用的 shader 与 fallback 贴图缺席，
表现为 `Cannot load shader`、以及带粒子模型的 fx 子场景连坐加载失败。

## 结构

```
Wiki/
├─ Run.command / Run.bat     编译 → 启动
├─ Main.tscn                 只有一个根 Control + App.cs
└─ src/
   ├─ App.cs                 外壳：侧栏 / 顶栏 / 路由，另含 --shot / --inspect / --pack 调试入口
   ├─ Assets.cs              pck 挂载、文本与 json 读取、目录遍历、贴图与音效加载
   ├─ Data.cs                表与文本合并：单位 / 技能 / 装饰物 / 媒体索引 / 材质层透明度
   ├─ Ui.cs                  配色、字体、控件工厂
   ├─ Sidebar.cs             12 个分类导航 + 计数
   ├─ Page.cs                页面基类、页头、空状态、未接入分类
   ├─ OverviewPage.cs        概览
   ├─ ListPage.cs            卡片网格 + 过滤 + 分页加载
   ├─ DetailPage.cs          详情：3D 舞台 / 动画 / 技能 / 音效 / 属性 / 原始数据
   └─ ModelPreview.cs        SubViewport 预览：取景、拖动旋转、滚轮缩放、队伍色
```

## 数据口径

| 面板 | 来源 |
|---|---|
| 单位 | `Tables/UnitsunitUI.json` + `UnitsUnitData` / `UnitsUnitBalance` / `UnitsUnitAbilities` |
| 单位中文名 / 图标 | `Texts/*UnitStrings.json`（`<id>.Name`）、`Texts/*UnitFunc.json`（`<id>.Art`） |
| 技能 | `Tables/UnitsAbilityData.json` + `Texts/*AbilityStrings.json` / `*AbilityFunc.json` |
| 装饰物 | `Tables/DoodadsDoodads.json` + `Texts/UIWorldEditStrings.json`（`WorldEditStrings.<Name>`） |
| 模型 / 音效 / 贴图 | 递归扫描 `Extract/extract` 下的 `.glb` / `.wav` / `.png` |

表名与文本名都按**后缀**匹配（`UnitUI.json`、`UnitStrings.json`），不写死完整文件名——Extract 侧改前缀
（`MeleeV0` / `CustomV0` / `CustomV1`）时不会失效。文本合并顺序：无前缀 → `Melee` → `CustomV1` → `CustomV0`。

## 已知限制

1. **第二层贴图按 wc3.json 的 KMTA 判定**：材质里 `use_detail=true` 的第二层，只有该层 `KMTA` 轨道峰值 > 0 才显示。
   山丘之王的 `heromountainkingAvatar.png` 在游戏里靠 KMTA 全程 0 隐藏，直接 mix 会把模型洗成白色。
2. **TeamGlow 不显示**：`TeamGlow00.png` 的整壳加色层会盖住本体，预览里 `layer_alpha=0`；队伍色只作用于
   `use_team_color` 的布料/皮革材质。
3. **取景剔除超大面片**：TeamGlow 壳与部分建筑的施工面片比本体大数倍，取景按"剔除 TeamGlow 后可见网格"计算。
4. **动画直接播 `AnimationPlayer`**：`AnimationTree` 状态机被关掉（`Active=false`），不还原游戏里的状态切换。
5. **可破坏物 / 物品 / 升级 / 表格 / 文本** 仍是占位页。
6. 详情页的「原始数据」只做平铺 key-value，不做 json 折叠。

## 调试入口

```bash
Godot --path Wiki -- --shot=/tmp/a.png@units:Hmkg   # 截图并退出
Godot --path Wiki -- --inspect=Units/Human/Footman/Footman.tscn   # 打印网格 / 材质 / 动画 / 包围盒
Godot --path Wiki -- --pack   # 打印 pck 挂载状态、res:// 根目录、.godot/exported 条目数
```
