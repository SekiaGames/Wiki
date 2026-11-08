# 资源预览 Wiki

只读的 War3 资源档案馆，用浏览器打开。资源全部来自 `../Godot/extract`，不复制、不写入、不生成索引文件。

当前进度：**P0 界面全量 + 功能打桩**。设计细节见 `PLAN.md`。

## 运行

**双击 `Wiki/Run.bat`** —— 启动只读服务并自动打开默认浏览器，Ctrl+C 停止。

```bat
Run.bat                 :: 8123 端口 + 打开浏览器
Run.bat 9000            :: 自定义端口
Run.bat -nobrowser      :: 只起服务，不开浏览器（自动化用）
```

也可以手动起服务：

```bash
python Wiki/tools/serve.py            # 默认 127.0.0.1:8123
python Wiki/tools/serve.py --port 9000
```

打开 <http://127.0.0.1:8123/Wiki/index.html>

不能双击 `index.html` 打开 —— `file://` 下 ES module 与资源请求都会被浏览器拦，所以必须走 `Run.bat`（或上面的命令）。

## 开机自启

**双击 `Wiki/AutoStart.bat`**，出现数字菜单：

```
==========================================================
  Asset Wiki - autostart
==========================================================
[ON ] Autostart enabled          ← 当前状态自动显示
      link   : ...\Startup\WorldEditor Asset Wiki.lnk
      target : D:\WorldEditor\Wiki\Run.bat
      args   : 

   1   Enable autostart   (start the wiki at logon)
   2   Disable autostart
   3   Refresh status
   0   Exit

Choose 1/2/3/0 :
```

- 选 `1` 后会再问一句 `Open the wiki in the browser at logon? [Y/n]`：回车 = 登录时自动打开浏览器，输入 `n` = 只起服务（窗口最小化）
- 文件只操作启动文件夹里的那一个快捷方式，不写注册表、不动其他启动项
- 检查完按 `0` 或直接回车退出

快捷方式位置：`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\WorldEditor Asset Wiki.lnk`

## 热更新

页面默认订阅 `/api/watch`（Server-Sent Events）。服务端每 0.6 秒比对一次文件快照：

| 监听范围 | 说明 |
|---|---|
| `Wiki/` 整棵树 | 改前端代码 / 样式后自动刷新 |
| `converted/Tables`、`converted/Texts` | 重新导出表或文本后自动刷新 |
| 额外目录 | `/api/watch?dirs=Units/Human` 可临时加（P1 详情页会用） |

顶栏右侧有状态标签：`热更新 · 已连接`（青点）/ `重连中` / `关`。
加 `?live=0` 可临时关闭（headless 截图时需要，否则长连接会让 `--virtual-time-budget` 卡住）。

## 已实现

| 区域 | 状态 |
|---|---|
| 左侧导航（12 个分类，带真实计数） | 完整 |
| 概览页 | 完整 |
| 单位列表 | 836 行真实数据（UnitUI + UnitData + UnitBalance + UnitAbilities + Texts 本地化） |
| 技能列表 | 803 行真实数据（AbilityData + AbilityFunc 的 Art/Buttonpos/Animnames） |
| 装饰物列表 | 469 行真实数据（Doodads.json + `Texts/WorldEditStrings.json` 的中文名），按 环境/物件/结构/水面/悬崖/特殊 过滤 |
| 装饰物预览 | 静态模型 + 变体切换（如蘑菇 4 个变体），无动画时自动隐藏动画控件 |
| 模型库 | 3264 个 glb（含 portrait），按 单位/建筑/装饰物/技能/贴图外的目录聚类，详情可直接 3D 预览 + geoset 面板 |
| 音效 | 3877 个 wav，卡片带试听按钮，详情页原生播放器 |
| 贴图 | 6457 张 png，缩略图网格（棋盘格底），详情页大图 + 尺寸，小于 96px 的图标自动放大并像素化 |
| 过滤：种族 chips / 类型下拉 / 搜索 | 作用于真实数据 |
| 卡片网格 | 真实图标 png（840/840 命中）+ 生命/护甲/移速 |
| 详情页 3D 预览 | three.js 加载 glb，按 `.tscn` 挂 `materials/*.tres`；两层贴图按 shader 语义 mix；add/blend/mul 三种混合模式 |
| 动画 | 运行时解析 `_anims.tres`，全部动画可播 + `（绑定姿势）` |
| 队伍色 | 中立 + 8 种队伍色切换 |
| geoset 面板 | 8~10 个 geoset 逐个勾选显隐，带顶点数与尺寸 |
| 音效 | 目录内 wav 直接 `<audio>` 播放，按 What/Yes/Attack/Death 分组 |
| 技能联动 | 点技能播放它登记的 `Animnames` 动画 |
| 属性 / 原始数据 | 真实数值 + 原始表行 |
| 深链 `#/units/Hmkg/skills` | 完整 |
| 只读接口 | list / read / batch / index / watch，越界拒绝 |
| 热更新 | SSE 推送自动刷新 |

