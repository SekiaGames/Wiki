# Issue 0005 · 依赖 replaceableId 的贴图没有路径，模型在 Wiki 里是白模

- 状态：OPEN
- 提交：2026-11-10 20:55
- 需要改的端：Extract（`ModelBundle` 的 TEXS 解析与材质 `use_base_texture` 判定）
- 复现入口：`Godot --path Wiki -- --shot=/tmp/a.png@models:Doodads/Terrain/AshenTree/AshenTree0.glb`

## 现象

`Doodads/Terrain/AshenTree/AshenTree0` 在 Wiki 里整棵是纯白，没有任何贴图。同目录 `.tscn` / `.tres` 都能正常加载，
不报错、不是缺 shader，是材质根本没启用第一层贴图。

## 证据

1. `AshenTree0.mat0.tres`：

   ```
   shader_parameter/use_base_texture = false
   shader_parameter/use_team_color = false
   ```

   材质里没有任何 `base_texture` 参数——导出端没有拿到可用的贴图路径，于是把这一层关掉了。

2. `AshenTree0.wc3.json` 的 `textures`：

   ```json
   [{"replaceableId": 32, "path": ""}]
   ```

   MDX 里这一层是「可替换贴图」，`path` 为空、只有 `replaceableId=32`（tileset 树贴图）。导出端按空路径处理 → 无贴图。

3. 全库统计（3264 个 `.wc3.json`）：

   | 情况 | 模型数 |
   |---|---|
   | 至少一层「有 replaceableId 且 path 为空」 | **1401** |
   | rid 计数 | `1`:680、`2`:522、`11`:334、`31`:38、`32`:33、`36`:30、`33`:30、`37`:27、`35`:24、`34`:20、`21`:4 |

   即 43% 的模型受影响。`1`（队伍色）与 `2`（队伍光晕）在 Wiki 侧另有处理，真正表现为白模的是 `11` 与 `31..37`。

## 客户端怎么解的（权威参考）

`test/Open-Warcraft3/GameCore/L4_Composition/Game/Rendering/CharacterRenderer.cs:2319` `ResolveTexturePath`：

```
1  => ReplaceableTextures\TeamColor\TeamColor{tt}.blp
2  => ReplaceableTextures\TeamGlow\TeamGlow{tt}.blp
11 => ReplaceableTextures\Cliff\Cliff0.blp          // 地图有实际悬崖贴图时由 _replaceablePaths[11] 覆盖
31 => ReplaceableTextures\LordaeronTree\LordaeronFallTree.blp   // MountainGiant 手持树
_  => t.Path                                          // 空 → 白纹理
```

`31..37` 这几个 tileset 树/悬崖 id 走的是 `_replaceablePaths`（由地形 tileset 在运行时注入），
所以客户端有贴图、导出端没有。

贴图本体在库里是齐的，只是需要一层映射：

```
ReplaceableTextures/AshenvaleTree/AshenTree.png
ReplaceableTextures/LordaeronTree/LordaeronSummerTree.png / ...FallTree / ...WinterTree / ...SnowTree
ReplaceableTextures/Cliff/A_cliff0.png / B_Cliff0.png / ...
ReplaceableTextures/TeamColor/TeamColor00.png
```

## 建议改动（Extract 侧）

1. `wc3.json` 里保留 `replaceableId`（已有，别删）——这是 Wiki / Editor 以后做 tileset 替换的唯一依据。
2. 材质导出时，若该层 `path` 为空但 `replaceableId` 非 0，按客户端同一张表写入兜底贴图路径，
   再照常 `use_base_texture = true`：
   - `1/2` → `ReplaceableTextures/TeamColor(or TeamGlow)/TeamColor{tt}.png`，`tt` 取 00（Wiki 再按队伍色覆盖）
   - `11` → `ReplaceableTextures/Cliff/` 里任一可用 `*_cliff0.png`（或 `Cliff0` 若存在）
   - `31..37` → 该 doodad 所属 tileset 的树贴图。doodad 类型在 `DoodadsDoodads.json` 里与 `file` 一一对应
     （`AshenTree` → `AshenvaleTree/AshenTree`），可以按模型所在目录名反查，不必逐 id 硬编码
3. 反查不到时**不要**把 `use_base_texture` 置 false，改成 `true` + `assets/fallback/missing_texture.png`，
   这样 Wiki 里一眼能看出「缺贴图」而不是「白模」。

## 验收标准（Wiki 侧）

1. `models:Doodads/Terrain/AshenTree/AshenTree0.glb` 有树皮/树叶贴图，不再是纯白
2. `Doodads/Terrain/LordaeronTree/*`、`Doodads/Terrain/BarrensTree/*`、`Doodads/Terrain/NorthrendTree/*` 同样有色
3. `ReplaceableId 11` 的洞穴/桥梁（`Doodads/Terrain/CliffDoodadCave*`、城市桥梁）不再是白膜
4. 单位不回退：`Hmkg` 队伍色布料 / 皮革仍是队伍色，`TeamGlow` 仍按 wiki 规则隐藏
5. 重导后 `--pack` 出的 `extract.pck` 时 Wiki 侧 1401 个模型里白模数量降到 0（或仅剩真正缺资源的）

## 回复区（Godot 侧填写）

处理后在本目录写 `REPLY.md`：说明 `31..37` 是按目录名反查还是按 tileset 表映射、
以及是否需要在 `.wc3.json` 里额外给出 `tileset` 字段供 Wiki 切换皮肤。
