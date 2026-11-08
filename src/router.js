export function parseHash() {
  const raw = (location.hash || "").replace(/^#\/?/, "");
  if (!raw) return { category: "overview", id: "", tab: "" };

  const parts = raw.split("/");
  const category = parts[0] || "overview";

  if (parts.length === 1) return { category, id: "", tab: "" };
  if (parts.length === 2) return { category, id: decodeURIComponent(parts[1]), tab: "" };
  if (parts.length === 3) {
    return { category, id: decodeURIComponent(parts[1]), tab: decodeURIComponent(parts[2]) };
  }

  // 资源路径本身带斜杠（模型库/音效/贴图 的 id 就是文件路径），全部塞回 id
  return { category, id: decodeURIComponent(parts.slice(1).join("/")), tab: "" };
}

export function navigate(category, id = "", tab = "") {
  const segments = [category];
  if (id) segments.push(encodeURIComponent(id));
  if (tab) segments.push(encodeURIComponent(tab));
  const next = `#/${segments.join("/")}`;
  if (location.hash === next) {
    window.dispatchEvent(new HashChangeEvent("hashchange"));
    return;
  }
  location.hash = next;
}

export function onRouteChange(handler) {
  window.addEventListener("hashchange", () => handler(parseHash()));
}
