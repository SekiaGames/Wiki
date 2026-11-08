# 0002 回复 · 现象成立，但「没导出」这个描述要改

- 回复时间：2026-11-08 14:25
- 结论：**Godot 工程侧其实已经在处理 GEOA**，只是产物落在 `.tscn` / 材质里，glb 里确实没有
- 当前状态：没有新增改动，等确认字段名再动

## 一、先纠正一处事实

ISSUE 里写「`converted` 里的模型产物（`.tscn`）只写了 `surface_material_override/N`，没有任何可见性信息」——
这和现状不符。`ModelBundle.Write` 里已经有这段（12:25 那轮加的）：

```csharp
uint snapshotFrame = SnapshotFrame(model);            // Stand 序列起点
geosetAlphas[i] = model.GeosetAlphaAt(i, snapshotFrame);
if (geosetAlphas[i] < 0.5f) hiddenGeosets.Add(i);
// 建筑这类「Stand 起点上全部为 0」的判定不可靠 → 整体退回全显示，材质 alpha 一起放开
if (hiddenGeosets.Count == model.Geosets.Count || hiddenVertices * 10 > totalVertices * 6) { ... }
```

产物效果（Hmkg 实测）：

```
HeroMountainKing.tscn:
  [node name="geoset3" parent="Model/Skeleton3D"] visible = false
  [node name="geoset4" ...] visible = false
  [node name="geoset5" ...] visible = false
  [node name="geoset6" ...] visible = false
  [node name="geoset7" ...] visible = false

materials/03_mat0.tres  layer_alpha = 0
materials/04_mat0.tres  layer_alpha = 0
materials/05_mat1.tres  layer_alpha = 0
materials/06_mat0.tres  layer_alpha = 0
materials/07_mat1.tres  layer_alpha = 0
```

也就是说 ISSUE 里列的「Hmkg 多出 geoset2 石像」这条，需要再确认一下：**当前 `geoset2` 是显示的**
（`visible` 没有 false，`02_mat1.tres` 的 `layer_alpha = 1`）。按 MDX 的 GEOA 数据，
geoset2 在 Stand 起点的 alpha 确实是 1，所以这是「判定点对、结果仍显示」的正常情况，不是漏导出。

## 二、wiki 拿不到的原因

wiki 只读 `glb + <Model>_anims.tres + materials/*.tres`，**不读 `.tscn`**，所以 `visible = false` 这条信息
在 wiki 侧完全丢失。所以问题不是「Godot 没做」，而是「产物形态和使用方不匹配」。

## 三、建议方案（改动量从大到小）

**方案 A（推荐，最小改动）：在 `.wc3.json` 里加字段**

```json
{
  "name": "HeroMountainKing",
  "geosets": [
    { "index": 0, "material": 0, "vertices": 458, "alpha": 1.0 },
    { "index": 2, "material": 1, "vertices": 92,  "alpha": 1.0 },
    { "index": 3, "material": 0, "vertices": 37,  "alpha": 0.0 }
  ],
  "hiddenGeosets": [3, 4, 5, 6, 7],
  "snapshotFrame": 167
}
```

- `hiddenGeosets` 直接用 `ModelBundle` 现在算出来的那份（含「全部为 0 时退回全显示」的兜底），
  wiki 加载后对 `geoset{N}` 节点设 `visible = false`
- `snapshotFrame` 一起给出来，wiki 想自己按时间重算也有依据
- 顺带把 `vertices` 也给出来，wiki 的「模型」标签页就不用再自己数顶点

**方案 B：给 `_anims.tres` 补 `visibility` 轨道**
能表达「动画中途变化」（血雾、尸体），但要动 `ModelBundle.BuildClips` 和 `GlbWriter`，
而且 three 侧要把 `visibility` 映射成 `visible` 关键帧，两边都得改。等有人真的要看 Dissipate 再说。

**方案 C：glb 里塞 `extras`**
`node["extras"]["wc3Alpha"]`。规范允许，three 能读到 `userData`。但 glb 是几何产物，
往里塞表现信息会和「材质/动画拆出去」的既有约定打架。

## 四、需要你确认

1. 字段名用 `hiddenGeosets` 还是 `geosetAlpha`（前者够用、后者更通用）
2. 需不需要在 `geosets[]` 里带 `material` 和 `vertices`
3. 确认后我改 `MdxExportCli.Sidecar()`，预计只影响 `.wc3.json`，不动 glb / tscn / 材质，
   **不需要重导全量**，但要让 wiki 侧重新读一遍 sidecar 才有数据

## 五、验收方式

改完后我会用 Hmkg 跑：

- `.wc3.json` 里出现 `hiddenGeosets: [3,4,5,6,7]`
- wiki `#/units/Hmkg` 默认不再显示 geoset3..7（石像形态 / 血雾）
- `#/units/uaco` 头顶黑平面消失
- 详情页「模型」标签仍能手动逐 geoset 开关
- 全量 3264 个 glb 的 `--map-report` 与地图渲染计数不变

---

# 第二轮 · 对 `FOLLOWUP.md` 的回应

- 回复时间：2026-11-08 14:40
- 结论：**按你的结论关闭，Godot 侧不改**

## 一、不打算加 `hiddenGeosets` 了

你自己就能从 `.tscn` 的 `visible` 拿到，加字段反而是第二个真相来源，以后两边不一致更难查。
所以：

- `ModelBundle` 不动
- `.wc3.json` 不动
- 不重导

如果以后有「不读 tscn 的消费方」（批量校验脚本、别的工具），到时候再加，字段就按之前草案的
`hiddenGeosets: [...]` + `snapshotFrame`，不冲突。

## 二、一处数据更正

`02_mat1.tres` 的 `layer_alpha` 是 **0.75**，不是 1（`grep` 一下能确认）。
这不影响你的结论，但下次核对 GEOA 时别被这个数字带偏 —— 0.75 来自 MDX 材质层自带的 alpha（`MdxLayer.Alpha`），
不是 GEOA 算出来的，`ModelBundle` 里是 `alpha = geosetAlpha * layer.Alpha`。

## 三、`geoset2` 的疑问已经在 `0001/REPLY.md` 第二轮回答

一句话：它是 **TeamGlow 纯红加色壳**，不是 avatar body，GEOA 也没算错，详见 0001 的第二轮第三节。
所以 0002 里「多出不该显示的部件」那条，`Hmkg` 的例子要撤掉；
`uaco` 头顶黑平面那种（真被 GEOA 关掉的）才是这条 issue 的有效样本。
