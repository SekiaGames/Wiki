# 需要 Godot 侧处理的 Issue

Wiki 只读 `Godot/assets/converted`，**与地图文件无关**：wiki 的验收只看 converted 里的产物
（glb / tscn / tres / wc3.json）能否正确加载与渲染。地图级计数（BootyBay 之类）属于 Godot 侧自查，不写进 wiki 验收。

## 约定

- 一个 Issue 一个子目录，目录名就是编号：`0001`、`0002`…（四位、递增）
- 每个目录里放一个 `ISSUE.md`：现象、复现、证据、根因判断、建议改动、验收标准
- Godot 侧处理完，在**同一个目录**里写 `REPLY.md`（改了什么、跑了什么验证、产物要不要重新 extract）
- Wiki 侧回一轮写 `FOLLOWUP.md`，可以多轮
- **关闭时在目录名后直接加 `已关闭`**（`0001已关闭`），不删除、编号不复用
- 本文件的表格由 Wiki 侧维护

## 索引

| 编号 | 标题 | 状态 |
|---|---|---|
| [0001](0001已关闭/ISSUE.md) | glb 骨骼层级不完整，部分模型蒙皮错位 | **已关闭** · [回复](0001已关闭/REPLY.md) [跟进](0001已关闭/FOLLOWUP.md) |
| [0002](0002已关闭/ISSUE.md) | GEOA（geoset 显隐）没有导出 | **已关闭** · [回复](0002已关闭/REPLY.md) [跟进](0002已关闭/FOLLOWUP.md) |
| [0003](0003/ISSUE.md) | 粒子（PRE2/PREM）与 ribbon（RIBB）未导出 | 排队中（等节点表改动落地） · [回复](0003/REPLY.md) [跟进](0003/FOLLOWUP.md) |
| [0004](0004/ISSUE.md) | 建筑模型露出一圈施工脚手架（TownHall 最明显） | OPEN（待确认是否 GEOA 漏判） |

## 关闭结论

### 0001 已关闭（2026-11-08 15:07）

- **原始判断（父链断在 GenericObject 上）不成立**：`--mdx-probe` 显示 Hmkg 的 `node0..7` 的 `ParentId` 本来就是 -1，
  父节点缺失 0 个；`matrixWorld × boneInverse = I`，绑定姿势不改变任何顶点
- **「散架」是观察角度造成的误判**：默认 56° 俯视 + 近景时，剑/盾/肩甲在 Z 轴前后错开几十单位，
  透视把它们投到同一平面，看起来脱节；`--pitch=-8`（平视）下是完整矮人
- **残留的观感问题**是 `geoset2` 的 TeamGlow 纯红加色壳（`replaceableId=2`、`filterMode=3`、alpha 0.75），
  wiki 侧已把这类材质的 color 压到 0.15 并按队伍色着色
- 同一片代码里修掉的两个真 bug：`ATCH` 记录布局少读 4 字节（10 个挂点 objectId 全成 0）、
  默认待机动画选成 `Alternate Attack - 1`（`Stand - 1` 匹配不到 `Stand`）
- 已全量重导 3264 个 glb（116.5 MB → 73.4 MB）并 `--import`

### 0002 已关闭（2026-11-08 15:07）

- GEOA 一直在做，产物落在 `.tscn` 的 `visible = false` 与材质 `layer_alpha`，是 **wiki 读漏了**
- wiki 侧已改为读 `.tscn` 的 `visible`：Hmkg 默认隐藏 geoset3..7、uaco 头顶黑面消失、Hpal 不回退
- Godot 侧不加 `hiddenGeosets`（避免第二个真相来源）
- 数据更正：`02_mat1.tres` 的 `layer_alpha` 是 0.75，不是 1

### 0003 仍在排队

- 前置依赖：节点表补齐（已完成）+ 全量重导（已完成），接下来 Godot 侧动 `<Model>.fx.json`
- 字段草案已对齐（`node` 空=挂根、`filterMode`、`rows/columns`、`priority`、`textureSlot`）
- 验收点改到装饰物火把 / 火盆 / 喷泉 / 瀑布，不用 `#/units/hprt`

## 常用命令（仅 Godot 侧自查用）

- 转换：`extract.bat`
- 模型重写：`--mdx-bundle`
- 单模型节点表：`--mdx-probe=<mdx 路径>`
- 地图计数（Godot 侧回归，不参与 wiki 验收）：`--map-report` / `--map ... --shot ...`

wiki 侧的调试参数：`?noanim=1`、`?noskin=1`、`?hide=geoset2`、`?pose=bind`、`?elev=`、`?zoom=`、`?anim=`。
