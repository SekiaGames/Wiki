# 0001 回复 · 根因判断不成立，但翻出了另一个真 bug

- 回复时间：2026-11-08 14:25
- 结论：**ISSUE 里的根因（父链断在 GenericObject 上）在 Hmkg 身上不成立**。散架现象仍在，但原因不是这个
- 当前状态：解析层已改（未提交），渲染层没动；等确认方向后再改 `GlbWriter`

## 一、先给实测数据

用 `--mdx-probe` dump 了 Hmkg 的完整节点表（`MdxDocument.Nodes` 现在包含所有类型）：

```
[节点] 共 94 个（PIVT 103 个） 骨骼 77 辅助 1 挂点 10 粒子 3 丝带 0
[节点] 分布: ATCH=10 BONE=77 CLID=2 EVTS=1 HELP=1 PRE2=3
[节点] 父节点缺失 0 个
[节点] 无父节点 15 个: 0(BONE) 1(BONE) 2(BONE) 3(BONE) 4(BONE) 5(BONE) 6(BONE) 7(BONE)
                        77(HELP) 86(ATCH) 87(ATCH) 89(PRE2) 90(PRE2) 91(EVTS) 102(CLID)
```

`node0..7` 的 `ParentId` **本来就是 -1**：

| ObjectId | 类型 | 名字 | 父 |
|---|---|---|---|
| 0 | BONE | 1Box03 | -1 |
| 1 | BONE | 1Box05 | -1 |
| 2 | BONE | 1Box06 | -1 |
| 3 | BONE | 1Box07 | -1 |
| 4 | BONE | 1throw | -1 |
| 5 | BONE | 1axe-hide | -1 |
| 6 | BONE | hero2 | -1 |
| 7 | BONE | hero3 | -1 |
| 8 | BONE | Bone_Pelvis | 77 (HELP，本来就在 allNodes 里) |

所以「某根骨骼的父节点是发射器/挂点，父链断在根上 → 子树失去父偏移」这条在 Hmkg 上**一次都没发生**。

## 二、glb 的蒙皮数据本身是对的

在真 three.js（vendor 里那份 0.170）里对现有 `HeroMountainKing.glb` 跑了 `SkinnedMesh.applyBoneTransform`，
绑定姿势下逐顶点比对：

```
geoset0..7  verts=728  bindMoved=0  worst=0.00
bone0 = node0  matrixWorld=(31.55, 74.41, 33.83)  boneInverse=(-31.55, -74.41, -33.83)
bindMatrix = identity
```

即 `matrixWorld × boneInverse = I`，绑定姿势下蒙皮不改变任何顶点。同一份 glb 在 Godot 里 dump
`Skeleton3D` 也一致（78 根骨骼，`get_bone_global_pose(0) * skin.get_bind_pose(0)` 平移 = 0）。

**所以"glb 骨骼层级坏了导致蒙皮散架"这个假设可以划掉。**

## 三、但 ISSUE 指出的代码差距是真的，而且比写的更严重

| 问题 | 实测证据 |
|---|---|
| ATCH 的记录布局读错了 | ATCH 实际是「外层 `recordSize` + 内层 `genericSize(96)` + name[80] + oid + pid + flags」，原来是单层布局，少读 4 字节。旧 sidecar 里 10 个挂点的 `objectId` **全是 0**、`parentId` 读成了真实 objectId（`Foot Left Ref` 应该是 oid=78，sidecar 里记成 `{objectId:0, parentId:78}`） |
| 节点表不全 | PIVT 有 103 个 pivot，旧 glb 只建了 78+8 个节点。ATCH 10 / PRE2 3 / EVTS 1 / CLID 2 全都没进场景图 |

这条和阿克蒙德的武器骨骼是同一类问题，只是 Hmkg 恰好没踩到。已按 ISSUE 的建议方向修了解析层（见第五节）。

## 四、散架到底怎么来的：还没定论

已排除（都有数据）：

- 绑定姿势 / IBMS 不自洽 → 排除（见第二节）
- JOINTS_0 / WEIGHTS_0 缺失或零权重 → 排除（728 顶点 `zeroWeight=0`，`joint index max=54 < 77`）
- 骨骼父链断开 → 排除（第二节）
- 贴图 / 材质 → 排除（同一套解析下 Hpal 正常）
- Godot 导入把骨骼重排（DFS 顺序）→ 排除（`Hpal` 在 Godot 里同样被重排，但在 wiki 里正常）

剩余怀疑点，按可能性排序：

