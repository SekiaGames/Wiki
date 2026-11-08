export function el(tag, props = {}, children = []) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(props)) {
    if (value === undefined || value === null || value === false) continue;
    if (key === "class") node.className = value;
    else if (key === "text") node.textContent = value;
    else if (key === "html") node.innerHTML = value;
    else if (key === "dataset") Object.assign(node.dataset, value);
    else if (key.startsWith("on") && typeof value === "function") node.addEventListener(key.slice(2).toLowerCase(), value);
    else node.setAttribute(key, value);
  }
  for (const child of [].concat(children)) {
    if (child === null || child === undefined || child === false) continue;
    node.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return node;
}

export function clear(node) {
  while (node.firstChild) node.removeChild(node.firstChild);
}

export function spinner(size = "") {
  return el("div", { class: `spinner${size ? " " + size : ""}` });
}

export function loadingBox(text = "加载中") {
  return el("div", { class: "loading-box" }, [spinner(), el("span", { text })]);
}

// 异步图片：图像到达前只显示转圈，失败显示占位文字。
export function lazyImage(src, options = {}) {
  const { wrapperClass = "", imgClass = "", alt = "", fallback = "无图标" } = options;
  const holder = el("div", { class: wrapperClass });
  const image = el("img", { alt, class: imgClass });
  const busy = spinner();
  holder.append(busy, image);
  if (!src) {
    busy.remove();
    holder.append(el("span", { class: "thumb-fallback", text: fallback }));
    return holder;
  }
  image.addEventListener("load", () => {
    busy.remove();
    image.classList.add("loaded");
  });
  image.addEventListener("error", () => {
    busy.remove();
    image.remove();
    holder.append(el("span", { class: "thumb-fallback", text: fallback }));
  });
  image.src = src;
  return holder;
}

let toastLayer = null;

export function toast(message) {
  if (!toastLayer) toastLayer = document.getElementById("toast-layer");
  const node = el("div", { class: "toast", text: message });
  toastLayer.append(node);
  setTimeout(() => {
    node.style.opacity = "0";
    node.style.transition = "opacity .2s";
    setTimeout(() => node.remove(), 220);
  }, 2000);
}

export function eyebrow(text) {
  return el("div", { class: "eyebrow", text });
}

export function pageHead(title, sub, eyebrowText) {
  return el("div", { class: "page-head" }, [
    eyebrowText ? eyebrow(eyebrowText) : null,
    el("h1", { class: "page-title", text: title }),
    sub ? el("p", { class: "page-sub", text: sub }) : null,
  ]);
}

export function emptyState(title, sub) {
  return el("div", { class: "empty" }, [
    el("div", { class: "empty-title", text: title }),
    el("div", { class: "empty-sub", text: sub }),
  ]);
}
