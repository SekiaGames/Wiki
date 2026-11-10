# 0004 回复 · 木架在当前产物里已经不存在了，这条可以关

- 回复时间：2026-11-10 21:35
- 结论：**现象不成立（对你的旧观测而言）**。`extract.pck`（今天 20:07 重导那一批）里 TownHall 的 Stand 只显示 3 个 geoset，整圈木架是 Birth 施工阶段的件，Stand 帧上 alpha 本来就是 0
- 需要改的端：**Extract 不需要改**，改的是你对产物的读法（或者说是这批产物的时间点）

## 一、先对一下版本

你写 issue 时看到的「36 个 geoset 里只有 5 个 `visible = false`」，和现在对不上。当前 `Buildings/Human/TownHall/TownHall.tscn` 是 **33 个 `visible = false`**，只有 geoset11 / 13 / 14 可见。

我直接解了 `TownHall.mdx` 的 GEOA 原始字节（36 条记录，每条一条 KGAO 轨），按序列求值，结果如下：

| clip | 区间 | 可见 geoset | 说明 |
|---|---|---|---|
| Birth | 0–60000 | 4、5、6（7/8/9/10 在 15000/30000/45000 依次出现） | 施工阶段件，就是你说的「木架」 |
| Stand | 61667–62000 | 11、13、14 | geo11 = 建筑主体（bbox 359×497×366），13/14 = 贴地 splat |
| Portrait | 68333–71667 | 1、11、13、14 | geo1 = 竖直大面片（TeamGlow，就是那个黑幕） |
| Birth Upgrade First | 100000–160000 | 11、13、14、15 | 二级升级 |
| Birth Upgrade Second | 165333–225333 | 11、13、14、28、29 | 三级升级 |
| Death | 263333–266500 | 2、3、35 | |

也就是说 ModelBundle 的快照判定是对的：**它取 Stand 起始帧（61667），只留主体，把施工件全关了**。你现在实拍应该看不到木架：

```
Godot --path Wiki -- --shot=/tmp/th.png@models:Buildings/Human/TownHall/TownHall.glb
```

画面是完整市政厅 + 两块地面 splat，没有木架、没有黑幕。

## 二、回答你 issue 里问的两个问题

1. **木架 geoset 的 GEOA 是什么？** 它们在 Stand 帧 alpha = 0，`ModelBundle` 的判定覆盖到了，没有漏。
2. **是不是「建造/升级阶段件，静态预览本来就会全显」？** 不是。当前产物**不是**「全显」，而是按 Stand 单帧快照显隐。你之前看到全显，是因为那批产物的待机动画选错了（0001 里修掉的「默认待机动画匹配不到 Stand」），选到了别的序列，快照帧落在施工阶段里。

## 三、`stage` 字段：不需要新增

你要的「阶段分组」在 `wc3.json` 里已经全了，别再引入第二个真相来源（和 0002 的约定一致）：

```json
"geosetAnimations": [
  { "geosetId": 4, "alpha": 1, "alphaTrack": { "interp": 0, "keys": [[0,1],[61667,0],[62667,0], ...] } }
]
```

求值语义（和客户端 `MdxTimelineSampling.FindKeys` 一致）：

- 只取落在 `[sequenceStart, sequenceEnd]` 窗口内的键
- 窗口内一个键都没有 → 回退到记录里的静态 `alpha`（通常是 1），**不是**沿用窗口前最后一个键
- 窗口内有键 → 取最后一个不大于当前帧的键值（`interp = 0`，步进）

据此算 TownHall 的 Stand：geo4/5/6 因为 61667 处有键且值为 0 → 隐藏；geo11 在窗口内没有键 → 回退 1 → 可见。和你看到的画面一致。

## 四、一个真实的缺口（不在本条范围内）

当前的导出是 **Stand 单帧静态快照**，没有按 clip 的 geoset 可见性动画轨。所以：

- Wiki 单模型预览：够用
- Editor 里播放 Birth 想看到「一级建造 → 二级 → 三级」逐批长出来：看不到，施工件永远是隐藏的

要做的话是在 `.tscn` 里给 `geosetN` 补 `visible` 轨（和 `fx_nodeNN` 现在那套一样），这条我单独立项，不塞进 0004。

## 五、验收

1. `#/models/Buildings/Human/TownHall/TownHall.glb` 默认无木架 —— **已达成**
2. `Farm` 无黑幕 —— 已达成
3. 单位侧不回退：`Hmkg` / `Footman` 抽查正常（本轮重导后实拍过）
4. 「模型」标签 geoset 勾选状态与实际可见性一致 —— 你那边读 `tscn` 的 `visible` 即可，33 个 false
