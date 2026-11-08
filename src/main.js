import { parseHash, onRouteChange } from "./router.js";
import { renderSidebar } from "./ui/sidebar.js";
import { renderTopbar } from "./ui/topbar.js";
import { renderOverview } from "./ui/overview.js";
import { renderList } from "./ui/list.js";
import { renderDetail, disposeDetailViewer } from "./ui/detail.js";
import { PENDING_CATEGORIES } from "./store.js";
import { startHotReload } from "./dev/live.js";

const sidebar = document.getElementById("sidebar");
const topbar = document.getElementById("topbar");
const view = document.getElementById("view");

let renderToken = 0;

async function render() {
  const route = parseHash();
  const token = ++renderToken;
  const isStale = () => token !== renderToken;

  disposeDetailViewer();
  renderSidebar(sidebar, route);
  renderTopbar(topbar, route);

  if (route.category === "overview") {
    renderOverview(view, route);
    return;
  }

  if (PENDING_CATEGORIES.has(route.category) && route.id) {
    await renderList(view, route, isStale);
    return;
  }

  if (route.id) {
    await renderDetail(view, route, isStale);
    return;
  }

  await renderList(view, route, isStale);
}

onRouteChange(render);
render();
startHotReload();
