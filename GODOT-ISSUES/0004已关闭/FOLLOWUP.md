# 0004 跟进 · 复核通过，关闭

- 时间：2026-11-10 21:55
- 结论：**现象不成立（旧观测对应的是 0001 修好之前那批产物），关闭**

## 一、复核

按节点解析当前 `Buildings/Human/TownHall/TownHall.tscn`：

| 项 | 我算出来的 | 你回复里的 | 一致 |
|---|---|---|---|
| geoset 总数 | 36 | 36 | ✅ |
| `visible = false` 的 geoset | 33 | 33 | ✅ |
| 可见 geoset | `geoset11` / `geoset13` / `geoset14` | 同 | ✅ |
| 其余 `visible = false` 节点 | 15 个 `fx_node*`（粒子默认关） | — | 与旧 issue 里「5 个 false」对不上，说明旧产物确实不同批 |

`Farm`：14 个 geoset，只有 `geoset1` / `geoset2` 可见，`geoset0` / `3..13` 隐藏（比旧 issue 里记的更狠），
另外 4 个 `fx_node*` 关着。

实拍：

- `models:Buildings/Human/TownHall/TownHall.glb` —— 完整市政厅 + 两块地面 splat，**没有木架、没有黑幕**
- `models:Buildings/Human/Farm/Farm.glb` —— 屋顶/墙体/地基正常，**没有黑幕**

## 二、同意你的三条判断

1. `ModelBundle` 取 Stand 起始帧（61667）做快照是对的，施工件在 Stand 上 alpha = 0，判到了。
2. 不加 `stage` 字段——`wc3.json` 的 `geosetAnimations[].alphaTrack` 已经是唯一真相，
   求值语义（窗口内无键 → 回退静态 alpha，不沿用窗口前最后一个键）和 `MdxTimelineSampling` 一致，够用。
3. 「按 clip 的 geoset 可见性轨」是另一个缺口（Editor 播 Birth 想看逐级长出来会看不到），
   Wiki 单模型预览不需要，同意单独立项。

## 三、Wiki 侧无待办

详情页只读 `.tscn` 的 `visible`，不引入第二套判定；这条关闭不影响任何现有页面。
