# 资源预览 Wiki —— 设计方案

目标：一个**纯静态**浏览器站点，只读 `Godot/extract`，用于美术/策划预览资源、以及作为 Agent 与你协作时的共同事实来源（"这个模型叫什么、有哪些动画、技能图标是哪个"）。

不写资源、不建数据库、不改 converted 树。

---

## 1. 资产盘点（实测，2026-11-08）

| 类别 | 数量 | 体积 | 备注 |
|---|---|---|---|
| glb 模型 | 3264 | 72 MB | 中位 8 KB，最大 3.3 MB |
| png 贴图 | 6457 | 292 MB | 最大 0.75 MB，含 2184 张 CommandButtons 图标 |
| wav 音效 | 3877 | — | PCM 16bit 单声道 22050Hz，浏览器可直接解码 |
| json 表格/文本 | 3453 | — | `Tables/` 52 张 + `Texts/` 137 张 |
| tres 资源 | 9721 | — | 动画库 / 节点树 / 材质 |
| 合计 | — | 1.3 GB | |

模型目录（含 `.tscn` 的目录数）1600：

| 分类 | 目录 | 说明 |
|---|---|---|
| Units | 377 | 单位 836 行，835 行能对上模型 |
| Buildings | 163 | 建筑（独立顶层目录） |
| Doodads | 589 | 装饰物 |
| Abilities | 292 | 技能特效模型 |
| Objects | 70 | 出生点/背包等标记物 |
| Environment | 32 | 天空/火光 |
| UI | 76 | 界面元素 |
| SharedModels | 1 | |

`tscn` 总数 3133（每个模型另有一份 `_PORTRAIT`）。

### 1.1 数据来源对照表（wiki 各面板分别读哪里）

| 面板 | 数据源 |
|---|---|
| 单位中文名 / 提示 | `Texts/*UnitStrings.json` → 键 `Hamg.Name`、`Hamg.Ubertip` |
| 单位图标 / 弹道 / 按钮位 | `Texts/*UnitFunc.json` → 键 `Hblm.Art` = `ReplaceableTextures\CommandButtons\BTNHeroBloodElfPrince.blp` |
| 单位基础数值 | `Tables/UnitData.json`、`UnitBalance.json`、`UnitUI.json`（836 行 / 51 列） |
| 单位音效组 | `UnitUI.unitSound` + `Tables/UnitAckSounds.json`（1155 行）+ 单位目录内 wav |
| 技能数值 | `Tables/AbilityData.json`（803 行，3~4 级平铺列） |
| 技能图标 / 按钮位 / 施法动画名 | `Texts/*AbilityFunc.json` → `AHbz.Art`、`AHbz.Buttonpos`、`AHbz.Animnames` |
| 装饰物 | `Tables/Doodads.json` + `DestructableData.json` |
| 模型动画名 | `<Model>.wc3.json` 的 `sequences`（名称 + 起止帧）与 `<Model>_anims.tres` 的轨道 |

图标路径不能靠命名拼（`BTN`+id 只命中 1/836），必须走 `Texts/*Func.json` 的 `Art` 字段。

### 1.2 两个必须先说清的事实（决定架构）

1. **glb 里没有动画，也没有材质。** 实测 `Archnathid.glb`：`animations: 0`、`materials: 0`、`images: 0`、`skins: 1`。动画被拆到 `<Model>_anims.tres`（AnimationLibrary），材质拆到 `materials/NN_matN.tres`，geoset→材质映射写在 `.tscn` 的 `surface_material_override/N`。
2. **动画轨道只有三种**：`position_3d` 62134 / `rotation_3d` 32767 / `scale_3d` 9672，全部可以直接映射成 three.js 的 `VectorKeyframeTrack` / `QuaternionKeyframeTrack`，不需要处理 method / bezier / 粒子轨道。

结论：浏览器要出效果，必须自己把 `.tres + .tscn + glb` 组合起来。方案见 §3。

---

## 2. 站点形态

- 纯静态 HTML + 原生 ES Module，无打包器、无 node_modules 依赖（three.js 直接 vendor 到 `Wiki/vendor/`）。
- **必须走 http 服务**：`file://` 下 ES module 与 GLB fetch 都会被浏览器拦。提供 `Wiki/tools/serve.py`（`http.server`，根目录 `D:/WorldEditor`，端口 8123）。
- 资源用相对 URL 引用 `/Godot/extract/...`，**不拷贝、不复制 1.3 GB**，converted 重新解压后 wiki 自动跟着变。
- **不生成任何索引/缓存文件**：目录列表与文件读取都走 `serve.py` 的只读接口（`/api/asset/list`、`/api/asset/read`），
  浏览器内存里组装，`Wiki/` 只有源码。
