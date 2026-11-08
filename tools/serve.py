#!/usr/bin/env python3
"""只读本地服务：给 Wiki 提供静态页面与资源目录访问。

- 静态根目录固定为仓库根（D:/WorldEditor），页面在 /Wiki/ 下
- /api/asset/* 只读 Godot/assets/converted，任何越界路径一律拒绝
- 全程不写任何文件、不建缓存目录
"""

import argparse
import json
import os
import posixpath
import sys
import time
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlparse

REPO_ROOT = Path(__file__).resolve().parents[2]
ASSET_ROOT = (REPO_ROOT / "Godot" / "assets" / "converted").resolve()
WIKI_ROOT = (REPO_ROOT / "Wiki").resolve()

WATCH_INTERVAL = 0.6
WATCH_PING = 15.0
DEFAULT_WATCH_DIRS = ["Tables", "Texts"]
VERBOSE = False
EXT_INDEX_CACHE = {}
BATCH_LIMIT = 80

TEXT_SUFFIX = {".json", ".tres", ".tscn", ".txt", ".wts", ".md"}
MIME_OVERRIDE = {
    ".glb": "model/gltf-binary",
    ".gltf": "model/gltf+json",
    ".wav": "audio/wav",
    ".tres": "text/plain; charset=utf-8",
    ".tscn": "text/plain; charset=utf-8",
    ".import": "text/plain; charset=utf-8",
}


def resolve_inside_assets(relative):
    candidate = (ASSET_ROOT / relative.replace("\\", "/")).resolve()
    if candidate != ASSET_ROOT and ASSET_ROOT not in candidate.parents:
        raise ValueError("路径越界")
    return candidate


def directory_listing(relative, depth, name_filter):
    base = resolve_inside_assets(relative) if relative else ASSET_ROOT
    if not base.is_dir():
        raise ValueError("不是目录")

    depth = max(1, min(int(depth), 8))
    entries = []

    def walk(current, level):
        try:
            children = sorted(os.scandir(current), key=lambda e: (not e.is_dir(), e.name.lower()))
        except OSError:
            return
        for child in children:
            rel = child.path[len(str(ASSET_ROOT)) + 1:].replace("\\", "/")
            if name_filter and name_filter.lower() not in child.name.lower():
                continue
            item = {
                "name": child.name,
                "path": rel,
                "dir": child.is_dir(),
            }
            if not item["dir"]:
                try:
                    item["size"] = child.stat().st_size
                except OSError:
                    item["size"] = 0
            entries.append(item)
            if item["dir"] and level + 1 < depth:
                walk(child.path, level + 1)

    walk(str(base), 0)
    return entries


def watch_targets(extra_dirs):
    targets = [(WIKI_ROOT, "wiki")]
    for relative in list(DEFAULT_WATCH_DIRS) + list(extra_dirs):
        try:
            path = resolve_inside_assets(relative)
        except ValueError:
            continue
        if path.is_dir():
            targets.append((path, relative))
    return targets


def snapshot(targets):
    stamps = {}
    for path, key in targets:
        count = 0
        newest = 0.0
        for current, _dirs, files in os.walk(path):
            for name in files:
                try:
                    stat = os.stat(os.path.join(current, name))
                except OSError:
                    continue
                count += 1
                if stat.st_mtime > newest:
                    newest = stat.st_mtime
        stamps[key] = [count, round(newest, 3)]
    return stamps


def ext_index(ext):
    ext = ext.lower()
    if not ext.startswith("."):
        ext = "." + ext
    cached = EXT_INDEX_CACHE.get(ext)
    if cached is not None:
        return cached
    prefix = len(str(ASSET_ROOT)) + 1
    files = []
    for current, _dirs, names in os.walk(ASSET_ROOT):
        for name in names:
            if name.lower().endswith(ext):
                files.append(os.path.join(current, name)[prefix:].replace("\\", "/"))
    files.sort()
    EXT_INDEX_CACHE[ext] = files
    return files


def read_asset_text(relative):
    target = resolve_inside_assets(relative)
    if not target.is_file():
        raise ValueError("不是文件")
    suffix = target.suffix.lower()
    if suffix == ".json":
        return json.loads(target.read_text(encoding="utf-8"))
    if suffix in TEXT_SUFFIX:
        return target.read_text(encoding="utf-8", errors="replace")
    raise ValueError("只允许读取文本类资源")


