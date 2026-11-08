import { el, clear, emptyState, loadingBox, lazyImage, toast } from "./dom.js";
import { categoryMeta } from "../store.js";
import { getAbilityDetail, getDoodadDetail, getModelDetail, getSoundDetail, getTextureDetail, getUnitDetail, assetUrl } from "../api.js";
import { navigate } from "../router.js";
import { KLASS_LABEL, RACE_LABEL } from "../constants.js";
import { ModelViewer, TEAM_COLORS } from "../viewer3d.js";

const SOUND_KIND_LABEL = {
  What: "回应",
  Yes: "确认",
  YesAttack: "攻击应答",
  Attack: "攻击",
  Death: "死亡",
  Birth: "出生",
  Pissed: "烦躁",
  Warcry: "战吼",
  Ready: "就绪",
  Other: "其他",
};

let activeViewer = null;
let activeAudio = null;

export function disposeDetailViewer() {
  if (activeViewer) {
    activeViewer.dispose();
    activeViewer = null;
  }
  if (activeAudio) {
    activeAudio.pause();
    activeAudio = null;
  }
}

async function fetchDetail(category, id, variantIndex) {
  switch (category) {
    case "units":
      return getUnitDetail(id);
    case "abilities":
      return getAbilityDetail(id);
    case "doodads":
      return getDoodadDetail(id, variantIndex);
    case "models":
      return getModelDetail(id);
    case "sounds":
      return getSoundDetail(id);
    case "textures":
      return getTextureDetail(id);
    default:
      return null;
  }
}

function buildMediaStage(item) {
  const canvas = el("div", { class: "stage-canvas media-canvas" });

  if (item.klass === "texture") {
    const image = el("img", { class: "media-image", alt: item.name, src: item.url });
    image.addEventListener("load", () => {
      const width = image.naturalWidth || 0;
      if (width && width <= 96) image.classList.add("tiny");
      else if (width && width <= 200) image.classList.add("small");
      const hint = el("div", { class: "stage-hint", text: `${image.naturalWidth} × ${image.naturalHeight}` });
      canvas.append(hint);
    });
    canvas.append(image);
    const toolbar = el("div", { class: "stage-toolbar" }, [
      el("span", { class: "topbar-stat", text: item.id }),
      el("div", { class: "toolbar-spacer" }),
      el("button", {
        class: "tool-btn",
        text: "新窗口打开",
        onclick: () => window.open(item.url, "_blank"),
      }),
    ]);
    return { element: el("section", { class: "stage" }, [canvas, toolbar]) };
  }

  const audio = el("audio", { controls: "controls", src: item.url, class: "media-audio" });
  canvas.append(audio);
  const toolbar = el("div", { class: "stage-toolbar" }, [
    el("span", { class: "topbar-stat", text: item.id }),
    el("div", { class: "toolbar-spacer" }),
    el("button", {
      class: "tool-btn",
      text: "新窗口打开",
      onclick: () => window.open(item.url, "_blank"),
    }),
  ]);
  return { element: el("section", { class: "stage" }, [canvas, toolbar]) };
}

function buildStage(item) {
  const canvas = el("div", { class: "stage-canvas" });
  const hint = el("div", { class: "stage-hint" });
  canvas.append(loadingBox("加载 glb / 材质 / 动画"), hint);

  const animSelect = el("select", { class: "ctl" }, [el("option", { text: "—" })]);
  const playButton = el("button", { class: "tool-btn on", text: "▶ 播放" });
  const replayButton = el("button", { class: "tool-btn", text: "重播" });
  const speedSelect = el("select", { class: "ctl" }, [
    el("option", { value: "0.5", text: "0.5×" }),
    el("option", { value: "1", text: "1×", selected: true }),
    el("option", { value: "1.5", text: "1.5×" }),
    el("option", { value: "2", text: "2×" }),
  ]);
  const wireButton = el("button", { class: "tool-btn", text: "线框" });
  const shotButton = el("button", { class: "tool-btn", text: "截图" });
  const teamSelect = el("select", { class: "ctl" },
    TEAM_COLORS.map((color) => el("option", { value: String(color.hex), text: color.label })));

  const variantSelect = el("select", { class: "ctl" },
    (item.variants || []).map((variant) => el("option", {
      value: String(variant.index),
      text: `变体 ${variant.index + 1}`,
      selected: variant.index === (item.model?.activeVariant ?? 0),
    })));
  if (!item.variants || item.variants.length < 2) variantSelect.style.display = "none";

  const animGroup = el("div", { class: "toolbar-group" }, [
    el("span", { class: "topbar-stat", text: "动画" }),
    animSelect,
    playButton,
    replayButton,
    el("span", { class: "topbar-stat", text: "速度" }),
    speedSelect,
  ]);

  const variantGroup = el("div", { class: "toolbar-group" }, [
    el("span", { class: "topbar-stat", text: "变体" }),
    variantSelect,
  ]);
  if (!item.variants || item.variants.length < 2) variantGroup.style.display = "none";

  const toolbar = el("div", { class: "stage-toolbar" }, [
    animGroup,
    el("span", { class: "topbar-stat", text: "队伍色" }),
    teamSelect,
    variantGroup,
    el("div", { class: "toolbar-spacer" }),
    wireButton,
    shotButton,
  ]);

  return {
    element: el("section", { class: "stage" }, [canvas, toolbar]),
    canvas,
    hint,
    animGroup,
    animSelect,
    playButton,
    replayButton,
    speedSelect,
    teamSelect,
    variantSelect,
    wireButton,
    shotButton,
  };
}