- 路由用 hash（`#/units/Hmkg/skills`），刷新、分享、直达某个标签页都成立。

---

## 3. 动画/材质的两条路线

### 路线 A（推荐起步）：浏览器运行时解析 `.tres`

`Wiki/src/` 里写三个解析器：

| 模块 | 职责 | 难度 |
|---|---|---|
| `tres.js` | Godot 文本资源通用解析（`[ext_resource]` / `[sub_resource]` / `[resource]` / `PackedFloat32Array(...)`） | 中 |
| `anim.js` | `_anims.tres` → `AnimationClip[]`；`NodePath("Model/Skeleton3D:node22")` 取冒号后的节点名在 glb 里按名字找 | 低 |
| `mat.js` | `materials/*.tres` + `.tscn` → 每个 geoset 的贴图/透明度/双面/队伍色 | 低 |

- 优点：零构建产物，永远与 converted 同步；改资源不用重新生成任何东西。
- 代价：每个模型首次加载多读 1~3 个小文本文件（`_anims.tres` 中位 ~30 KB，大型英雄 ~300 KB）。
- 已知风险点（先做探针再全量）：轨道里的节点名与 glb 节点名是否 100% 对得上（`node22` 这类名字是导出器按索引生成的，理论上稳定）；`AnimationTree` 状态机不解析，只按动画名直接切。

### 路线 B（可选后置）：构建期烘焙成"网页专用 glb"

给 `GlbWriter`/导出器加一个 `--wiki-assets`，输出把动画＋材质＋贴图内嵌的 glb 到 `Wiki/assets/`。

- 优点：页面逻辑最简，加载最快，动画兼容性由 C# 侧保证。
- 代价：多一份约 300 MB 产物、每次 `extract.bat` 后必须重跑、要动编辑器导出代码（与"wiki 只读"的定位有摩擦）。

**建议**：先 A。若大装饰物页面卡顿或轨道对不上超过 5%，再上 B 兜底（两者可共存，wiki 侧只换一个 loader）。

---

## 4. 目录结构

```
D:/WorldEditor/
├─ Wiki/                       ← 新增，全部只读资源
│  ├─ index.html
│  ├─ src/
│  │  ├─ app.js                启动 / 全局渲染
│  │  ├─ router.js             hash 路由（分类 / 条目 / 标签页）
│  │  ├─ store.js              分类定义 + 过滤状态
│  │  ├─ api.js                唯一数据出口（P0 走 stub，P1 换成只读接口）
│  │  ├─ stub/data.js          打桩样例（真实 id / 中文名 / 图标路径）
│  │  ├─ godot/tres.js  anim.js  mat.js  tscn.js   ← P1 新增
│  │  ├─ ui/dom.js             元素构造、转圈、懒加载图片、toast
│  │  ├─ ui/sidebar.js  topbar.js  overview.js
│  │  ├─ ui/list.js            过滤条 + 卡片网格
│  │  ├─ ui/detail.js          3D 舞台 + 四个标签页
│  │  └─ ui/viewer3d.js        three.js 预览器 ← P1 新增
│  ├─ vendor/three/             three.module.js + GLTFLoader + OrbitControls（P1 加入）
│  └─ tools/
│     └─ serve.py              只读服务（静态 + /api/asset/list + /api/asset/read）
└─ Godot/extract/      ← 原样引用
```

---

## 5. 索引：运行时内存组装（不落盘）

静态服务器无法列目录，因此由 `serve.py` 提供两个只读接口：

| 接口 | 作用 |
|---|---|
| `GET /api/asset/list?path=&depth=&q=` | 列目录（递归深度可控），返回 name/path/dir/size |
| `GET /api/asset/read?path=` | 读文本类资源（.json 返回解析后的对象，.tres/.tscn/.txt 返回原文） |
| `GET /api/watch?dirs=` | SSE 推送文件变更（默认盯 `Wiki/` + `Tables` + `Texts`），用于热更新自动刷新 |

浏览器侧在内存里组装索引：

| 数据 | 来源 | 备注 |
|---|---|---|
| 单位列表 | `Tables/UnitUI.json` + `UnitData/Balance/Abilities` | 836 行，首次进入单位页读一次 |
| 中文名/说明/图标 | `Texts/*UnitStrings.json`、`Texts/*UnitFunc.json` | 137 个文本表，按需合并 |
| 技能 | `Tables/AbilityData.json` + `Texts/*AbilityFunc.json` | 含按钮位、施法动画名 |
| 模型/动画 | `<Model>_anims.tres` 的 `[sub_resource type="Animation"]` 名 | 详情页按需读 |
| 装饰物/可破坏物 | `Tables/Doodads.json`、`DestructableData.json` | P4 |

