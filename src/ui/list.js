import { el, clear, pageHead, emptyState, loadingBox, lazyImage, toast } from "./dom.js";
import { categoryMeta, filtersFor, setFilter, PENDING_CATEGORIES } from "../store.js";
import {
  categoryInfo,
  listAbilities,
  listDoodads,
  listEmpty,
  listModels,
  listSounds,
  listTextures,
  listUnits,
  MEDIA_PAGE_SIZE,
} from "../api.js";
import { navigate } from "../router.js";
import { KLASSES, KLASS_LABEL, RACES, RACE_LABEL } from "../constants.js";

const MEDIA_CATEGORIES = new Set(["models", "sounds", "textures"]);

let currentAudio = null;

async function loadRows(category, filters) {
  switch (category) {
    case "units":
      return listUnits(filters);
    case "abilities":
      return listAbilities(filters);
    case "doodads":
      return listDoodads(filters);
    case "models":
      return listModels(filters);
    case "sounds":
      return listSounds(filters);
    case "textures":
      return listTextures(filters);
    default:
      return listEmpty(category);
  }
}

function statPills(item) {
  const pills = [];
  if (item.stats) {
    if (item.stats.hp !== undefined) pills.push(["HP", item.stats.hp]);
    if (item.stats.armor !== undefined) pills.push(["护甲", item.stats.armor]);
    if (item.stats.dmg !== undefined) pills.push(["攻击", item.stats.dmg]);
    if (item.stats.speed !== undefined) pills.push(["移速", item.stats.speed]);
  }
  if (item.pos) pills.push(["按钮位", item.pos]);
  if (item.kind) pills.push(["类别", item.kind]);
  if (item.skills && item.skills.length) pills.push(["技能", item.skills.length]);
  return el("div", { class: "card-stats" },
    pills.map(([key, value]) => el("span", { class: "stat-pill" }, [key + " ", el("b", { text: String(value) })])));
}

function subLine(item) {
  const parts = [item.id];
  if (item.en) parts.push(item.en);
  if (item.klass && KLASS_LABEL.has(item.klass)) parts.push(KLASS_LABEL.get(item.klass));
  if (item.race && RACE_LABEL.has(item.race) && item.race !== "all") parts.push(RACE_LABEL.get(item.race));
  const node = el("div", { class: "card-meta" });
  parts.forEach((part, index) => {
    if (index) node.append(el("span", { class: "dot", text: "·" }));
    node.append(el("span", { text: part }));
  });
  return node;
}

function card(item, category) {
  if (category === "textures") return textureCard(item);
  if (category === "sounds") return soundCard(item);
  if (category === "models") return modelCard(item);

  const thumb = lazyImage(item.iconUrl, {
    wrapperClass: "thumb",
    alt: item.name,
    fallback: category === "doodads" ? "" : "无图标",
  });
  thumb.append(el("span", { class: "thumb-tag", text: "3D · P1" }));

  return el("article", {
    class: "card",
    onclick: () => navigate(category, item.id),
  }, [
    thumb,
    el("div", { class: "card-body" }, [
      el("h3", { class: "card-title", text: item.name }),
      subLine(item),
      statPills(item),
    ]),
  ]);
}

function mediaMeta(item) {
  return el("div", { class: "card-meta" }, [el("span", { text: item.en || "—" })]);
}

function modelCard(item) {
  const thumb = el("div", { class: "thumb media-thumb" }, [
    el("span", { class: "media-glyph", text: "◇" }),
    el("span", { class: "thumb-tag", text: "模型" }),
  ]);
  return el("article", { class: "card", onclick: () => navigate("models", item.id) }, [
    thumb,
    el("div", { class: "card-body" }, [
      el("h3", { class: "card-title", text: item.name }),
      mediaMeta(item),
    ]),
  ]);
}

