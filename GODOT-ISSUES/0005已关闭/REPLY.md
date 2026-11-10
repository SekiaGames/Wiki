# 0005 回复 · 已修，白模数量 1443 → 4

- 回复时间：2026-11-10 21:35
- 结论：**成立，已修并全量重导 + 重打 `extract.pck`**
- 改的端：Extract（`ReplaceableTextures` 新表 + `ModelBundle.TexturePng` + `wc3.json` 的 texture 条目）

## 一、根因

`ModelBundle.TexturePng` 原来只认 `replaceableId` 1 和 2，其余一律落进 `string.IsNullOrEmpty(path) ? null`，于是 `use_base_texture = false`。你查的 1401 个模型、1443 个材质，全部来自这一句。

## 二、映射表不是猜的，来自游戏数据

`Tables/MeleeV0UnitsDestructableData.json` 的 `texID` / `texFile` 两列就是引擎填这个槽的表：

| texID | tileset | texFile |
|---|---|---|
| 31 | L / F,Q / W / W,N | LordaeronSummerTree / …FallTree / …WinterTree / …SnowTree |
| 32 | A / C | AshenvaleTree\AshenTree / AshenvaleTree\FelwoodTree |
| 33 | B | BarrensTree\BarrensTree |
| 34 | N | NorthrendTree\NorthTree |
| 35 | D | Mushroom\MushroomTree |
| 11 | * | Cliff\Cliff0（部分 Cliff1） |

36 / 37（RuinsTree、OutlandMushroomTree）表里没有对应行，按目录名落到
`ReplaceableTextures/RuinsTree/RuinsTree.png`、`ReplaceableTextures/OutlandMushroomTree/MushroomTree.png`。

落到的默认皮肤：

| rid | 默认贴图 |
|---|---|
| 1 | `ReplaceableTextures/TeamColor/TeamColor00` |
| 2 | `ReplaceableTextures/TeamGlow/TeamGlow00` |
| 11 | `ReplaceableTextures/Cliff/Cliff0` |
| 31 | `ReplaceableTextures/LordaeronTree/LordaeronSummerTree` |
| 32 | `ReplaceableTextures/AshenvaleTree/AshenTree` |
| 33 | `ReplaceableTextures/BarrensTree/BarrensTree` |
| 34 | `ReplaceableTextures/NorthrendTree/NorthTree` |
| 35 | `ReplaceableTextures/Mushroom/MushroomTree` |
| 36 | `ReplaceableTextures/RuinsTree/RuinsTree` |
| 37 | `ReplaceableTextures/OutlandMushroomTree/MushroomTree` |

## 三、对你建议里「按目录名反查」的修正

**目录名只能定族，定不了皮肤**，这条必须澄清，不然 Wiki 换肤会换错：

- `Doodads/Terrain/AshenTree` 一个目录同时是灰谷的 `AshenTree`（tileset A）和费伍德的 `FelwoodTree`（tileset C）
- `Doodads/Terrain/LordaeronTree` 一个目录覆盖夏 / 秋 / 冬 / 雪四种
- `Doodads/Terrain/CityscapeTree` 用的是洛丹伦那套皮肤（rid 31），不是城市自己的一套

所以材质只能写一个默认皮肤，真正的皮肤要由地图 tileset 决定。为此 `wc3.json` 的每条 texture 加了两个字段：

```json
{
  "replaceableId": 32,
  "path": "",
  "resolvedPath": "res://extract/ReplaceableTextures/AshenvaleTree/AshenTree.png",
  "variants": [
    { "tileset": "A", "path": "ReplaceableTextures/AshenvaleTree/AshenTree",
      "resolvedPath": "res://extract/ReplaceableTextures/AshenvaleTree/AshenTree.png" },
    { "tileset": "C", "path": "ReplaceableTextures/AshenvaleTree/FelwoodTree",
      "resolvedPath": "res://extract/ReplaceableTextures/AshenvaleTree/FelwoodTree.png" }
  ]
}
```

- `resolvedPath` = 材质里实际写死的那张（默认皮肤）
- `variants` 只在「同 rid 有多套皮肤」时出现（31、32 有；33/34/35 单套不给；11 因为 Cliff0/Cliff1 取决于地形、数据里没给 tileset 对应关系，也不给）
- `replaceableId` 原样保留，没删

你们要换肤就直接用 `variants`，不要从 `path` 反推。

**不需要额外给 `tileset` 字段**：一张地图的 tileset 在地图数据里，模型侧只能给「哪些 tileset 对应哪张图」这张表。

## 四、rid 21 的处理（和 issue 建议第 3 条不同）

`UI/Cursor/{Human,Orc,Undead,NightElf}Cursor.mdx` 的第二层贴图是 `replaceableId = 21`、路径空。库里没有 21 号可替换资产，客户端 `ResolveTexturePath` 的 switch 里也没有 21 分支 → 退回空路径 → 白图。

所以这 4 个模型**保持该层关闭**（`use_base_texture = false`），不贴 `missing_texture.png`。理由：这一层在原版本来就是白图/不显示，贴一张粉红棋盘反而是引入假信息，而且光标模型地图里根本不用。

同类原则：**只有「表里查得到、但磁盘上找不到文件」时才落 `missing_texture.png`**，让缺资源一眼可见。本轮重导后 `missing_texture` 引用数 = **0**，说明所有已知 rid 都落到了真实文件。

## 五、量化

| 指标 | 修前 | 修后 |
|---|---|---|
| `use_base_texture=false` 且非队伍色 | 1443 | **4**（全是 UI 光标） |
| 引用 `missing_texture.png` | — | 0 |
| 受影响模型 | 1401 | 4 |

按 rid 拆的修复量：`11` 334 个、`31` 38、`32` 33、`33` 30、`34` 20、`35` 24、`36` 30、`37` 27，合计 536 个模型；`21` 4 个按上面第四条不动。

## 六、验收（我这边实拍过）

```
Godot --path Wiki -- --shot=/tmp/a.png@models:Doodads/Terrain/AshenTree/AshenTree0.glb
Godot --path Wiki -- --shot=/tmp/b.png@models:Doodads/Terrain/CliffDoodad/CliffDoodadCave/CliffDoodadCave.glb
```

- `AshenTree0`：有树皮 + 绿叶，不再纯白 ✅
- `CliffDoodadCave`：岩石/悬崖贴图正常 ✅
- 单位不回退：`HeroMountainKing`、`Footman` 材质仍是原贴图 + 队伍色 ✅

产物已全量重导 3264 个模型并重新 `--export-pack`，`extract.pck` 是今晚 21:28 的，Wiki 直接跑就能看到。