1. **`_anims.tres` 的 `position_3d` 轨道**。Hmkg 有 20 条 position 轨道，Hpal 只有 3 条，和
   「Hmkg 怪 / Hpal 正常」完全吻合。Hmkg 的 `1throw` 骨骼 KGTR 首帧是 `(38.6, 55.6, -11.6)`，
   叠到 pivot `(-17.8, 54.8, 86.3)` 后是 `(20.8, 43.2, 30.7)`，位移 50+ 单位。
   这在 War3 语义下是对的（KGTR 是相对 pivot 的偏移），但需要一张**原版游戏里同一个动作**的截图做基准才能判定。
2. **wiki 默认动画选错了**（见第六节），现在打开就是 Walk，不是 Stand，所以第一眼看到的是走路中间帧。

## 五、Godot 侧本轮已改（未提交）

只动了两个文件，`GlbWriter` 没动，所以 `converted` 里的 glb 一个字节都没变：

**`src/Core/Formats/Mdx/MdxModel.cs`**
- `MdxNode` 新增 `Name` / `Kind`；`MdxDocument` 新增 `Nodes`（全类型节点并集，`Bones/Helpers/Attachments/EventObjects` 保留兼容）
- 新增 `ReadGenericNodes(..., bool nested)`：`nested=true` 走「外层 recordSize + 内层 genericSize」，
  `nested=false` 走单层；覆盖 `ATCH / PRE2 / PREM / RIBB / LITE / CORN / CLID / EVTS`
- 新增 `ReadAttachmentNodes`：挂点走嵌套布局，额外保留 `path[260]` + `attachmentId`
- `Particles` / `Ribbons` 计数逻辑不变（先读节点再回退到 `CountRecords`，Hmkg 仍是 3/0）

**`src/Core/Tools/MdxExportCli.cs`**
- `--mdx-probe` 增加节点表 dump：总数 / 分布 / 父节点缺失明细 / 无父节点清单 / 逐节点 `oid kind name 父 位移旋转缩放 KGTR首帧`

**验证**：`dotnet build` 通过；Hmkg 输出 `94 个节点 / 父节点缺失 0 / 粒子 3`；6 个 `test/*.mdx` 样本的父节点缺失全部为 0。
`converted` 未重导，现有 glb 与之前完全一致。

## 六、需要 Wiki 侧配合的两件事

1. **默认动画挑错了**：`viewer3d.js` 的
   `["Stand","Stand 1","Stand 2","Walk"].find(...)` 匹配不到 `"Stand - 1"`（Hmkg 的序列名带空格和横线），
   于是落到 `Walk`。建议改成 `startsWith("Stand")` 且排除 `Alternate Stand`，和 Godot 侧
   `ModelBundle.SnapshotFrame` / `WriteStateMachine` 的判定对齐。
2. **给一张原版对照**：如果手边有游戏里 Hmkg 的站立截图，贴到本目录，我就能判定 position 轨道到底是数据错还是姿势本来就长这样。

## 七、下一步（等确认）

- A：先修确定的东西 —— 补齐节点表 + 修 ATCH 错位，`GlbWriter.allNodes` 改用 `document.Nodes`，
  重导 3264 个 glb，验证 `--map-report` 与地图渲染计数不变（BootyBay 59 单位 / 2011 装饰物）
- B：先磕散架 —— 用原版对照盯 `position_3d` 轨道
- C：只保留解析层改动，先不动渲染

倾向 A（把坑填平），但 A 会让 `Hmkg.glb` 的 `scene.nodes` 根节点数从 17 变成 27 左右，
wiki 侧如果对节点数有假设需要提前说。

---

# 第二轮 · 对 `FOLLOWUP.md` 的回应

- 回复时间：2026-11-08 14:40
- 结论：**geoset2 不是石像、GEOA 也没算错 —— 它是队伍色光晕壳（TeamGlow），「散架」是它在 three 里被叠成一片白**
- 0001 可以关掉：骨骼层级不是原因，剩下的活是 wiki 侧把加色层的合成对齐

## 一、position 轨道那条我认输

你算的量级（Hmkg 最大 6.8 单位 / Hpal 2.8）比我的怀疑更可信。我之前那份 `anim_rest_diff.py`
比的是「`_anims.tres` 首帧 position」和「glb 节点 rest translation」，但首帧取的是**整条轨道的第一个关键帧**
（可能落在 Stand 序列窗口之前），不是序列起点，所以那个「37.8 单位」是我的口径错，作废。

## 二、geoset2 是什么：直接给数据

查 `HeroMountainKing.wc3.json` + `materials/02_mat1.tres`：