function soundCard(item) {
  const badge = el("span", { class: "media-glyph", text: "▶" });
  const thumb = el("div", {
    class: "thumb media-thumb",
    onclick: (event) => {
      event.stopPropagation();
      if (currentAudio) currentAudio.pause();
      currentAudio = new Audio(item.url);
      currentAudio.play().catch(() => toast("浏览器拒绝了播放"));
    },
  }, [badge, el("span", { class: "thumb-tag", text: "试听" })]);
  return el("article", { class: "card", onclick: () => navigate("sounds", item.id) }, [
    thumb,
    el("div", { class: "card-body" }, [
      el("h3", { class: "card-title", text: item.name }),
      mediaMeta(item),
    ]),
  ]);
}

function textureCard(item) {
  const thumb = el("div", { class: "thumb media-thumb checker" });
  const image = el("img", { alt: item.name, loading: "lazy", class: "texture-thumb" });
  image.addEventListener("load", () => image.classList.add("loaded"));
  image.addEventListener("error", () => image.remove());
  image.src = item.thumbUrl;
  thumb.append(image);
  return el("article", { class: "card", onclick: () => navigate("textures", item.id) }, [
    thumb,
    el("div", { class: "card-body" }, [
      el("h3", { class: "card-title small", text: item.name }),
      mediaMeta(item),
    ]),
  ]);
}

const DOODAD_CATEGORIES = [
  { id: "all", label: "全部" },
  { id: "E", label: "环境" },
  { id: "O", label: "物件" },
  { id: "S", label: "结构" },
  { id: "W", label: "水面" },
  { id: "C", label: "悬崖" },
  { id: "Z", label: "特殊" },
];

function buildFilterbar(category, onRefresh) {
  const filters = filtersFor(category);
  const bar = el("div", { class: "filterbar" });

  if (category === "units") {
    for (const race of RACES) {
      bar.append(el("button", {
        class: `chip${filters.race === race.id ? " active" : ""}`,
        text: race.label,
        onclick: (event) => {
          setFilter(category, "race", race.id);
          for (const node of bar.querySelectorAll(".chip")) node.classList.remove("active");
          event.currentTarget.classList.add("active");
          onRefresh();
        },
      }));
    }
    bar.append(el("div", { class: "filter-divider" }));

    const klassSelect = el("select", {
      class: "ctl",
      onchange: (event) => {
        setFilter(category, "klass", event.target.value);
        onRefresh();
      },
    }, KLASSES.map((row) => el("option", { value: row.id, text: row.label, selected: filters.klass === row.id })));
    bar.append(klassSelect);
  }

  if (category === "doodads") {
    for (const option of DOODAD_CATEGORIES) {
      bar.append(el("button", {
        class: `chip${(filters.category || "all") === option.id ? " active" : ""}`,
        text: option.label,
        onclick: (event) => {
          setFilter(category, "category", option.id);
          for (const node of bar.querySelectorAll(".chip")) node.classList.remove("active");
          event.currentTarget.classList.add("active");
          onRefresh();
        },
      }));
    }
    bar.append(el("div", { class: "filter-divider" }));
  }

  if (MEDIA_CATEGORIES.has(category)) {
    const host = el("div", { class: "chip-row" }, [el("span", { class: "topbar-stat", text: "加载中…" })]);
    bar.familyHost = host;
    bar.append(host, el("div", { class: "filter-divider" }));
  }

  const search = el("input", {
    class: "ctl",
    type: "search",
    placeholder: category === "units" ? "搜索名称 / id / 英文名" : "搜索名称",
    value: filters.q,
  });
  let timer = 0;
  search.addEventListener("input", (event) => {
    setFilter(category, "q", event.target.value);
    window.clearTimeout(timer);
    timer = window.setTimeout(onRefresh, 180);
  });
  bar.append(search);

  const sortSelect = el("select", {
    class: "ctl",
    onchange: () => {
      toast("排序将在 P2 接入真实数据后启用");
      sortSelect.value = "id";
    },
  }, [
    el("option", { value: "id", text: "按 id 排序" }),
    el("option", { value: "hp", text: "按生命值排序" }),
    el("option", { value: "name", text: "按名称排序" }),
  ]);
  bar.append(sortSelect);

  const count = el("div", { class: "filter-count" });
  bar.append(count);
  bar.countNode = count;
  return bar;
}

