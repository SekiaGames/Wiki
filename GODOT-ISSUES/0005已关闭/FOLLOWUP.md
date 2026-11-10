# 0005 跟进 · Wiki 侧复核通过，关闭

- 时间：2026-11-10 21:55
- 结论：**修复成立，关闭**

## 一、逐条复核（都在当前 `extract.pck` 上重跑）

| 回复里的结论 | 我的复核方式 | 结果 |
|---|---|---|
| `use_base_texture=false` 且非队伍色 = 4 | 扫全部 `.tres`，排除 `use_team_color = true` | **4**，且正好是 `UI/Cursor/{Human,Orc,Undead,NightElf}Cursor.mat1.tres` ✅ |
| `missing_texture.png` 引用 = 0 | 扫全部 `.tres` / `.tscn` | **0** ✅ |
| `wc3.json` 保留 `replaceableId` 并加 `resolvedPath` / `variants` | 读 `AshenTree0.wc3.json` | `replaceableId: 32` + `resolvedPath: …/AshenvaleTree/AshenTree.png` + `variants[A,C]` ✅ |
| 材质真的用上了那张图 | 读 `AshenTree0.mat0.tres` | `use_base_texture = true`，`base_texture = AshenTree.png` ✅ |
| 树不再纯白 | `--shot … models:Doodads/Terrain/AshenTree/AshenTree0.glb` | 棕色树干 + 绿色树冠 ✅ |
| 悬崖/洞穴不再白膜 | `--shot … models:Doodads/Terrain/CliffDoodad/CliffDoodadCave/CliffDoodadCave.glb` | 岩石 + 地面贴图正常 ✅ |
| 单位不回退 | `--shot … models:Units/Human/HeroMountainKing/HeroMountainKing.glb` | 贴图正常，无红色发光壳，姿势完整 ✅ |

## 二、接受你的两处修正

1. **「按目录名反查」不成立**——`AshenTree` 目录同时是灰谷（A）与费伍德（C）两套皮肤，目录名只能定族。
   Wiki 侧以后换肤只用 `variants`，不从 `path` 反推。这条我记在 README 的已知限制里了。
2. **rid 21（UI 光标）保持关闭**比贴 missing_texture 好：原版就是白图，粉红棋盘是假信息。同意，不追。

## 三、Wiki 侧待办（不影响关闭）

- 目前详情页只显示默认皮肤（`resolvedPath`）。要做「按 tileset 换肤」的开关时，读 `variants` 即可，
  `variants` 只出现在 31 / 32 两种 rid 上，33/34/35/36/37 是单套皮肤，不需要给选择器。
- README 的已知限制第 8 条已按本次结果改写（不再是「1401 个白模」，改成「4 个 UI 光标」）。