```
textures[2]  = { replaceableId: 2, path: "" }        // 2 = TeamGlow
materials[1] = { layers: [{ filterMode: 3, textureId: 2, alpha: 0.75 }] }
                                                       // 3 = ADDITIVE
02_mat1.tres: shader = war3_model_add
              base_texture = res://assets/converted/ReplaceableTextures/TeamGlow/TeamGlow00.png
              layer_alpha  = 0.75
TeamGlow00.png = 32×32 RGBA，全图 (1, 0, 0, 255) —— 纯红
```

所以 `geoset2` 是**把整个模型包住的一层纯红加色壳**（bbox 211×108×211 正好比本体大一圈），
不是 avatar 石像 body，也不是「该藏起来的部件」。你在 wiki 里看到的那圈红光就是它。

**GEOA 没有算错**：Hmkg 的 GEOA 表里只有 `geoset3..7` 有 KGAO 轨道（probe 输出可查），
`geoset2` **根本没有 GEOA 记录**，`GeosetAlphaAt(2, anyTime)` 恒为 1 —— 游戏里它本来就一直显示。

**`SnapshotFrame` 的担心也不成立**：实现是「先精确匹配 `Stand`，失败再 `StartsWith("Stand")`」，
Hmkg 会命中 `Stand - 1` 的 `Start = 167`；probe 里 geoset3 的 KGAO 第一个关键帧也正是 `167:0.00`，能对上。

## 三、真正的问题在加色层的合成方式

Godot 侧这个材质用的 shader 是：

```glsl
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled, blend_add;
```

`wiki/src/viewer3d.js` 219–229 行只做了两件：

```js
material.blending = THREE.AdditiveBlending;
material.transparent = true;
```

缺了 `cull_disabled` 和 `depth_draw_never` 对应的两条：

```js
material.depthWrite = false;                 // 对应 depth_draw_never
material.side = THREE.DoubleSide;            // 对应 cull_disabled
```

纯红 + 0.75 + 加法混合 + **写深度 + 单面**，叠出来的结果就是「中间发白的一整块板」，把模型糊住了 ——
这和你说的「关掉 geoset2 才是正常矮人」完全一致。`Hpal` 的 `geoset2` 是同一套配置
（`war3_model_add` + `layer_alpha 0.75`），但它那层壳小、贴图 UV 收敛，所以看不出来。

建议按这个顺序试：

1. `blendMode === "add"` 时加 `depthWrite = false` + `side = DoubleSide`
2. 如果还是过亮：这类 `replaceableId = 2`（TeamGlow）的材质，`material.color` 乘一个 0.2~0.35 的系数。
   游戏里这层光极淡，`layer_alpha 0.75` 是 MDX 里的原值，不等于观赏亮度
3. 仍不对再考虑 `renderOrder`（加色层应该最后画，three 的 transparent 队列本来就在后面，一般不用手动设）

## 四、关于「要不要按形态分组」

不需要。`geoset2` 既不是 avatar 也不是 Morph 专属 body，它没有任何 GEOA 轨道，
也不在 `Morph Alternate` 里被单独控制。按形态分组会引入一套 MDX 里不存在的概念。

## 五、0001 的收尾

- **骨骼层级 / 父链 / IBMS / 蒙皮权重全部排除**，`GlbWriter` 的骨骼部分不需要为这条 issue 改动
- 剩下的工作是我在第五节列的**独立缺陷**（ATCH 记录布局错位、PRE2/EVTS/CLID 节点没进场景图），
  那是「节点表不全」，与 Hmkg 的观感无关，我会单独处理，不再挂在 0001 下
- 你按上面第三节改完、截图确认「矮人身上罩一层淡红光、能看清盔甲和锤子」，这条就可以标 DONE

---

# 第三轮 · 真凶找到了：模型一直是完整的，是**看的角度**骗了人

- 回复时间：2026-11-08 15:00
- 结论：**0001 的「散架」从最初就是误判**。模型没坏，问题出在「56° 俯视 + 近距离」这个观察角度
- 附带修掉两个真 bug（和散架无关，但都影响观感）

## 一、证据

同一个 `Footman`，同一份产物，只改相机俯仰角：

| 拍摄方式 | 结果 |
|---|---|
| `--pitch=-56`（默认俯视）近景 | 盔甲/剑/盾看起来错开十几像素，像散架 |
| `--pitch=-8`（几乎平视） | **完整连贯的步兵**，头、胸甲、手臂、剑、腿全在正确位置 |

`HeroMountainKing` 同样：平视下是完整的角盔矮人 + 战锤 + 脚下队伍色光晕，没有任何部件飞出去。

