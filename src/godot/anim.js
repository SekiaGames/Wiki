// 解析 <Model>_anims.tres（AnimationLibrary）→ 与 three.js 无关的动画描述。
// 轨道只有 position_3d / rotation_3d / scale_3d 三种，keys 结构为
//   PackedFloat32Array(time, transition, v0, v1, (...), ...)

import { parseGodotText } from "./tres.js";

const TRACK_KIND = {
  position_3d: { kind: "position", stride: 3 },
  rotation_3d: { kind: "quaternion", stride: 4 },
  scale_3d: { kind: "scale", stride: 3 },
};

export function parseAnimationLibrary(text) {
  const doc = parseGodotText(text);
  const subById = new Map(doc.subResources.map((entry) => [entry.id, entry]));
  const library = doc.resource._data || {};
  const clips = [];

  for (const [name, ref] of Object.entries(library)) {
    const id = ref && typeof ref === "object" ? ref.args?.[0] : ref;
    const sub = subById.get(id);
    if (!sub) continue;
    const clip = buildClip(name, sub.props);
    if (clip) clips.push(clip);
  }
  return clips;
}

function buildClip(name, props) {
  const length = Number(props.length ?? 0);
  const loop = props.loop_mode === 1;
  const groups = new Map();

  for (const [key, value] of Object.entries(props)) {
    const match = /^tracks\/(\d+)\/([A-Za-z_]+)$/.exec(key);
    if (!match) continue;
    const index = Number(match[1]);
    if (!groups.has(index)) groups.set(index, {});
    groups.get(index)[match[2]] = value;
  }

  const tracks = [];
  for (const group of groups.values()) {
    const spec = TRACK_KIND[group.type];
    if (!spec || !group.path) continue;
    const node = nodeNameFromPath(group.path.args?.[0] || "");
    if (!node) continue;
    const keys = Array.isArray(group.keys?.args) ? group.keys.args : [];
    const samples = buildSamples(keys, spec.stride);
    if (!samples.times.length) continue;
    tracks.push({ node, kind: spec.kind, ...samples });
  }

  if (!tracks.length) return null;
  return { name, length: length || lastTime(tracks[0]), loop, tracks };
}

function nodeNameFromPath(path) {
  const lastSegment = String(path).split("/").pop() || "";
  const parts = lastSegment.split(":");
  return parts[parts.length - 1].trim();
}

function buildSamples(keys, stride) {
  const step = 2 + stride;
  const times = [];
  const values = [];
  for (let i = 0; i + step <= keys.length; i += step) {
    const time = keys[i];
    if (typeof time !== "number") break;
    times.push(time);
    for (let j = 0; j < stride; j += 1) values.push(keys[i + 2 + j]);
  }
  return { times, values };
}

function lastTime(track) {
  return track.times[track.times.length - 1] || 0;
}

export function clipDuration(clip) {
  const trackMax = clip.tracks.reduce((max, track) => Math.max(max, track.times[track.times.length - 1] || 0), 0);
  return Math.max(clip.length || 0, trackMax);
}