function wireStage(stageUi, item) {
  const { canvas, hint, animSelect, playButton, replayButton, speedSelect, teamSelect, variantSelect, wireButton, shotButton } = stageUi;

  if (!item.model) {
    clear(canvas);
    canvas.append(
      emptyState("没有可用模型", "该条目在 converted 里找不到对应的 glb。"),
    );
    stageUi.element.querySelector(".stage-toolbar")?.remove();
    return null;
  }

  const viewer = new ModelViewer(canvas);
  activeViewer = viewer;

  animSelect.addEventListener("change", () => {
    if (animSelect.value === "__bind__") viewer.bindPose();
    else viewer.play(animSelect.value);
  });
  playButton.addEventListener("click", () => {
    if (animSelect.value === "__bind__") viewer.bindPose();
    else viewer.play(animSelect.value);
  });
  replayButton.addEventListener("click", () => {
    viewer.play(animSelect.value);
  });
  speedSelect.addEventListener("change", () => {
    viewer.setSpeed(Number(speedSelect.value) || 1);
  });
  teamSelect.addEventListener("change", () => {
    viewer.setTeamColor(Number(teamSelect.value));
  });
  variantSelect.addEventListener("change", () => {
    const url = new URL(location.href);
    url.searchParams.set("v", variantSelect.value);
    location.href = url.toString();
  });
  wireButton.addEventListener("click", () => {
    wireButton.classList.toggle("on");
    viewer.setWireframe(wireButton.classList.contains("on"));
  });
  shotButton.addEventListener("click", () => {
    const link = el("a", { download: `${item.id}.png`, href: viewer.screenshot() });
    link.click();
  });

  hint.textContent = `${item.model.glb} · ${item.model.slots.length} 个 geoset`;

  viewer.readyPromise = viewer
    .load(item.model)
    .then(({ clips }) => {
      clear(canvas);
      canvas.append(viewer.renderer.domElement);
      const names = clips.length ? clips : ["—"];
      clear(animSelect);
      animSelect.append(el("option", { value: "__bind__", text: "（绑定姿势）" }));
      for (const name of names) animSelect.append(el("option", { value: name, text: name }));
      animSelect.value = viewer.currentName || "__bind__";
      const hidden = (new URLSearchParams(location.search).get("hide") || "").split(",").filter(Boolean);
      for (const name of hidden) viewer.setGeosetVisible(name, false);
      const params = new URLSearchParams(location.search);
      const wanted = params.get("anim");
      if (wanted && viewer.play(wanted)) animSelect.value = wanted;
      if (params.get("pose") === "bind") {
        animSelect.value = "__bind__";
        viewer.bindPose();
      }
      if (!clips.length) stageUi.animGroup.style.display = "none";
      hint.textContent = `${item.model.glb} · ${clips.length} 个动画 · ${item.model.slots.length} geoset`;
      if (item.model.missingMaterials) {
        toast(`${item.model.missingMaterials} 个材质未能解析`);
      }
    })
    .catch((error) => {
      clear(canvas);
      canvas.append(emptyState("模型加载失败", String(error?.message || error)));
    });

  return viewer;
}

