# 0003 回复 · 现象成立，同意暂缓；字段草案先定下来

- 回复时间：2026-11-08 14:25
- 结论：**成立**。粒子/ribbon 数据在 MDX 里拿得到，`GlbWriter` 确实一个字节都没输出
- 当前状态：不动手，等 0001 的渲染层改动落地后再一起做（两件事都要动 `GlbWriter`）

## 一、现状核对

`MdxModel` 对这两块只有计数：

```csharp
case "PRE2":
case "PREM":
case "RIBB":
    ReadGenericNodes(...);            // 14:25 这轮新增：节点骨架已经能读到了
    document.Particles += CountRecords(...);
```

也就是说**发射器/丝带的节点位置（objectId / parentId / pivot）现在有了**，
缺的是类型专属参数（发射率、生命区间、颜色、贴图槽、混合模式……）。

Hmkg 实测：`粒子 3 丝带 0`，与旧 sidecar 的 `particles=3` 一致，没有退化。
`--smoke` 的 `33/33` 自检也不受影响。

## 二、产物形态：同意单独出 `<Model>.fx.json`

理由和你写的一致（glTF 没有对应概念），但有三点补充：

1. **要带节点引用**：`node` 用 `node{ObjectId}` 的名字，和 glb 里一致，wiki 直接
   `root.getObjectByName()` 就能挂
2. **贴图路径要提前解析成 converted 里的相对路径**（`.blp` → `.png`），别让 wiki 再做一次大小写/后缀猜
3. **没有粒子的模型不写这个文件**，wiki 侧 `404` 就静默跳过，不要生成空壳

## 三、字段草案（先按这个对齐，实现时可能有微调）

```json
{
  "name": "TorchHuman",
  "particles": [
    {
      "node": "node3",
      "texture": "ReplaceableTextures/GenericGlow/GenericGlow2.png",
      "blend": "add",
      "filterMode": 3,
      "rows": 1, "columns": 1,
      "life": [0.4, 0.6],
      "emissionRate": 30,
      "speed": 0.0,
      "gravity": 0.0,
      "latitude": 0.0,
      "length": 0.0,
      "width": [4.0, 4.0],
      "color": [[1,1,1], [1,0.6,0.2], [0.5,0.1,0.0]],
      "alpha": [1.0, 0.8, 0.0],
      "squirt": false,
      "head": true,
      "tail": true,
      "unshaded": false
    }
  ],
  "ribbons": [
    {
      "node": "node18",
      "texture": "Textures/RibbonBlur1.png",
      "above": 10.0,
      "below": 5.0,
      "alpha": 0.7,
      "color": [1, 1, 1],
      "lifeSpan": 1.2,
      "emissionRate": 20,
      "rows": 1, "columns": 1,
      "gravity": 0.0
    }
  ]
}
```

`color` / `alpha` 是**静态中值**，wiki 侧按关键帧插值就行；`width` 给的是 `[start, end]`。

## 四、前置依赖

这条要动 `GlbWriter` / `ModelBundle` 的输出流程，和 0001 的「节点表补齐 + ATCH 错位」是同一片代码。
建议顺序：**0001 的渲染层改动先落地、全量重导验证通过，再叠 0003**，避免两个改动一起出错时无法二分。

## 五、优先级

同意放在最后。现在 wiki 缺的是「模型本体姿势是否对」（0001），粒子是锦上添花。
另外提醒一句：真正需要粒子的是**装饰物**（火把/火盆/喷泉/瀑布），单位里只有少数带（Hmkg 的 3 个
是攻击特效用的 `BlizParticleBashTrail` 之类），所以验收点建议从**装饰物分类里的火把**和
BootyBay 地图上的火把看，而不是 `#/units/hprt`。

---

# 第二轮 · 对 `FOLLOWUP.md` 的回应

- 回复时间：2026-11-08 14:40
- 结论：**你补的两个字段都收，实现时一起给**

## 一、字段补充

| 字段 | 处理 |
|---|---|
| `node` 为空时挂模型原点 | 收。实现时约定：`node` 省略或空串 = 挂到模型根，`node` 段不写 `null` |
| `ribbon.filterMode` | 收。和 `particle.filterMode` 同义，取值照 MDX 的 `filterMode`（0 opaque / 1 cutout / 2 blend / 3 add） |
| `particle.rows/columns` | 已有，ribbon 也照加 |

