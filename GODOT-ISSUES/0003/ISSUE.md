# Issue 0003 · 粒子（PRE2/PREM）与 ribbon（RIBB）未导出

- 状态：OPEN（暂缓，优先级最低）
- 提交：2026-11-08
- 需要改的端：Godot 导出（`MdxModel` → `GlbWriter`）
- 复现入口：`http://127.0.0.1:8123/Wiki/index.html#/units/hprt`

## 现象

带粒子/丝带的模型在 wiki 里只剩底座几何：

| 类型 | 具体表现 |
|---|---|
| 火把 / 火盆 | 只有石座，没有火焰 |
| 喷泉 / 瀑布 | 只有水池，没有水流 |
| 传送门 | 只有石框，没有漩涡 |
| 英雄光环 | 只有地面一圈贴图，没有粒子 |

wiki 的模型预览器只消费 glb，所以这一条不修就永远缺内容。

## 现状证据

- `MdxModel.cs`：`PRE2` / `PREM` 只做 `document.Particles += CountRecords(...)`，`RIBB` 只做 `document.Ribbons += ...`，
  连节点的 `ObjectId` / `ParentId` 都没读
- `GlbWriter.cs` 不输出任何粒子/丝带数据
- 同一个文件里的 `--smoke` 自检能打印 `Particles` / `Ribbons` 计数（例：`33/33`），说明数据在 MDX 里是拿得到的

## 参考实现

- `Open-Warcraft3/GameCore/L4_Composition/Game/Rendering/MdxParticle2Renderer.cs`（发射器 2 的完整参数与更新）
- `Open-Warcraft3/GameCore/L4_Composition/Game/Rendering/MdxRibbonRenderer.cs`
- 需要的字段大致是：发射器位置/速度/重力/生命区间/起始与结束尺寸/起始与结束颜色/贴图/混合模式/
  emission rate/长宽比/latitude/segment 颜色与透明度 —— ribbon 还有高度之上/之下的颜色。

## 期望的产物

不建议塞进 glb（glTF 没有对应概念），推荐单独输出一份 JSON：

```
<Model>.fx.json
{
  "particles": [{ "node": 12, "texture": "Textures/...", "blend": "add",
                  "life": [0.5, 0.8], "speed": [...], "gravity": [...], "rate": 30, ... }],
  "ribbons":  [{ "node": 18, "texture": "...", "heightAbove": 10, "heightBelow": 5, ... }]
}
```

wiki 侧用 `THREE.Points` / 自建 ribbon mesh 还原（单模型预览不需要和游戏完全一致，能量出来即可）。

## 验收标准（wiki 侧，只看 converted）

1. `#/doodads` 里搜「火把」「火盆」「喷泉」「瀑布」的装饰物模型，能看到粒子/丝带
2. 单位侧抽样：带发射器的英雄（如 `Hmkg` 的 3 个）加载后不报错
3. 没有粒子数据的模型不受影响：缺 `*.fx.json` 时 wiki 静默跳过，控制台无 404 噪音

地图里火把的观感属于 **Godot 侧自查**（`--shot` 同机位对比），wiki 不参与 —— wiki 只读
`Godot/extract`，与地图文件无关。

## 回复区（Godot 侧填写）

处理后在本目录写 `REPLY.md`：JSON 字段定义（wiki 侧按这个实现渲染），以及哪些模型有数据。
