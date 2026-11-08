// 解析 materials/NN_matN.tres（ShaderMaterial）与 <Model>.tscn（geoset → 材质）。

import { parseGodotText, resPathToAsset } from "./tres.js";

export function parseModelScene(text) {
  const doc = parseGodotText(text);
  const extById = new Map(doc.extResources.map((entry) => [entry.id, entry]));

  const modelEntry = doc.extResources.find((entry) => entry.type === "PackedScene");
  const animEntry = doc.extResources.find((entry) => entry.type === "AnimationLibrary");

  const slots = [];
  const hidden = [];
  for (const node of doc.nodes) {
    if (node.props.visible === false) hidden.push(node.name);
    for (const [key, value] of Object.entries(node.props)) {
      if (!key.startsWith("surface_material_override/") || value?.k !== "ExtResource") continue;
      const ext = extById.get(value.args?.[0]);
      if (!ext) continue;
      slots.push({
        node: node.name,
        slot: Number(key.split("/")[1]),
        materialPath: resPathToAsset(ext.path),
        visible: node.props.visible !== false,
      });
    }
  }

  return {
    modelPath: modelEntry ? resPathToAsset(modelEntry.path) : "",
    animsPath: animEntry ? resPathToAsset(animEntry.path) : "",
    slots,
    hidden,
  };
}

export function parseMaterial(text) {
  const doc = parseGodotText(text);
  const extById = new Map(doc.extResources.map((entry) => [entry.id, entry.path]));
  const props = doc.resource;

  const textureRef = props["shader_parameter/base_texture"];
  const texturePath = textureRef?.k === "ExtResource" ? extById.get(textureRef.args?.[0]) : null;
  const detailRef = props["shader_parameter/detail_texture"];
  const detailPath = detailRef?.k === "ExtResource" ? extById.get(detailRef.args?.[0]) : null;

  const teamColor = props["shader_parameter/team_color"];
  const shaderEntry = doc.extResources.find((entry) => entry.type === "Shader");
  const shaderName = String(shaderEntry?.path || "").split("/").pop().replace(/\.gdshader$/i, "");
  const blendMode = shaderName.replace("war3_model_", "") || "opaque";

  return {
    name: props.resource_name || "",
    blendMode,
    baseTexture: resPathToAsset(texturePath),
    detailTexture: resPathToAsset(detailPath),
    alphaCutoff: numberOr(props["shader_parameter/alpha_cutoff"], 0),
    layerAlpha: numberOr(props["shader_parameter/layer_alpha"], 1),
    colorScale: numberOr(props["shader_parameter/color_scale"], 1),
    useTeamColor: props["shader_parameter/use_team_color"] === true,
    useDetail: props["shader_parameter/use_detail"] === true,
    useBaseTexture: props["shader_parameter/use_base_texture"] !== false,
    teamColor: Array.isArray(teamColor?.args) ? teamColor.args.map(Number).slice(0, 4) : null,
  };
}

function numberOr(value, fallback) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}
