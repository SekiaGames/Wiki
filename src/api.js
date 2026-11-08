// 数据接口层：全部走 serve.py 的只读接口，运行时组装，不落盘。
// 所有路径解析都基于 /api/asset/index 返回的真实文件名，不猜大小写。

import { parseAnimationLibrary } from "./godot/anim.js";
import { parseMaterial, parseModelScene } from "./godot/mat.js";

export const ASSET_BASE = "/Godot/assets/converted/";

export const backend = { mode: "live", label: "实时读取" };

export function assetUrl(relative) {
  if (!relative) return "";
  return ASSET_BASE + String(relative).replace(/\\/g, "/").replace(/^\/+/, "");
}

export function iconUrl(iconPath) {
  if (!iconPath) return "";
  return assetUrl(String(iconPath).replace(/\.(blp|tga)$/i, ".png"));
}

const cache = new Map();

async function getJson(url) {
  const response = await fetch(url, { cache: "no-cache" });
  if (!response.ok) throw new Error(`HTTP ${response.status} ${url}`);
  return response.json();
}

const api = {
  read: (path) => getJson(`/api/asset/read?path=${encodeURIComponent(path)}`),
  list: (path, depth = 1) =>
    getJson(`/api/asset/list?path=${encodeURIComponent(path)}&depth=${depth}`),
  index: (ext) => getJson(`/api/asset/index?ext=${encodeURIComponent(ext)}`),
  async batch(paths) {
    const out = {};
    for (let i = 0; i < paths.length; i += 40) {
      const slice = paths.slice(i, i + 40);
      const data = await getJson(`/api/asset/batch?paths=${encodeURIComponent(slice.join(","))}`);
      Object.assign(out, data.items || {});
    }
    return out;
  },
};

// ---------- 通用工具 ----------

function tableRows(table) {
  if (!table) return [];
  const { columns, rows } = table;
  return rows.map((row) => {
    const item = {};
    columns.forEach((column, index) => {
      item[column] = row[index];
    });
    return item;
  });
}

