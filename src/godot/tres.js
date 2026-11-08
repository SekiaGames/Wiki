// Godot 文本资源（.tres / .tscn）解析。
// 只处理 wiki 需要的那部分：section 头、ext/sub resource、node、键值（含 PackedFloat32Array 与字典）。

export function parseGodotText(text) {
  const doc = {
    header: {},
    extResources: [],
    subResources: [],
    nodes: [],
    resource: {},
  };

  let current = doc.resource;

  const lines = String(text).split(/\r?\n/);
  for (let i = 0; i < lines.length; i += 1) {
    const line = lines[i].trim();
    if (!line || line.startsWith(";")) continue;

    if (line.startsWith("[")) {
      const section = parseSection(line);
      if (section.kind === "gd_resource" || section.kind === "gd_scene") {
        doc.header = section.attrs;
        current = doc.resource;
      } else if (section.kind === "ext_resource") {
        doc.extResources.push(section.attrs);
        current = null;
      } else if (section.kind === "sub_resource") {
        const entry = { ...section.attrs, props: {} };
        doc.subResources.push(entry);
        current = entry.props;
      } else if (section.kind === "node") {
        const entry = { ...section.attrs, props: {} };
        doc.nodes.push(entry);
        current = entry.props;
      } else {
        current = doc.resource;
      }
      continue;
    }

    if (!current) continue;
    const eq = line.indexOf("=");
    if (eq < 0) continue;

    const key = line.slice(0, eq).trim();
    let valueText = line.slice(eq + 1).trim();
    while (!isBalanced(valueText) && i + 1 < lines.length) {
      i += 1;
      valueText += "\n" + lines[i];
    }
    current[key] = parseValue(valueText);
  }

  return doc;
}

function parseSection(line) {
  const inner = line.replace(/^\[/, "").replace(/\]$/, "");
  const kind = inner.split(/[\s\]]/)[0];
  const attrs = {};
  const pattern = /([A-Za-z0-9_./-]+)=("([^"]*)"|[-\d.]+|true|false)/g;
  let match;
  while ((match = pattern.exec(inner))) {
    const value = match[3] !== undefined ? match[3] : match[2];
    attrs[match[1]] = /^-?\d+(\.\d+)?$/.test(value) ? Number(value) : value;
  }
  return { kind, attrs };
}

export function isBalanced(text) {
  let depth = 0;
  let inString = false;
  for (let i = 0; i < text.length; i += 1) {
    const ch = text[i];
    if (inString) {
      if (ch === "\\") i += 1;
      else if (ch === '"') inString = false;
      continue;
    }
    if (ch === '"') inString = true;
    else if (ch === "(" || ch === "[" || ch === "{") depth += 1;
    else if (ch === ")" || ch === "]" || ch === "}") depth -= 1;
  }
  return depth <= 0;
}

export function parseValue(text) {
  const value = String(text).trim();
  if (!value) return null;

  if (value.startsWith("{") && value.endsWith("}")) {
    const result = {};
    for (const entry of splitTopLevel(value.slice(1, -1))) {
      const colon = indexOfTopLevelColon(entry);
      if (colon < 0) continue;
      const key = parseValue(entry.slice(0, colon));
      result[String(key)] = parseValue(entry.slice(colon + 1));
    }
    return result;
  }

  if (value.startsWith("[") && value.endsWith("]")) {
    return splitTopLevel(value.slice(1, -1)).map(parseValue);
  }

  if (value.startsWith('&"')) return unquote(value.slice(1));
  if (value.startsWith('"')) return unquote(value);
  if (value === "true") return true;
  if (value === "false") return false;
  if (/^-?\d+$/.test(value)) return Number(value);
  if (/^-?(\d+\.\d*|\.\d+|\d+)([eE][-+]?\d+)?$/.test(value)) return Number(value);

  const call = /^([A-Za-z0-9_]+)\(([\s\S]*)\)$/.exec(value);
  if (call) {
    const args = splitTopLevel(call[2]).map(parseValue);
    return { k: call[1], args, raw: value };
  }

  return value;
}

export function splitTopLevel(text) {
  const parts = [];
  let depth = 0;
  let inString = false;
  let buffer = "";
  for (let i = 0; i < text.length; i += 1) {
    const ch = text[i];
    if (inString) {
      buffer += ch;
      if (ch === "\\") {
        i += 1;
        buffer += text[i] ?? "";
      } else if (ch === '"') {
        inString = false;
      }
      continue;
    }
    if (ch === '"') {
      inString = true;
      buffer += ch;
      continue;
    }
    if (ch === "(" || ch === "[" || ch === "{") depth += 1;
    else if (ch === ")" || ch === "]" || ch === "}") depth -= 1;
    if (ch === "," && depth === 0) {
      parts.push(buffer);
      buffer = "";
      continue;
    }
    buffer += ch;
  }
  if (buffer.trim()) parts.push(buffer);
  return parts.map((part) => part.trim()).filter(Boolean);
}

function indexOfTopLevelColon(text) {
  let depth = 0;
  let inString = false;
  for (let i = 0; i < text.length; i += 1) {
    const ch = text[i];
    if (inString) {
      if (ch === "\\") i += 1;
      else if (ch === '"') inString = false;
      continue;
    }
    if (ch === '"') inString = true;
    else if (ch === "(" || ch === "[" || ch === "{") depth += 1;
    else if (ch === ")" || ch === "]" || ch === "}") depth -= 1;
    else if (ch === ":" && depth === 0) return i;
  }
  return -1;
}

function unquote(text) {
  const body = text.replace(/^"|"$/g, "");
  return body.replace(/\\(["\\nt])/g, (_, ch) => {
    if (ch === "n") return "\n";
    if (ch === "t") return "\t";
    return ch;
  });
}

export function resPathToAsset(resPath) {
  if (!resPath) return "";
  return String(resPath).replace(/^res:\/\/assets\/converted\//, "").replace(/^res:\/\//, "");
}
