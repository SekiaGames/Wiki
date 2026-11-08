import { el, clear } from "./dom.js";
import { CATEGORIES, categoryMeta, PENDING_CATEGORIES } from "../store.js";
import { categoryInfo } from "../api.js";
import { navigate } from "../router.js";

export function renderSidebar(root, route) {
  clear(root);

  const brand = el("div", { class: "brand" }, [
    el("div", { class: "brand-mark", text: "W3" }),
    el("div", { class: "brand-text" }, [
      el("div", { class: "brand-title", text: "Warcraft" }),
      el("div", { class: "brand-sub", text: "Asset Wiki" }),
    ]),
  ]);

  const nav = el("nav", { class: "nav" });
  let currentGroup = null;
  let groupNode = null;

  for (const category of CATEGORIES) {
    if (category.group !== currentGroup) {
      currentGroup = category.group;
      groupNode = el("div", { class: "nav-group" }, [
        el("div", { class: "nav-group-title", text: `${currentGroup} · ${currentGroup === "浏览" ? "Discover" : currentGroup === "资源" ? "The World" : currentGroup === "媒体" ? "Media" : "Data"}` }),
      ]);
      nav.append(groupNode);
    }

    const info = categoryInfo(category.id);
    const active = route.category === category.id;
    const pending = PENDING_CATEGORIES.has(category.id);

    const item = el("a", {
      class: `nav-item${active ? " active" : ""}${pending ? " pending" : ""}`,
      href: `#/${category.id}`,
      onclick: (event) => {
        event.preventDefault();
        navigate(category.id);
      },
    }, [
      el("span", { class: "nav-glyph", text: category.glyph }),
      el("span", { class: "nav-label", text: category.label }),
      info.count ? el("span", { class: "nav-count", text: info.count.toLocaleString() }) : null,
    ]);

    groupNode.append(item);
  }

  const foot = el("div", { class: "sidebar-foot" }, [
    el("div", {}, [el("span", { class: "status-dot" }), "只读 converted"]),
    el("div", { text: "P1 · 真实数据 + 3D 预览" }),
    el("div", { text: "1.3 GB · 3264 glb · 6457 png" }),
  ]);

  root.append(brand, nav, foot);
  document.title = `${categoryMeta(route.category).title} · 资源 Wiki`;
}