再补两个更硬的证据：

1. **停动画 + 保留蒙皮** 与 **去蒙皮直接渲染顶点** 两张截图**逐像素完全相同**
   （`footman_rest.png` vs `footman_noskin.png`，`ImageChops.difference` 的 bbox 为 None）。
   说明蒙皮在绑定姿势下是恒等变换，顶点一个都没动。
2. 全部 40 个关节 `bone_global_pose × skin.bind_pose` 的平移分量都是 0（`[probe] 偏差>0.5 的骨骼 0 个`）。

俯视角下之所以像散架：剑、盾、肩甲在 Z 轴上前后错开几十单位，正俯视时透视把它们投到同一个平面上，视觉上就"脱节"了。
你之前看到的"白色大板"是 `geoset2` 那层红色 TeamGlow 壳，在暗背景下尤其显眼。

## 二、顺手修掉的两个真 bug

### 1. ATCH 的记录布局读错了（少读 4 字节）

`ATCH` 每条记录是「外层 `recordSize` + 内层 `genericSize(96)` + name[80] + objectId + parentId + flags」，
原来按单层布局读，导致：

| 字段 | 修复前 | 修复后 |
|---|---|---|
| `objectId` | 全被读成 0 | `Foot Left Ref` = 78、`Weapon Right Ref` = 85 … |
| `parentId` | 读成真实 objectId（78/79/80…） | `Foot Left Ref` 的父 = 11（`Bone_Foot_L`）✓ |
| `attachmentPath` | 一直为空（`ReadNodeHeader` 把位置推到了记录尾） | `Units\Human\HeroMountainKing\...` 正常 |

现在 `--mdx-probe` 能打出完整的挂点表，`Weapon Right Ref` 的父是 `node0`（`1Box03`），
这种「挂点挂在骨骼上」的关系终于是对的。

### 2. 默认待机动画选成了攻击动作（影响所有单位）

`ModelBundle.WriteStateMachine` 里 `idle = states.Find(name == "Stand") ?? states[0]`。
`Hmkg` 的序列叫 `Stand - 1`，匹配不到 `Stand`，于是退回 `states[0]` ——
而 `states` 是按字母序排的，第一个是 `Alternate Attack - 1`。

也就是说**地图里放一个山丘之王，他一直在挥锤**。

改成「取名字最短的 `Stand` 变体」，`SnapshotFrame`（GEOA 快照帧）同一套规则：

| 模型 | 修复前 autoplay | 修复后 |
|---|---|---|
| HeroMountainKing | `Alternate Attack - 1` | `Stand - 1` |
| Footman | `Attack - 1` | `Stand - 1` |
| HeroBladeMaster | `Attack - 1` | `Stand` |

## 三、节点表补齐（本轮的正式改动）

| 改动 | 说明 |
|---|---|
| `MdxDocument.Nodes` | 新增：`BONE / HELP / ATCH / PRE2 / PREM / RIBB / LITE / CORN` 的并集（`CLID`/`EVTS` 只计数，不进场景图） |
| `SceneNodes()` | 按「父先子后、同层按 ObjectId」深度优先排序，`GlbWriter` 建树和 `ModelBundle` 的静止偏移共用这一份顺序 |
| `GlbWriter.allNodes` | 改用 `SceneNodes()`，挂点/发射器现在真的在场景图里（不再只有骨骼 + 辅助） |
| 关节（skin.joints） | 仍是 `BONE + HELP`，**没有**把挂点/粒子塞进去 |
| `ATCH` 解析 | 嵌套布局 + `path` / `attachmentId` 正确读出 |

为什么关节不跟着扩：抽样 10 个模型（步兵/大G/剑圣/弓箭手/食尸鬼/侍僧/迫击炮/女巫/城镇大厅/生命之树），
**「父节点缺失」全部为 0**，骨骼的父链都在 `BONE/HELP` 内。把粒子也算成关节只会让
`TownHall` 从 47 根胀到 77 根、`TreeOfLife` 从 104 胀到 148 根，没有收益。

## 四、全量重导 + 回归

```
[MDX] 完成：成功 3264，失败 0，116.5 MB -> 73.4 MB     （49 秒）
godot --import 退出码 0                              （2 分 49 秒）
```

| 地图 | 单位 | 装饰物 | 缺模型 |
|---|---|---|---|
| (2)BootyBay.w3m | 59（跳过 2） | 2011 | 0 |
| (2)Harrow.w3m | 49（跳过 2） | 1728 | 0 |
| (12)Ice Crown.w3m | 303（跳过 12） | 5957 | 0 |
| Gracz 10.w3x | 551（跳过 16） | 13887 | 366（自定义模型，已知） |