## 数据流（运行时，无中间产物）

```
Tables/*.json ─┐
Texts/*.json ──┼─→ api.js 组装数据集（内存）─→ 列表 / 详情
WorldEditStrings.json（装饰物中文名）
glb ───────────┤
*.tscn ────────┤─→ mat.js 解析 geoset→材质
materials/*.tres┘   anim.js 解析 _anims.tres → 动画描述 → viewer3d.js 建 three 对象
```

模型目录靠 `/api/asset/index?ext=.glb` 的索引按文件名反查（835/836 个单位能对上 glb，唯一缺的是 `IntentionallyLeftBlank`）。

## 目录

```
Wiki/
├─ index.html            importmap 指向 vendor/three
├─ Run.bat              一键启动（起服务 + 开浏览器）
├─ AutoStart.bat        开机自启的增删查
├─ styles/app.css
├─ src/
│  ├─ main.js            渲染入口
│  ├─ router.js          hash 路由（分类 / 条目 / 标签页）
│  ├─ store.js           分类定义 + 过滤状态
│  ├─ constants.js       种族/类型标签
│  ├─ api.js             数据出口：表 + 文本合并 + 模型解析调度
│  ├─ viewer3d.js        three.js 预览器（材质/动画/队伍色/截图）
│  ├─ godot/
│  │  ├─ tres.js         Godot 文本资源通用解析
│  │  ├─ anim.js         _anims.tres → 动画描述
│  │  └─ mat.js          .tscn 槽位 + 材质 tres（含 add/blend/mul）
│  ├─ dev/live.js        热更新（SSE 订阅 → 自动刷新）
│  └─ ui/                dom / sidebar / topbar / overview / list / detail
├─ vendor/three/         three.module.js + GLTFLoader + OrbitControls + BufferGeometryUtils
└─ tools/
   ├─ serve.py           只读服务 + /api/watch
   └─ autostart.ps1      自启动快捷方式的实际实现
```

## 已知限制

0. **媒体分类按 240 条分页**：模型库/音效/贴图各有几千项，首屏只渲染 240 个卡片，底部「加载更多」继续追加；搜索与目录聚类是全量过滤的。
1. **geoset 可见性**：按 `.tscn` 的 `visible = false` 与材质 `layer_alpha` 还原（Hmkg 隐藏 geoset3..7、uaco 头顶黑面消失），
   详情页「模型」标签仍可逐个手动开关。
2. **建筑的超大面片自动隐藏**：`Farm` / `TownHall` 里有 4~8 顶点、尺度远超本体的面片（最大 1178 单位），
   在游戏里由专门层处理，glb 里只是普通面片。wiki 按「顶点 ≤8 且最大边 > 本体中位数 ×2.2」自动隐藏，
   「模型」标签可以再打开。取景框只按可见面片算。
3. **TeamGlow 光晕比游戏里亮**：那是整壳的纯红加色层（`replaceableId=2`、`filterMode=3`），wiki 把它的 color 压到 0.15
   并按所选队伍色着色，只为看得清本体，不追求与游戏一致。
3. **粒子 / ribbon 不在 glb 里**：火把、喷泉、传送门只有底座，见 `GODOT-ISSUES/0003`。
4. **技能详情页没有模型**：技能模型在 `Abilities/` 下，P3 再接。
5. **可破坏物 / 物品 / 升级 / 表格 / 文本** 仍是空状态。

## 需要 Godot 侧处理的问题

统一放在 `GODOT-ISSUES/`：一个编号一个目录，目录里一个 `ISSUE.md`；Godot 侧回 `REPLY.md`，wiki 回 `FOLLOWUP.md`；
关闭时目录名后加 `已关闭`。索引见 `GODOT-ISSUES/README.md`。

| 编号 | 标题 | 状态 |
|---|---|---|
| 0001 | glb 骨骼层级不完整，部分模型蒙皮错位 | 已关闭（是观察角度误判 + TeamGlow，附带修了 ATCH 布局与默认待机动画） |
| 0002 | GEOA（geoset 显隐）未导出 | 已关闭（wiki 改为读 `.tscn` 的 `visible`） |
| 0003 | 粒子 / ribbon 未导出 | 排队中 |
| 0004 | 建筑露出一圈施工脚手架 | 待确认 |

**wiki 的验收只看 `Godot/extract`**：模型能否加载、几何是否完整、动画是否绑定成功、geoset 是否与 tscn 一致；
地图级计数（BootyBay 之类）由 Godot 侧自查，与 wiki 无关。

## 调试参数

| URL 参数 | 作用 |
|---|---|
| `?live=0` | 关闭热更新（headless 截图必需） |
| `&pose=bind` | 详情页以绑定姿势打开 |
| `&hide=geoset2,geoset6` | 隐藏指定 geoset |
| `&noskin=1` | 用普通 Mesh 替换 SkinnedMesh，看原始网格 |
