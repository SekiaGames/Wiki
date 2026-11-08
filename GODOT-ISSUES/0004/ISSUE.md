# Issue 0004 · 建筑模型露出一圈施工脚手架（TownHall 最明显）

- 状态：OPEN（待确认是否属于 GEOA 漏判）
- 提交：2026-11-08 15:15
- 需要改的端：Godot 导出（`ModelBundle` 的 GEOA 快照判定）—— **先确认，再改**
- 复现入口：`http://127.0.0.1:8123/Wiki/index.html#/models/Buildings/Human/TownHall/TownHall.glb`

## 现象

城镇大厅（`TownHall.glb`）在 wiki 里渲染出来，主体正常，但**外围一圈竖直木架/木板**（像施工脚手架）也在。

同一个模型在 `converted` 里的 `.tscn` 只有 5 个 `visible = false`（36 个 geoset 里），
木架那批 geoset 是可见的。

## 已排除的 wiki 侧问题

修了两处 wiki 自己的 bug（不影响本条判断，但先说明我这边是干净的）：

1. **无材质的隐藏节点被漏掉**：`Farm` 的 `geoset12/13` 是 `visible = false` 且没有
   `surface_material_override`，wiki 之前只处理"有材质的节点"，所以隐藏标记丢了 → 渲染出一块黑幕。
   现在 `parseModelScene` 单独收 `hidden` 列表，已修。
2. **超大面片自动隐藏**：建筑里有 4 顶点、尺度远超本体的面片（`Farm` 1139×859、`TownHall` 340×871×1178），
   glb 里就是普通面片，wiki 现在按「顶点 ≤8 且最大边 > 本体中位数 × 2.2」自动隐藏（详情页「模型」标签可再打开）。

上面两条修完后，`Farm` 和 `TownHall` 的黑色幕布都消失了，**剩下的就是这圈木架**。

## 需要你确认的问题

1. 木架那些 geoset 在 MDX 里的 GEOA 数据是什么？是不是 `Stand` 快照帧上 alpha 就该是 0，
   但 `ModelBundle` 的判定没覆盖到？
2. 还是它们属于「建造/升级阶段」的 geoset，正常玩法里由 `Birth` / `Upgrade` 动画驱动，
   静态预览本来就会全显？

如果是第 2 种，wiki 侧不需要 Godot 改任何东西 —— 我在详情页让用户手动关就行，
但希望产物里能有个「阶段分组」的提示（比如 `.wc3.json` 里给每个 geoset 一个 `stage` 字段），
否则每个建筑都得靠人肉猜哪几个该关。

## 数据（TownHall，36 个 geoset）

| geoset | 顶点 | bbox | 材质 | tscn visible |
|---|---|---|---|---|
| geoset1 | 4 | 340×871×1178 | `01_mat0`（add） | true |
| geoset3 | 4 | 455×0×455 | `03_mat2`（add） | true |
| geoset5 | 8 | 287×0×311 | 05 | true |
| geoset13 / 16 | 8 | 287×0×311 | 13 / 16 | true |
| 其余 30 个 | 17~1053 | 40~400 | 多为 opaque | 5 个为 false |

（`geoset3/5/13/16` 的 y 尺寸是 0，是**水平面片**，像是阴影/贴地面；`geoset1` 是竖直大面）

## 验收标准（wiki 侧）

1. `#/models/Buildings/Human/TownHall/TownHall.glb` 默认不再出现木架（或明确接受它）
2. `#/models/Buildings/Human/Farm/Farm.glb` 不再有黑幕（已达成）
3. 单位侧不回退：`Hmkg` / `Hpal` / `hfoo` / `uaco` 正常
4. 「模型」标签里 geoset 勾选状态与实际可见性一致

## 回复区（Godot 侧填写）

处理后在本目录写 `REPLY.md`：说明这批 geoset 的语义；如果要给 `stage` 字段，给出字段定义。
