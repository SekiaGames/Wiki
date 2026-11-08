# Issue 0002 · GEOA（geoset 显隐）没有导出

- 状态：OPEN（等待 Godot 侧处理）
- 提交：2026-11-08
- 需要改的端：Godot 导出（`MdxModel` → `GlbWriter`）
- 复现入口：`http://127.0.0.1:8123/Wiki/index.html#/units/Hmkg`

## 现象

wiki 里模型的所有 geoset 默认全显，游戏里不该出现的部件也画出来了：

| 单位 | 多出来的部件 |
|---|---|
| `Hmkg` 山丘之王 | `geoset2`：211×108×211 的石像（avatar 形态） |
| `uaco` 侍僧 | 头顶一块黑色平面 |
| `Hpal` 圣骑士 | 脚下一块深色平面 + 红色光带 |

现在只能靠详情页「模型」标签手动勾掉。

## 现状证据

- `converted` 里的模型产物（`.tscn`）只写了 `surface_material_override/N`，没有任何可见性信息
- `<Model>_anims.tres` 的轨道只有 `position_3d` / `rotation_3d` / `scale_3d` 三类，没有 geoset 节点上的 visibility 轨道
- `MdxModel.cs` 里 `GeosetAlphaAt(geosetIndex, time)` 已经实现了 GEOA 的 KGAO 采样（注释写着"死亡才出现的骸骨/血迹靠这条轨道在 Stand 期间为 0"），但 `GlbWriter` 没有用到

## 期望的产物（任选其一，wiki 都能直接用）

1. **材质透明度**：把 GEOA 的静态 alpha 写进材质（`layer_alpha`），并在 `_anims.tres` 里给 geoset 节点补
   `tracks/N/type = "visibility"` 轨道（Godot 的 Animation 支持 `visibility` 类型，three 侧可映射成
   `material.opacity` / `visible` 的关键帧）
2. **额外的 JSON 提示**：在 `<Model>.wc3.json` 里加一段 `geosetAlpha: [{index, alpha}]` 或
   `hiddenGeosets: [2, 5]`，wiki 用它在加载后设置初始可见性（静态 alpha 足够解决 90% 的情况）

方案 1 更通用（能处理动画中途变化的血雾/尸体），方案 2 改动最小。

## 建议改动

- `MdxModel.cs` 已有 `GeosetAnimations`（`MdxGeosetAnimation`）与 `GeosetAlphaAt()`，数据现成
- `GlbWriter.cs`：写材质时把 `GeosetAlphaAt(geoset, 序列首帧时间)` 写进 `layer_alpha`；
  或按方案 2 输出到 `.wc3.json`

## 验收标准（wiki 侧，只看 converted）

1. `#/units/Hmkg` 默认不再显示被 GEOA 关掉的 geoset（与 `.tscn` 的 `visible = false` 一致）
2. `#/units/uaco` 头顶无黑色平面
3. 游戏里本来就该显示的部件不能少（对照 Godot 编辑器里同一个场景）
4. 详情页「模型」标签仍然可以手动开关每个 geoset

地图级计数不参与 wiki 验收（wiki 只读 `Godot/extract`）。

## 回复区（Godot 侧填写）

处理后在本目录写 `REPLY.md`：采用哪种方案、字段名、wiki 侧需要读取的位置。