另外顺手会带两个 wiki 用不用都无所谓的字段：`priority`（MDX 材质优先级，控制层序）和
`textureSlot`（ribbon 的贴图槽原始值，方便回溯）。

## 二、实现顺序不变

0001 的骨骼部分已经确认不用改（见 `0001/REPLY.md` 第二轮），所以这条的前置依赖只剩
**节点表补齐 + ATCH 错位修复**（原 0001 第五节的独立缺陷）。那批改完 + 全量重导验证通过之后，我再动 `*.fx.json`。

## 三、验收清单我先按你写的三条准备

1. 装饰物分类里的火把 / 火盆 / 喷泉 / 瀑布模型能看到粒子
2. BootyBay 同机位 `--shot` 前后对比，火把有火焰
3. 反例：不带粒子的装饰物（比如纯石头）不生成 `*.fx.json`，wiki 不报错

形态能看出来就够，数值偏差不纠结 —— 这条我也记下了。

---

# 第三轮 · 方案改了：不再出 `.fx.json`，直接翻译成 Godot 节点

- 回复时间：2026-11-08 15:40
- 结论：**`PRE2` 粒子与 `LITE` 灯光已经落地**，产物直接写进 `converted/<Model>.tscn`
- `.fx.json` 计划作废：它唯一的消费者是你，而你已经解析 tscn 了，多一层格式没意义

## 一、产物长什么样

```
TorchHuman.tscn
├── Model                          (glb)
├── geoset0/1                      (材质覆盖)
├── fx_node3                       GPUParticles3D  ← 火焰
│   ├── process_material           ParticleProcessMaterial
│   ├── draw_pass_1                QuadMesh + ShaderMaterial(war3_particle.gdshader)
│   └── metadata/war3_*            原始 MDX 值（filterMode / tailLength / texture…）
└── light_node2                    OmniLight3D
```

父节点是骨骼时会垫一层 `BoneAttachment3D`（Godot 的骨骼不是 `Node3D`），
所以节点路径是 `Model/Skeleton3D/attach_node37/fx_node37`。

## 二、你现在能拿到什么

wiki 侧只要在 `parseModelScene` 里顺手读这两类节点就行：

| 节点 | 关键属性 |
|---|---|
| `GPUParticles3D` | `amount`、`lifetime`、`process_material`（direction / spread / initial_velocity_min/max / gravity）、`draw_pass_1` 的 ShaderMaterial 参数 |
| `OmniLight3D` | `light_color`、`light_energy`、`omni_range` |

ShaderMaterial 里的参数就是渲染所需的全部：

```
particle_texture, color_start/middle/end, alpha_start/middle/end,
scale_start/middle/end, atlas_columns_rows, interval_first, interval_middle, tail_stretch
```

在 three.js 里还原：一个 `THREE.Points` 或 InstancedMesh，按 `v_life` 插值三段颜色/尺寸，
UV 按 `interval_first/middle` 在图集里取帧 —— 和 Godot shader 的逻辑一一对应，照抄即可。

## 三、实测

| 模型 | 结果 |
|---|---|
| `TorchHuman` | 火把顶端黄→橙→红的火苗，高度约柱子一半 |
| `City_Fountain` | 顶部水柱 + 散落水滴（5 个发射器里 2 个在待机帧有发射率） |
| 全库 | **426 个模型带粒子节点，193 个带 OmniLight3D** |
| BootyBay | 59 单位 / 2011 装饰物 / 缺模型 0，与改前一致 |

## 四、三条已知偏差（先记着，别当成 bug 报）

1. **按待机帧静态化**：MDX 的 `KP2E`（发射率）、`KP2S`（速度）这些是动画轨道。
   现在是取 `Stand` 起始帧的值写死。喷泉那种「周期性喷发」会只剩一个相位，
   不是每一秒都在喷。
2. **`squirt` 没做**：按关键帧爆发的发射器（开火、爆裂）目前只生成 `one_shot` 节点，不会按动画触发。
3. **`RIBB` 丝带没做**：瀑布、旗帜飘带仍然缺。需要自己生成带状网格，比粒子麻烦，单独排期。

## 五、给 wiki 的建议

- 先按 tscn 里的 `GPUParticles3D` 做，不用再等 `*.fx.json`
- 如果 three.js 那边嫌 `ParticleProcessMaterial` 的参数太 Godot 味，直接读 `metadata/war3_*`
  里的原始值（`filterMode`、`headOrTail`、`tailLength`、`texture`）更方便
- 没有 `fx_node*` 的模型就是没有粒子，不需要兜底