function num(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function splitList(value) {
  if (!value || value === "-" || value === "_") return [];
  return String(value)
    .split(",")
    .map((part) => part.trim())
    .filter((part) => part && part !== "-" && part !== "_");
}

function baseName(wc3Path) {
  return String(wc3Path).replace(/\\/g, "/").split("/").pop();
}

const RACE_MAP = {
  human: "human",
  orc: "orc",
  undead: "undead",
  nightelf: "nightelf",
};

function normalizeRace(rawRace, wc3Path) {
  const race = String(rawRace || "").toLowerCase();
  if (RACE_MAP[race]) return RACE_MAP[race];
  const fromPath = String(wc3Path).toLowerCase().split("\\")[1] || "";
  return RACE_MAP[fromPath] || "neutral";
}

// ---------- 文本表 ----------

async function loadTextFamily(patterns) {
  const listing = await api.list("Texts", 1);
  const files = (listing.entries || [])
    .filter((entry) => !entry.dir && patterns.some((pattern) => pattern.test(entry.name)))
    .map((entry) => entry.path);
  const batch = await api.batch(files);
  const merged = new Map();
  for (const item of Object.values(batch)) {
    if (!item.ok || !item.data?.keys) continue;
    for (const [key, value] of Object.entries(item.data.keys)) {
      if (!merged.has(key)) merged.set(key, value);
    }
  }
  return merged;
}

// ---------- 单位数据集 ----------

let unitDatasetPromise = null;

export function unitDataset() {
  if (!unitDatasetPromise) unitDatasetPromise = buildUnitDataset();
  return unitDatasetPromise;
}

async function buildUnitDataset() {
  const tablePaths = [
    "Tables/UnitUI.json",
    "Tables/UnitData.json",
    "Tables/UnitBalance.json",
    "Tables/UnitAbilities.json",
  ];
  const [tables, strings, funcs] = await Promise.all([
    api.batch(tablePaths),
    loadTextFamily([/UnitStrings\.json$/i]),
    loadTextFamily([/UnitFunc\.json$/i]),
  ]);

  const uiRows = tableRows(tables["Tables/UnitUI.json"]?.data);
  const dataById = new Map(
    tableRows(tables["Tables/UnitData.json"]?.data).map((row) => [row.unitID, row]),
  );
  const balanceById = new Map(
    tableRows(tables["Tables/UnitBalance.json"]?.data).map((row) => [row.unitBalanceID, row]),
  );
  const abilById = new Map(
    tableRows(tables["Tables/UnitAbilities.json"]?.data).map((row) => [row.unitAbilID, row]),
  );

  const rows = [];
  for (const ui of uiRows) {
    const id = ui.unitUIID;
    if (!id) continue;
    const data = dataById.get(id) || {};
    const balance = balanceById.get(id) || {};
    const abil = abilById.get(id) || {};
    const modelBase = baseName(ui.file || "");
    const heroAbilities = splitList(abil.heroAbilList);
    const isBuilding = String(balance.isbldg) === "1";
    const icon = funcs.get(`${id}.Art`) || "";

    rows.push({
      id,
      name: strings.get(`${id}.Name`) || data["comment(s)"] || modelBase || id,
      en: data["comment(s)"] || modelBase || id,
      race: normalizeRace(data.race, ui.file),
      klass: isBuilding ? "building" : heroAbilities.length ? "hero" : "unit",
      level: heroAbilities.length ? null : num(balance.level),
      hp: num(balance.realHP ?? balance.HP),
      mana: num(balance.realM ?? balance.manaN),
      armor: num(balance.realdef ?? balance.def),
      speed: num(balance.spd),
      sight: num(balance.sight),
      gold: num(balance.goldcost),
      lumber: num(balance.lumbercost),
      iconPath: icon,
      iconUrl: iconUrl(icon),
      modelBase,
      modelWc3: ui.file || "",
      heroAbilities,
      abilities: splitList(abil.abilList),
      tip: strings.get(`${id}.Ubertip`) || "",
      hotkey: strings.get(`${id}.Hotkey`) || "",
      raw: { unitUI: ui, unitData: data, unitBalance: balance, unitAbilities: abil },
    });
  }

  return { rows, byId: new Map(rows.map((row) => [row.id, row])) };
}

// ---------- 技能数据集 ----------

let abilityDatasetPromise = null;

export function abilityDataset() {
  if (!abilityDatasetPromise) abilityDatasetPromise = buildAbilityDataset();
  return abilityDatasetPromise;
}

async function buildAbilityDataset() {
  const [tablePayload, strings, funcs] = await Promise.all([
    api.read("Tables/AbilityData.json"),
    loadTextFamily([/AbilityStrings\.json$/i]),
    loadTextFamily([/AbilityFunc\.json$/i]),
  ]);
  const table = tablePayload?.json;

  const rows = [];
  for (const row of tableRows(table)) {
    const id = row.alias || row.code;
    if (!id) continue;
    const animNames = splitList(funcs.get(`${id}.Animnames`));
    const icon = funcs.get(`${id}.Art`) || "";
    rows.push({
      id,
      name: strings.get(`${id}.Name`) || row["comment(s)"] || id,
      en: row["comment(s)"] || id,
      race: row.race || "",
      levels: num(row.levels),
      iconPath: icon,
      iconUrl: iconUrl(icon),
      pos: funcs.get(`${id}.Buttonpos`) || funcs.get(`${id}.ButtonPos`) || "",
      anim: animNames[0] || "Spell",
      cool: num(row.Cool1),
      cost: num(row.Cost1),
      tip: strings.get(`${id}.Ubertip`) || "",
      raw: row,
    });
  }
  return { rows, byId: new Map(rows.map((row) => [row.id, row])) };
}

// ---------- 目录 / 模型解析 ----------

let glbIndexPromise = null;

function glbIndex() {
  if (!glbIndexPromise) {
    glbIndexPromise = api.index(".glb").then((payload) => {
      const map = new Map();
      for (const file of payload.files || []) {
        const name = file.split("/").pop().replace(/\.glb$/i, "").toLowerCase();
        if (!map.has(name)) map.set(name, []);
        map.get(name).push(file);
      }
      return map;
    });
  }
  return glbIndexPromise;
}

async function resolveModelDir(modelBase) {
  const index = await glbIndex();
  const target = String(modelBase).toLowerCase();
  const candidates = index.get(target) || [];
  if (!candidates.length) return null;
  const preferred =
    candidates.find((path) => path.split("/").pop().toLowerCase() === target) || candidates[0];
  return { dir: preferred.slice(0, preferred.lastIndexOf("/")), file: preferred };
}

function soundKind(fileName) {
  const name = fileName.replace(/\.wav$/i, "");
  if (/YesAttack\d*$/i.test(name)) return "YesAttack";
  if (/Attack\d*$/i.test(name)) return "Attack";
  if (/Pissed\d*$/i.test(name)) return "Pissed";
  if (/Warcry\d*$/i.test(name)) return "Warcry";
  if (/Ready\d*$/i.test(name)) return "Ready";
  if (/What\d*$/i.test(name)) return "What";
  if (/Yes\d*$/i.test(name)) return "Yes";
  if (/Death$/i.test(name)) return "Death";
  if (/Birth$/i.test(name)) return "Birth";
  return "Other";
}

async function loadModel(resolved) {
  const listing = await api.list(resolved.dir, 1);
  const names = (listing.entries || []).filter((entry) => !entry.dir).map((entry) => entry.name);
  const base = resolved.file.split("/").pop().replace(/\.glb$/i, "");
  const scenePath = `${resolved.dir}/${base}.tscn`;
  const animsPath = `${resolved.dir}/${base}_anims.tres`;

  const batch = await api.batch([scenePath, animsPath]);
  const sceneText = batch[scenePath]?.ok ? batch[scenePath].data : "";
  const scene = sceneText
    ? parseModelScene(sceneText)
    : { modelPath: resolved.file, animsPath, slots: [] };

  const materialPaths = [...new Set(scene.slots.map((slot) => slot.materialPath).filter(Boolean))];
  const matBatch = materialPaths.length ? await api.batch(materialPaths) : {};

  const slots = scene.slots.map((slot) => ({
    node: slot.node,
    materialPath: slot.materialPath,
    visible: slot.visible !== false,
    material: matBatch[slot.materialPath]?.ok ? parseMaterial(matBatch[slot.materialPath].data) : null,
  }));

  const animsText = batch[animsPath]?.ok ? batch[animsPath].data : "";
  const clips = animsText ? parseAnimationLibrary(animsText) : [];

  return {
    glb: scene.modelPath || resolved.file,
    dir: resolved.dir,
    clips,
    slots,
    hidden: scene.hidden || [],
    missingMaterials: slots.filter((slot) => !slot.material).length,
    fileNames: names,
  };
}

// ---------- 装饰物数据集 ----------

let doodadDatasetPromise = null;

export function doodadDataset() {
  if (!doodadDatasetPromise) doodadDatasetPromise = buildDoodadDataset();
  return doodadDatasetPromise;
}

async function buildDoodadDataset() {
  const [table, world] = await Promise.all([
    api.read("Tables/Doodads.json"),
    loadTextFamily([/WorldEditStrings\.json$/i]),
  ]);

  const rows = [];
  for (const row of tableRows(table.json)) {
    const file = String(row.file || "").replace(/\\/g, "/");
    if (!file) continue;
    const slash = file.lastIndexOf("/");
    const dir = slash >= 0 ? file.slice(0, slash) : "";
    const base = slash >= 0 ? file.slice(slash + 1) : file;
    rows.push({
      id: row.doodID,
      name: world.get(`WorldEditStrings.${row.Name}`) || row.comment || base,
      en: row.comment || base,
      category: row.category || "",
      tilesets: String(row.tilesets || ""),
      dir,
      base,
      numVar: Math.max(1, Number(row.numVar) || 1),
      scale: Number(row.defScale) || 1,
      walkable: String(row.walkable) === "1",
      klass: "doodad",
      race: "all",
      iconUrl: "",
    });
  }
  return { rows, byId: new Map(rows.map((row) => [row.id, row])) };
}

async function resolveDoodadVariants(row) {
  const index = await glbIndex();
  const variants = [];
  for (let i = 0; i < row.numVar; i += 1) {
    const suffix = row.numVar > 1 ? String(i) : "";
    const names = [`${row.base}${suffix}`];
    if (row.numVar === 1) names.push(`${row.base}0`, row.base);
    for (const name of names) {
      const paths = index.get(name.toLowerCase()) || [];
      const preferred = paths.find((path) => path.startsWith(`${row.dir}/`)) || paths[0];
      if (preferred) {
        const dir = preferred.slice(0, preferred.lastIndexOf("/"));
        const base = preferred.split("/").pop().replace(/\.glb$/i, "");
        variants.push({ index: i, glb: preferred, dir, base });
        break;
      }
    }
  }
  return variants;
}

// ---------- 对外接口 ----------

export async function listUnits(filters = {}) {
  const { rows } = await unitDataset();
  let items = rows;
  if (filters.race && filters.race !== "all") items = items.filter((row) => row.race === filters.race);
  if (filters.klass && filters.klass !== "all") items = items.filter((row) => row.klass === filters.klass);
  if (filters.q) {
    const q = filters.q.toLowerCase();
    items = items.filter(
      (row) =>
        row.id.toLowerCase().includes(q) ||
        String(row.name).toLowerCase().includes(q) ||
        String(row.en).toLowerCase().includes(q),
    );
  }
  return { items: items.map(toCard), total: rows.length, stubbed: false };
}

function toCard(row) {
  return {
    id: row.id,
    name: row.name,
    en: row.en,
    race: row.race,
    klass: row.klass,
    iconUrl: row.iconUrl,
    stats: {
      hp: row.hp ?? undefined,
      armor: row.armor ?? undefined,
      speed: row.speed ?? undefined,
    },
    skills: row.heroAbilities,
  };
}

export async function listAbilities(filters = {}) {
  const { rows } = await abilityDataset();
  let items = rows;
  if (filters.q) {
    const q = filters.q.toLowerCase();
    items = items.filter(
      (row) =>
        row.id.toLowerCase().includes(q) ||
        String(row.name).toLowerCase().includes(q) ||
        String(row.en).toLowerCase().includes(q),
    );
  }
  return {
    items: items.map((row) => ({
      id: row.id,
      name: row.name,
      en: row.en,
      klass: "ability",
      race: "all",
      iconUrl: row.iconUrl,
      pos: row.pos,
      stats: row.levels ? { 等级: row.levels } : undefined,
    })),
    total: rows.length,
    stubbed: false,
  };
}

export async function listDoodads(filters = {}) {
  const { rows } = await doodadDataset();
  let items = rows;
  if (filters.category && filters.category !== "all") {
    items = items.filter((row) => row.category === filters.category);
  }
  if (filters.q) {
    const q = filters.q.toLowerCase();
    items = items.filter(
      (row) =>
        row.id.toLowerCase().includes(q) ||
        String(row.name).toLowerCase().includes(q) ||
        String(row.en).toLowerCase().includes(q),
    );
  }
  return {
    items: items.map((row) => ({
      id: row.id,
      name: row.name,
      en: row.en,
      klass: "doodad",
      race: "all",
      iconUrl: "",
      kind: row.category,
      stats: { 变体: row.numVar },
    })),
    total: rows.length,
    stubbed: false,
  };
}

export async function listEmpty(category) {
  return { items: [], total: 0, stubbed: false, note: `${category} 尚未接入。` };
}

export async function getUnitDetail(id) {
  const { byId } = await unitDataset();
  const row = byId.get(id);
  if (!row) return null;

  const resolved = row.modelBase ? await resolveModelDir(row.modelBase) : null;
  const model = resolved ? await loadModel(resolved) : null;

  let sounds = [];
  if (model) {
    sounds = model.fileNames
      .filter((name) => /\.wav$/i.test(name))
      .map((name) => ({ file: name, kind: soundKind(name), url: assetUrl(`${model.dir}/${name}`) }));
    delete model.fileNames;
  }

  let skills = [];
  if (row.heroAbilities.length) {
    const { byId: abilityById } = await abilityDataset();
    skills = row.heroAbilities
      .map((skillId) => abilityById.get(skillId))
      .filter(Boolean)
      .map((skill) => ({
        id: skill.id,
        name: skill.name,
        iconUrl: skill.iconUrl,
        pos: skill.pos,
        anim: skill.anim,
        tip: skill.tip,
      }));
  }

  return {
    ...row,
    model,
    sounds,
    skills,
    animations: model ? model.clips.map((clip) => clip.name) : [],
    stubbed: false,
  };
}

export async function getAbilityDetail(id) {
  const { byId } = await abilityDataset();
  const row = byId.get(id);
  if (!row) return null;
  return { ...row, klass: "ability", animations: [row.anim], sounds: [], model: null, stubbed: false };
}

export async function getDoodadDetail(id, variantIndex = 0) {
  const { byId } = await doodadDataset();
  const row = byId.get(id);
  if (!row) return null;

  const variants = await resolveDoodadVariants(row);
  const chosen = variants[variantIndex] || variants[0];
  const model = chosen ? await loadModel({ dir: chosen.dir, file: chosen.glb }) : null;

  if (model) {
    model.activeVariant = chosen.index;
    model.variants = variants.map((variant) => ({ index: variant.index, glb: variant.glb }));
    delete model.fileNames;
  }

  return {
    ...row,
    model,
    variants: variants.map((variant) => ({ index: variant.index, glb: variant.glb })),
    sounds: [],
    skills: [],
    animations: model ? model.clips.map((clip) => clip.name) : [],
    stubbed: false,
  };
}

const CATEGORY_COUNTS = {
  units: { count: 836, note: "来自 Tables/UnitUI.json" },
  abilities: { count: 803, note: "来自 Tables/AbilityData.json" },
  doodads: { count: 469, note: "Tables/Doodads.json（模型目录 589 个，含变体）" },
  destructables: { count: 129, note: "Tables/DestructableData.json" },
  items: { count: 268, note: "Tables/ItemData.json" },
  upgrades: { count: 167, note: "Tables/UpgradeData.json" },
  models: { count: 3264, note: "glb 文件数（含 portrait），模型目录 1600" },
  sounds: { count: 3877, note: "converted 内 wav" },
  textures: { count: 6457, note: "converted 内 png" },
  tables: { count: 52, note: "Tables/ 下 json" },
  texts: { count: 137, note: "Texts/ 下 json" },
};

// ---------- 媒体索引（模型库 / 音效 / 贴图）----------

const mediaRowsCache = new Map();

const FAMILY_LABEL = {
  Units: "单位",
  Buildings: "建筑",
  Doodads: "装饰物",
  Abilities: "技能",
  Objects: "物件",
  Environment: "环境",
  SharedModels: "共享模型",
  ReplaceableTextures: "可替换贴图",
  TerrainArt: "地形",
  Textures: "贴图",
  UI: "界面",
  Sound: "音效",
  Fonts: "字体",
};

async function mediaRows(ext) {
  if (mediaRowsCache.has(ext)) return mediaRowsCache.get(ext);
  const promise = api.index(ext).then((payload) => {
    const rows = (payload.files || []).map((file) => {
      const slash = file.lastIndexOf("/");
      const dir = slash >= 0 ? file.slice(0, slash) : "";
      const name = slash >= 0 ? file.slice(slash + 1) : file;
      const family = dir.split("/")[0] || "";
      return {
        id: file,
        name: name.replace(/\.[^.]+$/, ""),
        fullName: name,
        dir,
        family,
        familyLabel: FAMILY_LABEL[family] || family || "其他",
      };
    });
    return rows;
  });
  mediaRowsCache.set(ext, promise);
  return promise;
}

function filterMedia(rows, filters) {
  let items = rows;
  if (filters.family && filters.family !== "all") {
    items = items.filter((row) => row.family === filters.family);
  }
  if (filters.q) {
    const q = filters.q.toLowerCase();
    items = items.filter(
      (row) => row.name.toLowerCase().includes(q) || row.dir.toLowerCase().includes(q),
    );
  }
  return items;
}

function familyOptions(rows) {
  const counts = new Map();
  for (const row of rows) counts.set(row.family, (counts.get(row.family) || 0) + 1);
  const options = [{ id: "all", label: "全部", count: rows.length }];
  for (const [family, count] of [...counts.entries()].sort((a, b) => b[1] - a[1])) {
    options.push({ id: family, label: FAMILY_LABEL[family] || family || "其他", count });
  }
  return options;
}

export const MEDIA_PAGE_SIZE = 240;

export async function listModels(filters = {}) {
  const rows = await mediaRows(".glb");
  const items = filterMedia(rows, filters);
  return {
    items: items.slice(0, filters.limit || MEDIA_PAGE_SIZE).map((row) => ({
      id: row.id,
      name: row.name,
      en: row.dir,
      klass: "model",
      race: "all",
      iconUrl: "",
      stats: undefined,
    })),
    total: items.length,
    families: familyOptions(rows),
    stubbed: false,
  };
}

export async function listSounds(filters = {}) {
  const rows = await mediaRows(".wav");
  const items = filterMedia(rows, filters);
  return {
    items: items.slice(0, filters.limit || MEDIA_PAGE_SIZE).map((row) => ({
      id: row.id,
      name: row.name,
      en: row.dir,
      klass: "sound",
      race: "all",
      url: assetUrl(row.id),
      iconUrl: "",
    })),
    total: items.length,
    families: familyOptions(rows),
    stubbed: false,
  };
}

export async function listTextures(filters = {}) {
  const rows = await mediaRows(".png");
  const items = filterMedia(rows, filters);
  return {
    items: items.slice(0, filters.limit || MEDIA_PAGE_SIZE).map((row) => ({
      id: row.id,
      name: row.name,
      en: row.dir,
      klass: "texture",
      race: "all",
      iconUrl: assetUrl(row.id),
      thumbUrl: assetUrl(row.id),
    })),
    total: items.length,
    families: familyOptions(rows),
    stubbed: false,
  };
}

export async function getModelDetail(path) {
  const slash = path.lastIndexOf("/");
  const dir = slash >= 0 ? path.slice(0, slash) : "";
  const base = path.split("/").pop().replace(/\.glb$/i, "");
  const model = await loadModel({ dir, file: path });
  const fileNames = model.fileNames || [];
  delete model.fileNames;

  const siblings = fileNames.filter((name) => /\.(glb|tscn|tres|png|wav)$/i.test(name)).length;

  return {
    id: path,
    name: base,
    en: dir,
    klass: "model",
    race: "all",
    model,
    skills: [],
    sounds: [],
    fileCount: siblings,
    animations: model.clips.map((clip) => clip.name),
    stubbed: false,
  };
}

export async function getSoundDetail(path) {
  const slash = path.lastIndexOf("/");
  return {
    id: path,
    name: path.split("/").pop(),
    en: slash >= 0 ? path.slice(0, slash) : "",
    klass: "sound",
    race: "all",
    url: assetUrl(path),
    model: null,
    skills: [],
    sounds: [],
    animations: [],
    stubbed: false,
  };
}

export async function getTextureDetail(path) {
  const slash = path.lastIndexOf("/");
  return {
    id: path,
    name: path.split("/").pop(),
    en: slash >= 0 ? path.slice(0, slash) : "",
    klass: "texture",
    race: "all",
    url: assetUrl(path),
    model: null,
    skills: [],
    sounds: [],
    animations: [],
    stubbed: false,
  };
}

export function categoryInfo(category) {
  return CATEGORY_COUNTS[category] || { count: 0, note: "" };
}
