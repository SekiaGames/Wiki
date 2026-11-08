// 热更新：订阅 /api/watch 的 SSE 事件，检测到 Wiki/ 或数据表变化后自动刷新页面。
// 关闭方式：地址后加 ?live=0

const RELOAD_DELAY = 240;

function badgeNode() {
  return document.getElementById("live-badge");
}

function setBadge(text, state) {
  const node = badgeNode();
  if (!node) return;
  node.textContent = text;
  node.className = `live-badge${state ? " on" : ""}`;
}

export function startHotReload(options = {}) {
  const dirs = options.dirs || [];
  const params = new URLSearchParams(location.search);

  if (params.get("live") === "0") {
    setBadge("热更新 关");
    return;
  }
  if (!("EventSource" in window)) {
    setBadge("热更新 不支持");
    return;
  }

  const query = dirs.length ? `?dirs=${encodeURIComponent(dirs.join(","))}` : "";
  let timer = 0;

  const source = new EventSource(`/api/watch${query}`);

  source.addEventListener("open", () => setBadge("热更新 · 已连接", true));

  source.addEventListener("error", () => setBadge("热更新 · 重连中", false));

  source.addEventListener("change", (event) => {
    let changed = [];
    try {
      changed = JSON.parse(event.data).changed || [];
    } catch {
      changed = [];
    }
    setBadge(`热更新 · ${changed.join("/") || "资源"} 变更`, true);
    window.clearTimeout(timer);
    timer = window.setTimeout(() => location.reload(), RELOAD_DELAY);
  });
}