全部只读、无缓存文件；换台机器把 `Wiki/` 拷过去仍然能用。

---

## 6. 页面结构（对齐参考截图）

**左侧栏**：概览 / 单位（英雄·单位·建筑·召唤物）/ 技能 / 装饰物 / 可破坏物 / 物品 / 升级 / 模型库 / 音效 / 贴图 / 表格。

**顶部过滤条**（单位页）：种族（人族/兽族/不死/暗夜/中立/战役）、类型（英雄·单位·建筑·树木）、是否可训练、tileset、有无模型、名称搜索（中/英/id）、排序（id/HP/护甲）。

**卡片网格**：图标 + 中文名 + id + 两三个数值标签；只对可视区渲染，图标 `loading="lazy"`。

**详情页**（右栏/整页）：

1. 3D 预览：OrbitControls；自动按包围盒取景；动画下拉（Stand/Walk/Attack/Spell/Death…）+ 播放暂停 + 进度 + 速度；geoset 勾选显隐；线框；地面网格；队伍色色板；一键截图。
2. 技能面板：按 `Buttonpos` 排成 2×2/3×2 网格，图标 + 悬停显示 `Tip/Ubertip`；点技能 → 播放该技能的 `Animnames` 动画 + 播放施法音效。
3. 音效面板：按 What/Yes/Attack/Death 分组，点击即播。
4. 属性表：UnitData/UnitBalance/UnitUI 合并展示。
5. 原始数据：可折叠的 json 原文（给 Agent 和人核对用）。

---

## 7. 实施阶段

| 阶段 | 内容 | 验收 |
|---|---|---|
| **P0（已完成）** | 界面全量 + 全部打桩：`serve.py` 只读服务、概况页、单位/技能/装饰物列表（真实图标、真实中文名）、过滤与搜索、详情页 3D 舞台（转圈占位）、技能 2×2、音效列表、属性表、原始数据、深链路由 | 打开即有完整界面；所有交互给出「P1/P2/P3 接入」提示，不出现假数据冒充真数据 |
| **P1（已完成）** | `tres/anim/mat` 解析器 + `viewer3d` + `api.js` 全量换真实数据；vendor three.js | 单位列表 836 行、技能 803 行真实数据；山丘之王 27 条动画可播；材质按 add/blend/mul 还原；队伍色 9 档；wav 可播 |
| P2 | 两层贴图按 shader 语义合成（base + detail mix）、geoset 显隐（GEOA）、属性矩阵与排序、施法动画带音效 | 队伍色布料与游戏内观感一致 |
| P3 | 技能面板接 `AbilityFunc` + 音效 `<audio>` 播放 + 技能动画联动 | 山丘之王点「风暴之锤」播 Spell 动画+音效 |
| P4 | 装饰物 469 / 模型库 3264 / 音效 3877 / 贴图 6457 均已接入（列表 + 预览 + 分页）；可破坏物、物品、升级、表格、文本仍是空状态 | 每类都能列表 + 预览 |
| P5（可选） | 卡片 3D 缩略图队列（单 renderer 顺序渲染 + 浏览器内缓存）、中英切换、地图使用情况标注 | 网格卡片显示真 3D 缩略图 |

P0/P1 是全部价值所在，做完就能替代"翻目录看模型"。

---

## 8. 已知限制（先写清，避免后面当成 bug 查）

1. **粒子 / ribbon 不显示**：`PRE2/PREM/RIBB` 从未进入 glb（与编辑器当前渲染一致），火把、喷泉、传送门只有底面几何。
2. **geoset 默认全显**：血雾、尸体、Alternate 形态在编辑器里靠 GEOA/动画隐藏，wiki v1 一律显示，靠面板手动勾选规避。
3. **队伍色是近似**：Godot 侧是 shader 里按 uniform 混色，wiki 先用 tints 近似，不保证与游戏内完全一致。
4. **不做地图上下文**：只预览单模型，不铺地形、不做放置预览。
5. 最大的 glb 3.3 MB（团队色英雄/大建筑），首帧会有可感知卡顿，靠加载态提示缓解。

---

## 9. 已定的三条（2026-11-08 用户确认）

1. **动画方案 A**：浏览器运行时解析 `.tres`，只读 converted，不生成任何 wiki 产物文件。
2. **首期范围**：先做简单界面、功能全部打桩（P0 已按此完成）。
3. **卡片缩略图**：目标是真 3D 渲染 + 异步加载，未加载完成时用转圈 UI 占位；P0 先用真实图标 + 转圈，P5 换 3D。
4. **代码与 Godot 工程隔离**：所有前端代码在 `Wiki/`，不改 `Godot/` 下任何文件。