function kvTable(item) {
  const rows = [
    ["id", item.id],
    ["名称", item.name || "—"],
    ["类型", KLASS_LABEL.get(item.klass) || item.klass || "—"],
  ];

  if (item.klass === "texture") {
    rows.push(["目录", item.en || "—"]);
    rows.push(["格式", String(item.name).split(".").pop().toUpperCase()]);
    rows.push(["地址", item.url]);
  } else if (item.klass === "sound") {
    rows.push(["目录", item.en || "—"]);
    rows.push(["格式", "WAV · PCM16"]);
    rows.push(["地址", item.url]);
  } else if (item.klass === "model") {
    rows.push(["目录", item.en || "—"]);
    rows.push(["glb", item.model?.glb || "—"]);
    rows.push(["geoset", item.model?.slots?.length ?? 0]);
    rows.push(["动画数", item.model?.clips?.length ?? 0]);
  } else if (item.klass === "doodad") {
    rows.push(["类别", item.category || "—"]);
    rows.push(["变体数", item.numVar ?? "—"]);
    rows.push(["默认缩放", item.scale ?? "—"]);
    rows.push(["可通行", item.walkable ? "是" : "否"]);
    rows.push(["适用地形", item.tilesets || "—"]);
    rows.push(["模型", item.model?.glb || "—"]);
    rows.push(["geoset", item.model?.slots?.length ?? 0]);
  } else {
    rows.push(["英文名", item.en || "—"]);
    rows.push(["种族", RACE_LABEL.get(item.race) || "—"]);
    rows.push(["等级", item.level ?? "—"]);
    rows.push(["生命值", item.hp ?? "—"]);
    rows.push(["魔法值", item.mana ?? "—"]);
    rows.push(["护甲", item.armor ?? "—"]);
    rows.push(["移速", item.speed ?? "—"]);
    rows.push(["视野", item.sight ?? "—"]);
    if (item.gold !== null && item.gold !== undefined) {
      rows.push(["造价", `${item.gold ?? 0} 金 / ${item.lumber ?? 0} 木`]);
    }
    rows.push(["热键", item.hotkey || "—"]);
    rows.push(["模型", item.model?.glb || "—"]);
    rows.push(["动画数", item.model?.clips?.length ?? 0]);
    rows.push(["按钮位", item.pos || "—"]);
    rows.push(["施法动画", item.anim || "—"]);
  }

  const list = el("dl", { class: "kv" });
  for (const [key, value] of rows) list.append(el("dt", { text: key }), el("dd", { text: String(value) }));
  return el("div", {}, [list, item.tip ? el("div", { class: "note", text: stripColors(item.tip) }) : null]);
}

function stripColors(text) {
  return String(text).replace(/\|c[0-9a-fA-F]{8}/g, "").replace(/\|r/g, "").replace(/\|n/g, "\n");
}

function skillTab(item, viewer) {
  if (!item.skills?.length) return emptyState("没有技能", "该单位没有登记英雄技能。");
  const grid = el("div", { class: "skill-grid" });
  for (const skill of item.skills) {
    const icon = lazyImage(skill.iconUrl, { wrapperClass: "skill-icon", alt: skill.name, fallback: "" });
    grid.append(el("div", {
      class: "skill-slot",
      title: `${skill.id} · ${stripColors(skill.tip || "")}`,
      onclick: () => {
        if (viewer?.play(skill.anim)) toast(`播放动画 ${skill.anim}（${skill.name}）`);
        else toast(`动画 ${skill.anim} 不存在`);
      },
    }, [
      icon,
      el("div", {}, [
        el("div", { class: "skill-name", text: skill.name }),
        el("div", { class: "skill-sub", text: `${skill.id} · ${skill.pos || "无按钮位"}` }),
      ]),
    ]));
  }
  return el("div", {}, [grid, el("div", { class: "note", text: "点技能会播放它登记的施法动画名。" })]);
}

function soundTab(item) {
  const sounds = item.sounds || [];
  if (!sounds.length) return emptyState("没有音效", "该模型目录下没有 wav。");
  const list = el("div");
  for (const sound of sounds) {
    list.append(el("div", {
      class: "sound-row",
      onclick: () => {
        if (activeAudio) activeAudio.pause();
        activeAudio = new Audio(sound.url);
        activeAudio.play().catch(() => toast("浏览器拒绝了播放"));
      },
    }, [
      el("span", { class: "sound-play", text: "▶" }),
      el("span", { class: "sound-name", text: sound.file }),
      el("span", { class: "sound-kind", text: SOUND_KIND_LABEL[sound.kind] || sound.kind }),
    ]));
  }
  return el("div", {}, [list, el("div", { class: "note", text: `共 ${sounds.length} 个 wav，PCM16 单声道。` })]);
}

function modelTab(item, viewer) {
  if (!viewer) return emptyState("没有模型", "该条目没有可加载的 glb。");
  const container = el("div");

  const renderList = () => {
    clear(container);
    const rows = viewer.geosetInfo();
    const list = el("div");
    for (const row of rows) {
      const checkbox = el("input", { type: "checkbox" });
      checkbox.checked = row.visible;
      checkbox.addEventListener("change", () => viewer.setGeosetVisible(row.node, checkbox.checked));
      list.append(el("label", { class: "geoset-row" }, [
        checkbox,
        el("span", { class: "geoset-name", text: row.node }),
        el("span", { class: "geoset-meta", text: `${row.vertices} 顶点 · ${(row.size || []).join("×")}` }),
      ]));
    }
    container.append(
      list,
      el("div", { class: "note", text: "游戏里 geoset 由 GEOA 规则控制（avatar 形态、血雾、尸体）；wiki 默认全显，可在这里手动关掉。" }),
      el("div", { class: "note", text: `glb：${item.model.glb}` }),
    );
  };

  if (viewer.root) renderList();
  else {
    container.append(loadingBox("等待模型加载"));
    viewer.readyPromise?.then(renderList).catch(() => {
      clear(container);
      container.append(emptyState("模型加载失败", "geoset 列表不可用。"));
    });
  }
  return container;
}

