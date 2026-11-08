export const CATEGORIES = [
  { id: "overview", label: "概览", glyph: "◈", group: "浏览", title: "资源总览", sub: "一座只读的资源档案馆：单位、技能、装饰物、音效与贴图，全部直接取自 Godot/assets/converted。" },
  { id: "units", label: "单位", glyph: "◆", group: "资源", title: "单位", sub: "每个单位的历史、数值与真实模型。选中后可以播放动画、浏览技能与音效。" },
  { id: "abilities", label: "技能", glyph: "✦", group: "资源", title: "技能", sub: "技能数值、按钮位置与施法动画名。" },
  { id: "doodads", label: "装饰物", glyph: "❖", group: "资源", title: "装饰物", sub: "树木、岩石与场景道具，按风格聚类。" },
  { id: "destructables", label: "可破坏物", glyph: "▣", group: "资源", title: "可破坏物", sub: "可被摧毁的场景物件与掉落。" },
  { id: "items", label: "物品", glyph: "◇", group: "资源", title: "物品", sub: "掉落物品与商店货架。" },
  { id: "upgrades", label: "升级", glyph: "▲", group: "资源", title: "升级", sub: "科技升级与它们的依赖关系。" },
  { id: "models", label: "模型库", glyph: "▤", group: "媒体", title: "模型库", sub: "按目录浏览全部 glb 模型，含 portrait 版本。" },
  { id: "sounds", label: "音效", glyph: "♪", group: "媒体", title: "音效", sub: "单位语音、战斗音效与环境声。" },
  { id: "textures", label: "贴图", glyph: "▩", group: "媒体", title: "贴图", sub: "地形贴图、图标与界面素材。" },
  { id: "tables", label: "表格", glyph: "▦", group: "数据", title: "表格", sub: "Tables/ 下的原始数据表，可做交叉核对。" },
  { id: "texts", label: "文本", glyph: "≡", group: "数据", title: "文本", sub: "Texts/ 下的本地化字符串与图标字段。" },
];

export const CATEGORY_BY_ID = new Map(CATEGORIES.map((item) => [item.id, item]));

export const PENDING_CATEGORIES = new Set([
  "destructables",
  "items",
  "upgrades",
  "tables",
  "texts",
]);

const filterState = new Map();

export function filtersFor(category) {
  if (!filterState.has(category)) {
    filterState.set(category, { q: "", race: "all", klass: "all", category: "all" });
  }
  return filterState.get(category);
}

export function setFilter(category, key, value) {
  filtersFor(category)[key] = value;
}

export function resetFilters(category) {
  filterState.set(category, { q: "", race: "all", klass: "all", category: "all" });
}

export function categoryMeta(category) {
  return CATEGORY_BY_ID.get(category) || CATEGORY_BY_ID.get("overview");
}
