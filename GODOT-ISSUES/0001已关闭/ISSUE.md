# Issue 0001 · glb 骨骼层级不完整，部分模型蒙皮错位

- 状态：OPEN（等待 Godot 侧处理）
- 提交：2026-11-08
- 需要改的端：Godot 导出（`MdxModel` / `GlbWriter`），wiki 侧无解
- 复现入口：`http://127.0.0.1:8123/Wiki/index.html#/units/Hmkg`

## 现象

山丘之王（`Hmkg`）在 wiki 预览里散成碎片：护甲、锤子、身体各自飘开。同机位下这些单位正常：

| 单位 | 结果 |
|---|---|
| `Hpal` 圣骑士 | 正常（行走姿势、锤子在手） |
| `hfoo` 步兵 | 正常 |
| `Hamg` 大魔法师 | 正常（骑乘 + 法师） |
| `uaco` 侍僧 | 正常 |
| `Hmkg` 山丘之王 | **散架** |

## 复现步骤

1. `python Wiki/tools/serve.py`（或双击 `Wiki/Run.bat`）
2. 打开 `#/units/Hmkg`
3. 尝试这些参数确认不是动画/材质问题：
   - `?noanim=1`（完全不播动画）→ 仍然散架
   - `?noSkin=1`（用普通 Mesh 替换 SkinnedMesh）→ 原始网格是一团紧凑的块，说明顶点必须靠骨骼展开
   - `?hide=geoset2`（关掉 211×108×211 的石像形态）→ 少一块，其余照旧

## 已排除

| 检查 | 结论 |
|---|---|
| `inverseBindMatrices` 与节点 `translation` 是否自洽 | 77 个 joint 全部满足 `world × IBM = I`，偏差 0.000 |
| 动画轨道能否绑到节点 | `_anims.tres` 引用 57 个节点，glb 里 86 个节点全部包含，未绑定轨道 0 条 |
| 材质 / 贴图 | 其它单位同一套解析正常；Hmkg 的 8 个 geoset 都能取到材质 |
| three.js 加载器 | 同加载器对上述 4 个单位正常 |
| 是否绑定姿势本身有问题 | 关掉动画仍然散架 |

## 根因判断

`Hmkg.glb` 的 `scene.nodes` 有 **8 个根骨骼**（`node0..7`），77 个 joint 分挂在 8 棵子树上；
`Hpal.glb` 也有多个根但渲染正常，所以根数不是直接原因。

对照客户端实现 `D:/Open-Warcraft3/GameCore/L4_Composition/Game/Rendering/MdxLoader.cs` 第 57–72 行：

```csharp
case "BONE": ParseBones(r, m, end); break;
case "HELP": ParseHelpers(r, m, end); break;
case "LITE": ParseLights(r, m, end); break;
// MDX 的空间层级不只包含 BONE/HELP。狂战士武器骨骼会以
// ParticleEmitter/Attachment 等 GenericObject 为父节点；这些节点即使
// 本身不绘制，也必须参与骨骼 world matrix 的递归计算。
case "PREM":
case "CORN": ParseNestedGenericNodes(r, m, end); break;
case "ATCH": ParseAttachments(r, m, end); break;
case "RIBB": ParseRibbonEmitters(r, m, end); break;
case "PRE2": ParseParticleEmitters2(r, m, end); break;
case "EVTS": ParseEventObjectNodes(r, m, end); break;
case "CLID": ParseCollisionShapeNodes(r, m, end); break;
```

本仓库的差距：

| 文件 | 现状 |
|---|---|
| `Godot/src/Core/Formats/Mdx/GlbWriter.cs` | `allNodes = Bones + Helpers` 之后才建父子关系，其它类型不参与 |
| `Godot/src/Core/Formats/Mdx/MdxModel.cs` | `ATCH` / `EVTS` 读成了节点列表，但 `GlbWriter` 没用；`PRE2` / `PREM` / `RIBB` / `LITE` / `CLID` / `CORN` 只做了 `CountRecords`，`ObjectId` / `ParentId` 根本没读 |

只要某根骨骼的父节点是发射器 / 挂点 / 灯光 / 碰撞体，这条父链就断在根上，子树整体失去父偏移 → 蒙皮散架。

## 建议改动

1. **`MdxModel.cs`**：把 `PREM` / `CORN` / `PRE2` / `RIBB` / `LITE` / `CLID` / `BPOS` 也按 GenericObject 头
   （`name[80] + objectId(u32) + parentId(u32) + flags(u32)`）解析成 `MdxNode`，与现有的
   `Bones` / `Helpers` / `Attachments` / `EventObjects` 一起放进统一的 `Nodes` 列表；
   各类型自己的专属字段（比如粒子的发射率、灯光的颜色）可以继续不解析，这里只要节点骨架。
2. **`GlbWriter.cs`**：`allNodes` 改用 `document.Nodes`（含所有类型），节点层级、静止世界位置
   （IBMS）、动画都基于它。
3. 节点名 `node{ObjectId}` 保持不变，wiki 侧按名字绑动画，不需要改。

参考实现：`Open-Warcraft3/.../MdxLoader.cs` 的 `ParseGenericNode()`（读 name/objectId/parentId/flags）。

## 验收标准（wiki 侧，只看 converted）

1. `Hmkg` 平视（`?elev=0.1`）是完整矮人，盔甲/锤子/腿都在位，不再出现「零件脱节」
2. `Hpal` / `hfoo` / `Hamg` / `uaco` 不回退，装饰物（静态模型）不受影响
3. wiki 里 3264 个 glb 全部可加载：`#/models` 计数不变，抽查 20 个能出画面
4. `<Model>_anims.tres` 的动画仍能全部绑定（控制台无「未绑定轨道」告警）

地图级计数（`--map-report` / `--shot`）属于 **Godot 侧自查**，不作为 wiki 的验收项 ——
wiki 只读 `Godot/extract`，与地图文件无关。

## 验证用命令

```python
# 看根节点数与节点/骨骼总数（当前：17 个根、86 节点、77 joint）
import json, struct
path = 'Godot/extract/Units/Human/HeroMountainKing/HeroMountainKing.glb'
data = open(path, 'rb').read()
length = struct.unpack_from('<I', data, 12)[0]
gltf = json.loads(data[20:20 + length])
print('scene roots:', gltf['scenes'][0]['nodes'])
print('nodes:', len(gltf['nodes']), 'joints:', len(gltf['skins'][0]['joints']))
```

```
# wiki 侧复现
http://127.0.0.1:8123/Wiki/index.html#/units/Hmkg
http://127.0.0.1:8123/Wiki/index.html?noanim=1#/units/Hmkg
```

修好后预期：`scene roots` 只剩 1~2 项，`#/units/Hmkg` 能看到完整的矮人。

## 回复区（Godot 侧填写）

处理后请在本目录写 `REPLY.md`：改了哪些文件、根节点数前后对比、是否需要重跑 `extract.bat` /
`--headless --import` / `export.bat`。
