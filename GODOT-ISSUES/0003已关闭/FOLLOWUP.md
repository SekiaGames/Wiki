# Wiki 侧跟进（对 0003 REPLY 的回应）

- 时间：2026-11-08 14:35
- 结论：**全部同意**，序也按你说的走

## 一、字段草案照收

`particles` / `ribbons` 的字段名保持你写的这份，wiki 侧按这个实现：

- `node` 用 `node{ObjectId}`：wiki 用 `root.getObjectByName()` 直接挂，和现在的动画绑定方式一致
- `texture` 给 converted 相对路径 + `.png`：wiki 不再做后缀/大小写猜测
- `color` / `alpha` 当静态中值，`width` 当 `[start, end]`，wiki 侧按生命插值
- 没有粒子就不写文件：wiki 侧请求失败静默跳过，不生成空壳（这条已记住）

补两个 wiki 侧实现时一定会用到、草案里没有的字段，你实现时看要不要顺手给：

| 字段 | 用途 |
|---|---|
| `node` 为空时用模型原点 | 有些发射器没有独立节点，直接挂根 |
| `ribbon.filterMode` / `particle.rows/columns` | 你已给 rows/columns；ribbon 也建议带上，不然透明方式对不上 |

## 二、顺序同意：0001 渲染层 → 全量重导验证 → 再叠 0003

同一个理由：`GlbWriter` / `ModelBundle` 同一片代码，两个改动一起出错没法二分。
0003 落地前 wiki 不需要任何改动，现在的加载器遇到缺 `*.fx.json` 就是全静态，不会报错。

## 三、验收点改成装饰物

同意。`#/units/hprt` 那个传送门确实不是好样本，改成：

1. `#/doodads/` 里搜「火把」「火盆」「喷泉」「瀑布」的模型
2. 地图里火把的观感（`--shot` 同机位对比）—— 这条由 Godot 侧自查，wiki 只读 converted
3. 反例：挑一个不带粒子的装饰物，确认没有多出来的东西

装饰物分类 wiki 已经接完了（469 条 + 3D 预览 + 变体切换），到时候直接在这些模型上验就行。

顺带一句：wiki 只预览单个模型，粒子不需要做到跟游戏完全一致 —— 能看出"有火/有水/有光晕"就够。
所以 `emissionRate` 之类的数值即使有偏差也不用纠结，形态和颜色对就行。

---

# 第五轮 · 对 RIBB 落地的复核，关闭本条

- 时间：2026-11-10 21:55
- 结论：**PRE2 粒子 / LITE 灯光 / RIBB 丝带三条都验过，关闭**

## 一、数据侧复核

| 回复里的结论 | 复核方式 | 结果 |
|---|---|---|
| 147 个 `.tscn` 有 `rib_node` | 全库 grep | **147** ✅ |
| `NagaBirth` 两条丝带烘成真网格 | 读 tscn | `rib_node26` / `rib_node27` 都是 `MeshInstance3D` ✅ |
| `Shade` 无重力丝带只给锚点 + metadata | 读 tscn | `rib_node48/49` 是 `Node3D`，带 `war3_ribbon_*` 十项 metadata ✅ |
| 火把有 `fx_node` + 灯光 | 读 `TorchHuman.tscn` | `fx_node3`（实例化 fx 子场景）+ `light_node2` ✅ |

## 二、实拍

- `models:Doodads/LordaeronSummer/Props/TorchHuman/TorchHuman.glb` —— 柱顶一团黄→橙火苗 ✅
- `models:Buildings/Naga/ShrineOfAshjara/NagaBirth.glb` —— 底座外圈垂落的水幕带 ✅

## 三、接受「静止看不到丝带」这个解释

「364 条丝带没有一条的节点链带平移/旋转动画，静止时截面重合、面积为零」——数据和现象对得上：
只有 26 条 `gravity > 0` 的丝带静止才有形态，正好是那 15 个模型。Wiki 不会再期待飘带，
也不会把它当丢数据处理。

## 四、剩下的不算本条

1. 运行时丝带渲染器（按 `war3_ribbon_*` 生成拖尾）属于 Editor / Launcher 表现层。
2. `KRAL` / `KRCO` / `KRTX` 与 `KP2E` 一样只取待机帧静态值 —— 这是既定取舍，不重开。
3. `filterMode` 5/6 按混合处理避免整条变黑 —— 已知偏差，接受。

本条关闭；后续如果 Wiki 侧要展示「按 tileset 换肤」或运行时拖尾，另开 issue。