class Handler(SimpleHTTPRequestHandler):
    server_version = "WorldEditorWiki/0.2"

    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(REPO_ROOT), **kwargs)

    def end_headers(self):
        if self.path.startswith("/api/"):
            self.send_header("Cache-Control", "no-store")
        self.send_header("Access-Control-Allow-Origin", "*")
        super().end_headers()

    def guess_type(self, path):
        suffix = posixpath.splitext(path)[1].lower()
        if suffix in MIME_OVERRIDE:
            return MIME_OVERRIDE[suffix]
        return super().guess_type(path)

    def do_GET(self):
        if self.path == "/" or self.path == "":
            self.send_response(302)
            self.send_header("Location", "/Wiki/index.html")
            self.end_headers()
            return
        if self.path.startswith("/api/watch"):
            self.handle_watch()
            return
        if self.path.startswith("/api/asset/"):
            self.handle_asset_api()
            return
        super().do_GET()

    def send_json(self, payload, status=200):
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def handle_watch(self):
        parsed = urlparse(self.path)
        query = parse_qs(parsed.query)
        extra = [
            part.strip().strip("/")
            for part in unquote(query.get("dirs", [""])[0]).split(",")
            if part.strip()
        ]
        targets = watch_targets(extra)

        self.close_connection = True
        self.send_response(200)
        self.send_header("Content-Type", "text/event-stream; charset=utf-8")
        self.send_header("Cache-Control", "no-cache, no-transform")
        self.send_header("Connection", "close")
        self.send_header("X-Accel-Buffering", "no")
        self.end_headers()

        try:
            self.wfile.write(b": hello\n\n")
            self.wfile.flush()
        except (BrokenPipeError, ConnectionResetError, OSError):
            return

        last = snapshot(targets)
        last_ping = time.monotonic()
        while True:
            time.sleep(WATCH_INTERVAL)
            current = snapshot(targets)
            changed = [key for key in current if current.get(key) != last.get(key)]
            try:
                if changed:
                    payload = json.dumps({"changed": changed, "stamp": current}, ensure_ascii=False)
                    self.wfile.write(f"event: change\ndata: {payload}\n\n".encode("utf-8"))
                    self.wfile.flush()
                    last = current
                    last_ping = time.monotonic()
                elif time.monotonic() - last_ping > WATCH_PING:
                    self.wfile.write(b": ping\n\n")
                    self.wfile.flush()
                    last_ping = time.monotonic()
            except (BrokenPipeError, ConnectionResetError, OSError):
                return

    def handle_asset_api(self):
        parsed = urlparse(self.path)
        query = parse_qs(parsed.query)
        relative = unquote(query.get("path", [""])[0]).strip("/")
        try:
            if parsed.path == "/api/asset/list":
                entries = directory_listing(
                    relative,
                    query.get("depth", ["1"])[0],
                    query.get("q", [""])[0],
                )
                self.send_json({"ok": True, "path": relative, "count": len(entries), "entries": entries})
                return
            if parsed.path == "/api/asset/index":
                ext = unquote(query.get("ext", [".glb"])[0])
                files = ext_index(ext)
                self.send_json({"ok": True, "ext": ext, "count": len(files), "files": files})
                return
            if parsed.path == "/api/asset/batch":
                raw = unquote(query.get("paths", [""])[0])
                paths = [part.strip() for part in raw.split(",") if part.strip()][:BATCH_LIMIT]
                items = {}
                for path in paths:
                    try:
                        items[path] = {"ok": True, "data": read_asset_text(path)}
                    except (ValueError, OSError, json.JSONDecodeError) as exc:
                        items[path] = {"ok": False, "error": str(exc)}
                self.send_json({"ok": True, "count": len(items), "items": items})
                return
            if parsed.path == "/api/asset/read":
                data = read_asset_text(relative)
                if isinstance(data, str):
                    self.send_json({"ok": True, "path": relative, "text": data})
                else:
                    self.send_json({"ok": True, "path": relative, "json": data})
                return
            self.send_json({"ok": False, "error": "未知接口"}, 404)
        except FileNotFoundError:
            self.send_json({"ok": False, "error": "文件不存在"}, 404)
        except ValueError as exc:
            self.send_json({"ok": False, "error": str(exc)}, 400)
        except OSError as exc:
            self.send_json({"ok": False, "error": f"读取失败：{exc}"}, 500)

    def log_message(self, fmt, *args):
        if VERBOSE or self.path.startswith("/api/"):
            sys.stderr.write("[api] %s\n" % (fmt % args))


def main():
    global VERBOSE
    parser = argparse.ArgumentParser(description="WorldEditor 资源预览 Wiki 只读服务")
    parser.add_argument("--port", type=int, default=8123)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--verbose", action="store_true", help="打印全部请求（默认只打印 /api/）")
    args = parser.parse_args()
    VERBOSE = args.verbose

    if not ASSET_ROOT.is_dir():
        print(f"资源目录不存在：{ASSET_ROOT}")
        return 1

    server = ThreadingHTTPServer((args.host, args.port), Handler)
    print(f"Wiki:   http://{args.host}:{args.port}/Wiki/index.html")
    print(f"资源根: {ASSET_ROOT}")
    print("只读服务，Ctrl+C 退出")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n已退出")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
