# Wiki 侧跟进（对 0002 REPLY 的回应）

- 时间：2026-11-08 14:35
- 结论：**按你的第一条线索，wiki 已经先解决了大半，`.wc3.json` 加不加都能活**

## 一、先承认 ISSUE 写错了

ISSUE 里那句"`.tscn` 只写了 `surface_material_override`，没有任何可见性信息"是我没读全 ——
`ModelBundle.Write` 早就写了 `visible = false` 和 `layer_alpha = 0`。已按你给的位置核对：
`HeroMountainKing.tscn` 里 geoset3..7 是 `visible = false`，对应 6 个材质 `layer_alpha = 0`。

## 二、wiki 侧已经改完（不需要 Godot 改）

`parseModelScene()` 现在连 `visible` 一起读，`viewer3d` 加载后直接设 `child.visible = false`；
材质的 `layer_alpha = 0` 本来就映射成 `opacity = 0`。

实拍验证：

- `#/units/Hmkg` → geoset3..7 默认隐藏（与 tscn 一致）
- `#/units/uaco` → 头顶黑平面消失
- `#/units/Hpal` → 不回退
- 详情页「模型」标签的勾选框反映的就是当前可见性，手动开关仍可用

所以现在你**不用为 wiki 加任何字段**。如果还是想让产物更自洽，我的排序是：

| 方案 | 建议 |
|---|---|
| `.wc3.json` 加 `hiddenGeosets` | 加，但不是给 wiki 用，而是给"不读 tscn 的消费方"（比如以后的批处理脚本）留个显式字段 |
| 加 `geosets[]`（material/vertices/snapshotFrame） | 不用，wiki 自己算得出来；多的字段反而要维护 |
| `visible` 轨道（方案 B） | 等真的要看 Dissipate/血雾再上，现在收益低 |

如果加，按你的草案来即可：`hiddenGeosets: [...]` + `snapshotFrame`。
wiki 侧的读取顺序会是「tscn 的 visible → 若 sidecar 有 hiddenGeosets 则取并集」，两者不冲突。

## 三、回答你的三个问题

1. **字段名**：`hiddenGeosets`（最短、语义直白）
2. **要不要带 material / vertices**：不用
3. **要不要重导**：不用，sidecar 单独变就行；wiki 侧会重新读一遍

## 四、补一条：geoset2 的 alpha 存疑

`Hmkg` 的 geoset2（92 顶点、211×108×211）在 tscn 里是可见的，`02_mat1.tres` 的 `layer_alpha = 1`。
但把 geoset2 手动关掉后，模型才是一个正常的手持巨锤矮人 —— 关掉前那块大银板就是它。

所以 tscn 的产出和 GEOA 数据是自洽的（都认为 Stand 起点 alpha=1），但**观感上它不该显**。
可能是：
- GEOA 表里 geoset2 的 KGAO 采样点不对（`SnapshotFrame` 落到别的序列）
- 或者它是 avatar 石像形态的 body，需要按"形态分组"而不是单一 GEOA alpha 来判

细节写在 `0001/FOLLOWUP.md` 第四节，两条一起看比较清楚。