function buildShortcuts(category) {
  if (category !== "units") return null;
  const make = (glyph, title, desc) =>
    el("div", {
      class: "shortcut",
      onclick: () => toast(`${title}：P2 接入（属性矩阵需要 UnitData / UnitBalance / UnitWeapons 三表联查）`),
    }, [
      el("div", { class: "shortcut-glyph", text: glyph }),
      el("div", {}, [
        el("div", { class: "shortcut-title", text: title }),
        el("div", { class: "shortcut-desc", text: desc }),
      ]),
      el("div", { class: "shortcut-arrow", text: "→" }),
    ]);
  return el("div", { class: "shortcuts" }, [
    make("▤", "属性矩阵", "按生命 / 护甲 / 攻击并列比较全部单位"),
    make("✦", "技能登记表", "按英雄与技能等级核对图标、按钮位与冷却"),
  ]);
}

export async function renderList(view, route, isStale) {
  const category = route.category;
  const meta = categoryMeta(category);
  const info = categoryInfo(category);
  const pending = PENDING_CATEGORIES.has(category);
  const media = MEDIA_CATEGORIES.has(category);

  clear(view);
  view.append(
    pageHead(meta.title, meta.sub, `THE ARCHIVE / ${meta.group || "资源"}`),
  );

  const grid = el("div", { class: "grid" });
  const filterbar = pending ? null : buildFilterbar(category, () => refresh());
  const shortcuts = buildShortcuts(category);
  const footer = el("div", { class: "list-footer" });

  if (shortcuts) view.append(shortcuts);
  if (filterbar) view.append(filterbar);
  if (pending) {
    view.append(el("div", { class: "note", text: `${info.note} 该分类在 P4 接入，目前只有计数。` }));
  }
  view.append(grid, footer);

  async function refresh() {
    const filters = filtersFor(category);
    if (media && !filters.limit) setFilter(category, "limit", MEDIA_PAGE_SIZE);

    grid.replaceChildren(el("div", { style: "grid-column:1/-1" }, [loadingBox("读取资源列表")]));
    const result = await loadRows(category, filtersFor(category));
    if (isStale()) return;

    if (filterbar?.countNode) {
      const shown = result.items.length;
      const total = result.total.toLocaleString();
      filterbar.countNode.textContent = shown < result.total ? `${shown} / ${total}（已加载）` : `${shown} / ${total}`;
    }

    if (filterbar?.familyHost && result.families) {
      clear(filterbar.familyHost);
      for (const family of result.families) {
        filterbar.familyHost.append(el("button", {
          class: `chip${(filters.family || "all") === family.id ? " active" : ""}`,
          text: `${family.label} ${family.count}`,
          onclick: () => {
            setFilter(category, "family", family.id);
            setFilter(category, "limit", MEDIA_PAGE_SIZE);
            refresh();
          },
        }));
      }
    }

    clear(grid);
    clear(footer);

    if (!result.items.length) {
      grid.style.gridTemplateColumns = "1fr";
      grid.append(emptyState(
        pending ? "该分类尚未接入" : "没有匹配的结果",
        pending ? `${info.note} P4 会按目录扫描生成。` : "换个关键词，或把过滤条件调回「全部」。",
      ));
      return;
    }

    grid.style.gridTemplateColumns = "";
    for (const item of result.items) grid.append(card(item, category));

    if (media && result.items.length < result.total) {
      footer.append(el("button", {
        class: "tool-btn more-btn",
        text: `加载更多（还有 ${(result.total - result.items.length).toLocaleString()} 项）`,
        onclick: () => {
          setFilter(category, "limit", (filtersFor(category).limit || MEDIA_PAGE_SIZE) + MEDIA_PAGE_SIZE);
          refresh();
        },
      }));
    }
  }

  await refresh();
}