function rawTab(item) {
  const payload = {
    id: item.id,
    name: item.name,
    en: item.en,
    race: item.race,
    klass: item.klass,
    stats: { level: item.level, hp: item.hp, mana: item.mana, armor: item.armor, speed: item.speed },
    model: item.model ? { glb: item.model.glb, clips: item.model.clips.map((clip) => clip.name), slots: item.model.slots.map((slot) => slot.node) } : null,
    raw: item.raw,
  };
  return el("pre", { class: "raw", text: JSON.stringify(payload, null, 2) });
}

function sidePanel(item, category, initialTab, viewer) {
  const tabs = [{ id: "stats", label: "属性", render: () => kvTable(item) }];
  if (category === "units") {
    tabs.push({ id: "skills", label: "技能", render: () => skillTab(item, viewer) });
    tabs.push({ id: "sounds", label: "音效", render: () => soundTab(item) });
    if (item.model) tabs.push({ id: "model", label: "模型", render: () => modelTab(item, viewer) });
  }
  if (category === "doodads" && item.model) {
    tabs.push({ id: "model", label: "模型", render: () => modelTab(item, viewer) });
  }
  if (category === "models" && item.model) {
    tabs.push({ id: "model", label: "模型", render: () => modelTab(item, viewer) });
  }
  tabs.push({ id: "raw", label: "原始数据", render: () => rawTab(item) });

  const body = el("div", { class: "tab-body" });
  const tabBar = el("div", { class: "tabs" });

  const activate = (id) => {
    const tab = tabs.find((row) => row.id === id) || tabs[0];
    for (const node of tabBar.querySelectorAll(".tab")) {
      node.classList.toggle("active", node.dataset.tab === tab.id);
    }
    clear(body);
    body.append(tab.render());
  };

  for (const tab of tabs) {
    tabBar.append(el("button", {
      class: "tab",
      dataset: { tab: tab.id },
      text: tab.label,
      onclick: () => {
        activate(tab.id);
        history.replaceState(null, "", `#/${category}/${item.id}/${tab.id}`);
      },
    }));
  }

  const head = el("div", { class: "side-head" }, [
    el("h2", { class: "side-title", text: item.name }),
    el("div", { class: "side-meta" }, [
      el("span", { text: item.id }),
      item.en ? el("span", { text: "·" }) : null,
      item.en ? el("span", { text: item.en }) : null,
      el("span", { text: "·" }),
      el("span", { text: KLASS_LABEL.get(item.klass) || "条目" }),
      item.race && item.race !== "all" ? el("span", { text: "·" }) : null,
      item.race && item.race !== "all" ? el("span", { text: RACE_LABEL.get(item.race) || item.race }) : null,
    ]),
  ]);

  const panel = el("aside", { class: "side-panel" }, [head, tabBar, body]);
  activate(initialTab || "stats");
  return panel;
}

export async function renderDetail(view, route, isStale) {
  disposeDetailViewer();
  const meta = categoryMeta(route.category);
  clear(view);

  view.append(el("a", {
    class: "back-link",
    href: `#/${route.category}`,
    text: `← 返回${meta.title}`,
    onclick: (event) => {
      event.preventDefault();
      navigate(route.category);
    },
  }));

  const box = el("div", {}, [loadingBox("读取条目与模型")]);
  view.append(box);

  let item = null;
  try {
    item = await fetchDetail(route.category, route.id, Number(new URLSearchParams(location.search).get("v") || 0));
  } catch (error) {
    box.remove();
    view.append(emptyState("读取失败", String(error?.stack || error?.message || error).split("\n").slice(0, 6).join(" ⏎ ")));
    return;
  }
  if (isStale()) return;

  box.remove();
  if (!item) {
    view.append(emptyState("找不到该条目", `${route.category} / ${route.id} 不在当前数据源里。`));
    return;
  }
  const is3d = Boolean(item.model);
  const stageUi = is3d ? buildStage(item) : buildMediaStage(item);
  const viewer = is3d ? wireStage(stageUi, item) : null;
  view.append(el("div", { class: "detail" }, [stageUi.element, sidePanel(item, route.category, route.tab, viewer)]));
  document.title = `${item.name} · 资源 Wiki`;
}
