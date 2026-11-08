export const RACES = [
  { id: "all", label: "全部" },
  { id: "human", label: "人族" },
  { id: "orc", label: "兽族" },
  { id: "undead", label: "不死族" },
  { id: "nightelf", label: "暗夜精灵" },
  { id: "neutral", label: "中立" },
];

export const KLASSES = [
  { id: "all", label: "全部类型" },
  { id: "hero", label: "英雄" },
  { id: "unit", label: "单位" },
  { id: "building", label: "建筑" },
];

export const RACE_LABEL = new Map(RACES.map((row) => [row.id, row.label]));

export const KLASS_LABEL = new Map([
  ...KLASSES.map((row) => [row.id, row.label]),
  ["doodad", "装饰物"],
  ["ability", "技能"],
  ["model", "模型"],
  ["sound", "音效"],
  ["texture", "贴图"],
]);
