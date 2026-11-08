import { el, clear, pageHead, toast } from "./dom.js";
import { CATEGORIES, PENDING_CATEGORIES } from "../store.js";
import { categoryInfo } from "../api.js";
import { navigate } from "../router.js";

const TILES = [
  ["3,264", "glb 模型", "72 MB · 中位 8 KB"],
  ["6,457", "png 贴图", "292 MB · 含 2184 图标"],
  ["3,877", "wav 音效", "PCM16 单声道 22050"],
  ["1,600", "模型目录", "含 tscn 的目录数"],
];

export function renderOverview(view, route) {
  clear(view);
  view.append(pageHead(
    "资源总览",
    "只读的 War3 资源档案馆。左侧选择分类，右侧浏览条目；选中条目后可预览模型、播放动画与音效。",
    "THE ARCHIVE / OVERVIEW",
  ));

  view.append(el("div", { class: "tiles" }, TILES.map(([num, label, note]) =>
    el("div", { class: "tile" }, [
      el("div", { class: "tile-num", text: num }),
      el("div", { class: "tile-label", text: label }),
      el("div", { class: "tile-note", text: note }),
    ]))));

  view.append(el("div", { class: "section-title", text: "浏览分类" }));

  const entries = el("div", { class: "entry-grid" });
  for (const category of CATEGORIES) {
    if (category.id === "overview") continue;
    const info = categoryInfo(category.id);
    entries.append(el("div", {
      class: "entry",
      onclick: () => navigate(category.id),
    }, [
      el("div", { class: "entry-name", text: category.label }),
      el("div", { class: "entry-count", text: `${info.count.toLocaleString()} 项${PENDING_CATEGORIES.has(category.id) ? " · P4 接入" : ""}` }),
    ]));
  }
  view.append(entries);

  view.append(el("div", { class: "section-title", text: "当前进度" }));
  view.append(el("div", { class: "note", text:
    "P1 已完成：单位列表与详情全部读真实数据（UnitUI / UnitData / UnitBalance / UnitAbilities + Texts 本地化），" +
    "详情页按需解析 glb、materials/*.tres 与 _anims.tres，可播全部动画、切换队伍色、试听语音、点技能触发施法动画。" +
    "P2 计划：属性矩阵与排序、原始描述文本、两层贴图混合的真实着色。" }));

  view.append(el("div", { class: "shortcuts", style: "margin-top:18px" }, [
    el("div", { class: "shortcut", onclick: () => toast("只读服务：Wiki/tools/serve.py，接口 /api/asset/list、/api/asset/read") }, [
      el("div", { class: "shortcut-glyph", text: "◎" }),
      el("div", {}, [
        el("div", { class: "shortcut-title", text: "只读接口已就绪" }),
        el("div", { class: "shortcut-desc", text: "白名单锁在 Godot/assets/converted，越界一律拒绝，不写任何文件" }),
      ]),
      el("div", { class: "shortcut-arrow", text: "→" }),
    ]),
    el("div", { class: "shortcut", onclick: () => navigate("units") }, [
      el("div", { class: "shortcut-glyph", text: "◆" }),
      el("div", {}, [
        el("div", { class: "shortcut-title", text: "从单位开始" }),
        el("div", { class: "shortcut-desc", text: "836 行单位表，英雄带技能面板与语音" }),
      ]),
      el("div", { class: "shortcut-arrow", text: "→" }),
    ]),
  ]));
}