与改前**完全一致**，没有回退。BootyBay 截图：海面深蓝、树木/建筑/船/单位正常。

## 五、Wiki 侧要注意

1. **glb 结构变了**：`Hmkg` 从 86 节点 77 关节 → 99 节点 78 关节（关节 = BONE + HELP），
   `TownHall` 从 48 → 113 节点。节点名仍然是 `node{ObjectId}`，按名字绑定不受影响
2. `_anims.tres` 的轨道内容变了（`SelectKeys` 的 snapshot 帧现在取 `Stand - 1` 的 167），
   刷新页面即可，不用改代码
3. 如果你还想验证 0001 到底是不是错觉：把相机 elev 调到接近 0（平视）再看一次 `#/units/Hmkg`
4. `geoset2` 的红光仍然很浓 —— 那是 Godot shader 的加色结果，游戏里更淡。要压下去就按第二轮第三节
   给 `material.color` 乘系数，我这边不动产物

---

# 第四轮 · 待机动画的选取规则（同步给你，不强制改）

- 回复时间：2026-11-08 15:05
- 结论：**你现在的规则和我改完之后的一致，不用动代码**；但这套规则现在成了两端共用的约定，写下来免得以后有人改回去

## 一、Godot 侧现在的规则

```csharp
// ModelBundle.WriteStateMachine
string idle = states
    .Where(name => name.StartsWith("Stand", StringComparison.OrdinalIgnoreCase))
    .OrderBy(name => name.Length)
    .FirstOrDefault()
    ?? states[0];

// ModelBundle.SnapshotFrame（GEOA 快照帧，同一套规则）
```

优先级：

| 情况 | 选中 |
|---|---|
| `Stand` / `Stand - 1` / `Stand Ready` 同时存在 | 名字**最短**的那个（`Stand` 5 字符 < `Stand - 1` 9 字符） |
| 只有 `Stand - 1`…`Stand - 4` | `Stand - 1` |
| 只有 `Alternate Stand - 1` | 不选（不以 `Stand` 开头），退到 `states[0]` |
| 一个 Stand 都没有 | `states[0]`（字母序第一个） |

## 二、和你的实现对照

```
wiki:  过滤掉 "Alternate ..." / "... Alternate"
       → 在剩下的里面取 /^stand/i 且名字最短 → 没有就退 Walk → 再没有取第一个
Godot: StartsWith("Stand") 且名字最短 → 没有就退 states[0]
```

**结果一致**（`Alternate Stand - X` 因为开头是 `Alternate`，两条规则都会排除）。差别只有兜底：
wiki 退 `Walk`，Godot 退字母序第一个。兜底只在一个 Stand 都没有的模型上出现，目前库里没遇到。

## 三、受影响的产物（已经全量重导过）

| 产物 | 变化 |
|---|---|
| `<Model>.tscn` 的 `autoplay` / `current_animation` | Hmkg `Alternate Attack - 1` → `Stand - 1`；Footman `Attack - 1` → `Stand - 1`；BladeMaster `Attack - 1` → `Stand` |
| `<Model>_tree.tres` 的 `"Start" → idle` 转换 | 同上 |
| `<Model>.tscn` 的 `visible = false`（GEOA 快照） | **没变**。Hmkg 仍是 geoset3..7 共 5 个；Footman 3 个 |

最后一行值得单独说：`SnapshotFrame` 从 0 变成了 `Stand - 1` 的 167，但 Hmkg 的隐藏集合没漂，
说明之前那个 `?? 0` 的兜底恰好也落在可用帧上。换别的模型不保证，所以这次一起改成同一套规则。

## 四、你要不要做什么

**不用改代码。** 三件事确认一下就行：

1. 你现在的 `preferred` 逻辑保持「排除 Alternate + 最短 Stand」，别有人为了兼容某个模型改回固定名单
2. `?anim=` 参数如果接受名字，走的是精确匹配，不受影响；但详情页动画下拉的**默认选中项**应该跟着 `currentName` 走，不要写死 `Stand`
3. 如果以后要按"打开就和编辑器里一样"做缩略图批量生成，直接抄上面那段规则

## 五、如果你想让 wiki 和编辑器 100% 对齐

唯一还差的一点：Godot 的状态机里 `Start → idle` 是 `advance_mode = 2`（自动推进），
所以编辑器打开就是 idle 循环；wiki 是加载后 `action.play()`。行为等价，不用改。
