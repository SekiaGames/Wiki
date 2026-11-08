import { el, clear } from "./dom.js";
import { categoryMeta } from "../store.js";
import { backend, categoryInfo } from "../api.js";

export function renderTopbar(root, route) {
  clear(root);
  const meta = categoryMeta(route.category);
  const info = categoryInfo(route.category);

  const crumbGroup = meta.group || "资源";
  const crumbs = el("div", { class: "crumbs" }, [
    el("b", { text: "世界" }),
    el("span", { class: "sep", text: "/" }),
    el("b", { text: crumbGroup }),
    el("span", { class: "sep", text: "/" }),
    el("span", { text: route.id ? `${meta.title} · ${route.id}` : meta.title }),
  ]);

  const right = el("div", { class: "topbar-right" }, [
    el("div", { class: "live-badge", id: "live-badge", text: "热更新 …" }),
    info.count ? el("div", { class: "topbar-stat", text: `${info.count.toLocaleString()} 项` }) : null,
    el("div", { class: "topbar-stat", text: "Godot/assets/converted" }),
    el("div", { class: "stub-chip", text: backend.label }),
  ]);

  root.append(crumbs, right);
}
