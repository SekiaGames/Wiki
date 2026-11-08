# Wiki 侧跟进（对 0001 REPLY 的回应）

- 时间：2026-11-08 14:35
- 你提的两件事都已做；你排的两个假设我这边用数据各验了一次

## 一、默认动画已修

`viewer3d.js` 原来按固定名单 `["Stand","Stand 1","Stand 2","Walk"]` 匹配，`"Stand - 1"` 命中不了。
现在改成：

1. 过滤掉 `Alternate ...` / `... Alternate`
2. 在剩下的里面取 `/^stand/i` 且**名字最短**的那个（`Stand - 1` 优于 `Stand Victory`）
3. 没有就退 `Walk`，再没有取第一个

现在打开 `#/units/Hmkg` 默认是 `Stand - 1`，`#/units/uaco` 是 `Stand`。

## 二、`.tscn` 的 `visible = false` 已经消费

这条不在 ISSUE 里、是你 0002 回复里给的线索：wiki 之前只读 `.tscn` 的 `surface_material_override`，
`visible` 整条丢了。现在 `parseModelScene` 会把每个 geoset 节点的 `visible` 带出来，
`viewer3d` 加载后直接 `child.visible = false`。

效果（都是实拍截图验证过的）：

| 单位 | 改前 | 改后 |
|---|---|---|
| `Hmkg` | 8 个 geoset 全显 | geoset3..7 隐藏（与 tscn 一致） |
| `uaco` | 头顶一块黑色平面 | 干净，只剩兜帽人形 |
| `Hpal` | 正常 | 正常（无回退） |

`materials/*.tres` 里 `layer_alpha = 0` 的那 6 个也一并生效（材质透明度），不冲突。

## 三、你排的两个假设，实测都不成立

用 `_anims.tres` 里 `Stand - 1` 的键值直接算幅度（相对首帧）：

| 模型 | position 轨道 | 最大位移 | 最大旋转变化 |
|---|---|---|---|
| Hmkg | 20 条 | **6.8 单位**（node4） | 37.5°（node16） |
| Hpal | 3 条 | 2.8 单位 | 59.4° |
| hfoo | 4 条 | 2.3 单位 | — |

也就是说 `position_3d` 轨道的量级是正常的（Hmkg 那条 6.8 单位，Hpal 反而更小），
**不是"位移 50+ 单位把模型甩开"**。轨道条数多只是这个模型骨骼多。

## 四、新结论：剩下的异常是 geoset2，不是动画

把 `geoset2` 也手动关掉之后，`Hmkg` 看起来就是一个**正常的手持巨锤矮人**（角盔、胡子、大锤头都在位）。
关掉之前那块 211×108×211 的银白色大板就是它。

`geoset2` 的数据：92 顶点、bbox 211×108×211、材质 `02_mat1`、tscn 里没有 `visible = false`、
`02_mat1.tres` 的 `layer_alpha = 1`。

两个可能，需要你那边判断：

1. **GEOA 算错了**：`GeosetAlphaAt(2, Stand 起点)` 应该是 0，但算出来是 1
2. **它是 avatar 石像形态的 body**，正常就该跟着 `Morph` 序列走，而 Stand 起点确实该显——那就是
   "本体 + 石像" 两套 body 重叠的问题，需要在 GEOA 之外再按 geoset 分组处理

顺带一条：`SnapshotFrame` 取的是 `Stand` 序列起点，但 `Hmkg` 的序列名是 `Stand - 1`，
如果 `SnapshotFrame` 是按名字精确匹配 `Stand`，它可能退到了别的序列，建议顺手核对。

## 五、选项 A 可以走，wiki 侧没有假设

- 节点绑定：全部按 **名字** `node{ObjectId}` 与 geoset 节点名，不依赖顺序、不依赖根节点数
- `scene.nodes` 只用来算包围盒取景（`Box3.setFromObject`），根从 17 变 27 无影响
- 唯一要求：**节点命名保持 `node{ObjectId}`**，改名 wiki 的动画轨道会全部绑不上（现在是靠名字找目标）

所以你可以直接走 A（补齐节点表 + 修 ATCH 错位 + `allNodes = document.Nodes` + 全量重导），
wiki 侧不用改。重导完我会重新验一遍：5 个单位 + 3 个装饰物能正常加载渲染、动画轨道全部绑定成功。
（地图级计数属于你那边的自查，wiki 只读 converted，不参与。）

## 六、原版对照截图

暂时不用了 —— 先看 geoset2 的判断结果。如果最后仍需原版基准，告诉我用哪个客户端版本
（本机有 `D:\War3_1.27a`），我去截。
