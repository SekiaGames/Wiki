// three.js 模型预览器：glb + materials/*.tres + _anims.tres。
// War3 模型是贴图烘焙光照，这里统一用 MeshBasicMaterial（无光）呈现。

import * as THREE from "three";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import { assetUrl } from "./api.js";

const textureLoader = new THREE.TextureLoader();
const textureCache = new Map();
const gltfLoader = new GLTFLoader();

// War3 材质最多两层：base_texture（或被队伍色替换）+ detail_texture 按自身 alpha 叠加。
// 语义对齐 Godot 侧 war3_model.gdshaderinc 的 war3_sample()：
//   col = use_base_texture ? texture(base, uv) : base_color
//   if (use_team_color) col.rgb = team_color.rgb
//   if (use_detail)     { d = texture(detail, uv); col.rgb = mix(col.rgb, d.rgb, d.a); col.a = max(col.a, d.a) }
function loadTexture(path) {
  if (!path) return null;
  if (!textureCache.has(path)) {
    const texture = textureLoader.load(assetUrl(path));
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.flipY = false;
    texture.anisotropy = 4;
    textureCache.set(path, texture);
  }
  return textureCache.get(path);
}

const whitePixel = (() => {
  const texture = new THREE.DataTexture(new Uint8Array([255, 255, 255, 255]), 1, 1);
  texture.needsUpdate = true;
  return texture;
})();

const TWO_LAYER_KEY = "war3-two-layer";

// 默认姿势：优先 "Stand" 系列（排除 Alternate 形态），名字最短的那个最贴近基础待机
// （"Stand - 1" 优于 "Stand Victory"）；没有就退到 Walk。
function pickDefaultClip(names) {
  const usable = names.filter((name) => !/^alternate/i.test(name) && !/alternate$/i.test(name));
  const pool = usable.length ? usable : names;
  const standMatches = pool
    .filter((name) => /^stand/i.test(name))
    .sort((a, b) => a.length - b.length);
  if (standMatches.length) return standMatches[0];
  return pool.find((name) => /^walk/i.test(name)) || pool[0];
}

function applyTwoLayer(material, detailTexture) {
  material.onBeforeCompile = (shader) => {
    shader.uniforms.detailMap = { value: detailTexture };
    shader.fragmentShader = shader.fragmentShader
      .replace(
        "#include <common>",
        `#include <common>
uniform sampler2D detailMap;`,
      )
      .replace(
        "#include <map_fragment>",
        `#include <map_fragment>
{
  vec4 war3Detail = texture2D(detailMap, vMapUv);
  diffuseColor.rgb = mix(diffuseColor.rgb, war3Detail.rgb, war3Detail.a);
  diffuseColor.a = max(diffuseColor.a, war3Detail.a);
}`,
      );
  };
  material.customProgramCacheKey = () => TWO_LAYER_KEY;
}

export const TEAM_COLORS = [
  { id: "neutral", label: "中立", hex: 0x8c8c96 },
  { id: "red", label: "红", hex: 0xff0303 },
  { id: "blue", label: "蓝", hex: 0x0042ff },
  { id: "teal", label: "青", hex: 0x1ce6b9 },
  { id: "purple", label: "紫", hex: 0x540081 },
  { id: "yellow", label: "黄", hex: 0xfffc00 },
  { id: "orange", label: "橙", hex: 0xfe8a0e },
  { id: "green", label: "绿", hex: 0x20c000 },
  { id: "brown", label: "棕", hex: 0x4f2b05 },
];

export class ModelViewer {
  constructor(container) {
    this.container = container;
    this.disposed = false;
    this.root = null;
    this.mixer = null;
    this.actions = new Map();
    this.current = null;
    this.speed = 1;
    this.wireframe = false;
    this.teamColorHex = 0x8c8c96;

    this.scene = new THREE.Scene();
    this.camera = new THREE.PerspectiveCamera(32, 1, 0.5, 200000);
    this.camera.position.set(140, 90, 190);

    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    this.renderer.setClearColor(0x000000, 0);
    this.renderer.domElement.style.width = "100%";
    this.renderer.domElement.style.height = "100%";
    this.renderer.domElement.style.display = "block";
    container.append(this.renderer.domElement);

    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.08;
    this.controls.minDistance = 20;
    this.controls.maxDistance = 20000;

    this.grid = new THREE.GridHelper(400, 20, 0x2c2c38, 0x1c1c24);
    this.grid.position.y = -0.2;
    this.scene.add(this.grid);

    this.resizeObserver = new ResizeObserver(() => {
      if (this.resizePending) return;
      this.resizePending = true;
      requestAnimationFrame(() => {
        this.resizePending = false;
        this.resize();
      });
    });
    this.resizeObserver.observe(container);
    this.resize();

    this.clock = new THREE.Clock();
    this.renderer.setAnimationLoop(() => this.tick());
  }

  resize() {
    const { clientWidth, clientHeight } = this.container;
    if (!clientWidth || !clientHeight) return;
    this.camera.aspect = clientWidth / clientHeight;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(clientWidth, clientHeight, false);
  }

  tick() {
    if (this.disposed) return;
    const delta = this.clock.getDelta();
    if (this.mixer) this.mixer.update(delta * this.speed);
    this.controls.update();
    this.renderer.render(this.scene, this.camera);
  }

  async load(model) {
    this.disposeModel();

    const gltf = await gltfLoader.loadAsync(assetUrl(model.glb));
    const root = gltf.scene;
    this.root = root;

    const materialsByNode = new Map(model.slots.map((slot) => [slot.node, slot]));
    const hiddenNodes = new Set(model.hidden || []);
    const uniqueMaterials = new Map();

    const noSkin = new URLSearchParams(location.search).get("noskin") === "1";
    const replacements = [];

    root.traverse((child) => {
      if (!child.isMesh) return;
      const slot = materialsByNode.get(child.name);
      const key = slot ? slot.materialPath || slot.material?.name : `__default_${child.name}`;
      let material = uniqueMaterials.get(key);
      if (!material) {
        material = this.buildMaterial(slot?.material, child.geometry);
        uniqueMaterials.set(key, material);
      }
      child.material = material;
      child.frustumCulled = false;
      if (slot?.visible === false || hiddenNodes.has(child.name)) child.visible = false;
      if (noSkin && child.isSkinnedMesh) replacements.push(child);
    });

    for (const mesh of replacements) {
      const plain = new THREE.Mesh(mesh.geometry, mesh.material);
      plain.name = mesh.name;
      plain.position.copy(mesh.position);
      plain.quaternion.copy(mesh.quaternion);
      plain.scale.copy(mesh.scale);
      mesh.parent.add(plain);
      mesh.parent.remove(mesh);
    }

    this.scene.add(root);
    this.hideOversizedPlanes(root);
    this.unresolved = 0;
    this.buildClips(root, model.clips || []);
    if (this.unresolved) console.warn(`[viewer] 未绑定的动画轨道 ${this.unresolved} 条`);
    this.frameObject(root);
    return { clips: [...this.actions.keys()] };
  }

  buildMaterial(spec, geometry) {
    const hasVertexColors = Boolean(geometry?.attributes?.color);
    const blendMode = spec?.blendMode || "opaque";
    const basePath = spec && spec.useBaseTexture && !spec.useTeamColor ? spec.baseTexture : "";
    const detailPath = spec?.useDetail ? spec.detailTexture : "";
    const detailTexture = loadTexture(detailPath);
    const baseTexture = loadTexture(basePath) || (detailTexture ? whitePixel : null);

    // TeamGlow：整壳的纯色加色层，游戏里按队伍色渲染且很淡，这里必须压亮度，否则会把模型糊成一片白
    const isTeamGlow = /TeamGlow/i.test(spec?.baseTexture || "");
    const tinted = Boolean(spec?.useTeamColor) || isTeamGlow;
    const glowScale = isTeamGlow ? 0.15 : 1;

    const alphaCutoff = spec ? Math.max(spec.alphaCutoff, 0.004) : 0;
    const opacity = spec ? spec.layerAlpha : 1;

    const material = new THREE.MeshBasicMaterial({
      map: baseTexture,
      transparent: opacity < 1,
      opacity,
      alphaTest: alphaCutoff,
      side: THREE.DoubleSide,
      vertexColors: hasVertexColors,
      color: new THREE.Color(1, 1, 1),
      depthWrite: true,
    });

    if (detailTexture && detailPath !== basePath) applyTwoLayer(material, detailTexture);

    if (blendMode === "add") {
      material.blending = THREE.AdditiveBlending;
      material.transparent = true;
      material.depthWrite = false;
    } else if (blendMode === "blend") {
      material.transparent = true;
      material.depthWrite = false;
    } else if (blendMode === "mul") {
      material.blending = THREE.MultiplyBlending;
      material.transparent = true;
      material.depthWrite = false;
    }

    material.userData.teamTinted = tinted;
    material.userData.tintScale = glowScale;
    if (tinted) material.color.setHex(this.teamColorHex).multiplyScalar(glowScale);
    return material;
  }

  setTeamColor(hex) {
    this.teamColorHex = hex;
    this.root?.traverse((child) => {
      if (!child.isMesh) return;
      for (const material of [].concat(child.material)) {
        if (!material.userData?.teamTinted) continue;
        const scale = material.userData.tintScale ?? 1;
        material.color.setHex(hex).multiplyScalar(scale);
      }
    });
  }

  buildClips(root, clipDescs) {
    this.mixer = new THREE.AnimationMixer(root);
    this.actions.clear();

    for (const desc of clipDescs) {
      const tracks = [];
      for (const track of desc.tracks) {
        const target = root.getObjectByName(track.node);
        if (!target) {
          this.unresolved = (this.unresolved || 0) + 1;
          continue;
        }
        const duration = track.times[track.times.length - 1] || 0;
        if (duration <= 0) continue;
        if (track.kind === "position") {
          tracks.push(new THREE.VectorKeyframeTrack(`${target.uuid}.position`, track.times, track.values));
        } else if (track.kind === "quaternion") {
          tracks.push(new THREE.QuaternionKeyframeTrack(`${target.uuid}.quaternion`, track.times, track.values));
        } else if (track.kind === "scale") {
          tracks.push(new THREE.VectorKeyframeTrack(`${target.uuid}.scale`, track.times, track.values));
        }
      }
      if (!tracks.length) continue;
      const duration = Math.max(desc.length || 0, tracks.reduce((max, t) => Math.max(max, t.times[t.times.length - 1]), 0));
      const clip = new THREE.AnimationClip(desc.name, duration, tracks);
      const action = this.mixer.clipAction(clip);
      action.loop = desc.loop ? THREE.LoopRepeat : THREE.LoopOnce;
      action.clampWhenFinished = !desc.loop;
      this.actions.set(desc.name, action);
    }

    const preferred = pickDefaultClip([...this.actions.keys()]);
    const skip = new URLSearchParams(location.search).get("noanim") === "1";
    if (preferred && !skip) this.play(preferred);
  }

  setGeosetVisible(nodeName, visible) {
    const node = this.root?.getObjectByName(nodeName);
    if (!node) return false;
    node.visible = visible;
    return true;
  }

  geosetInfo() {
    const list = [];
    this.root?.traverse((child) => {
      if (!child.isMesh) return;
      const geometry = child.geometry;
      geometry?.computeBoundingBox?.();
      const box = geometry?.boundingBox;
      const size = box ? box.getSize(new THREE.Vector3()) : null;
      list.push({
        node: child.name,
        vertices: geometry?.attributes?.position?.count || 0,
        visible: child.visible,
        size: size ? [size.x, size.y, size.z].map((value) => Math.round(value)) : null,
      });
    });
    return list;
  }

  // 建筑模型里有 4~8 顶点的超大平面（Farm 1139×859、TownHall 340×871×1178），
  // 在游戏里由专门的遮挡/阴影层处理，glb 里只是一块普通面片，渲染出来是黑幕。
  // 这里按「顶点极少 + 尺度远大于本体」自动隐藏，详情页「模型」标签可以再打开。
  hideOversizedPlanes(root) {
    const meshes = [];
    root.traverse((child) => {
      if (!child.isMesh) return;
      const geometry = child.geometry;
      geometry?.computeBoundingBox?.();
      const box = geometry?.boundingBox;
      if (!box) return;
      const size = box.getSize(new THREE.Vector3());
      meshes.push({
        mesh: child,
        vertices: geometry.attributes?.position?.count || 0,
        maxDim: Math.max(size.x, size.y, size.z),
      });
    });
    if (meshes.length < 3) return;

    const bodyDims = meshes
      .filter((row) => row.vertices > 50)
      .map((row) => row.maxDim)
      .sort((a, b) => a - b);
    if (!bodyDims.length) return;
    const bodyRef = bodyDims[Math.floor(bodyDims.length / 2)];

    this.autoHidden = [];
    for (const row of meshes) {
      if (row.vertices > 8) continue;
      if (row.maxDim < bodyRef * 2.2) continue;
      row.mesh.visible = false;
      this.autoHidden.push(row.mesh.name);
    }
  }

  play(name) {
    const action = this.actions.get(name);
    if (!action) return false;
    if (this.current && this.current !== action) this.current.fadeOut(0.12);
    action.reset();
    action.fadeIn(0.12);
    action.play();
    this.current = action;
    this.currentName = name;
    return true;
  }

  bindPose() {
    if (this.mixer) this.mixer.stopAllAction();
    this.current = null;
    this.currentName = null;
    this.root?.traverse((child) => {
      if (child.isSkinnedMesh) child.skeleton.pose();
    });
  }

  setSpeed(speed) {
    this.speed = speed;
  }

  setWireframe(enabled) {    this.wireframe = enabled;
    this.root?.traverse((child) => {
      if (!child.isMesh) return;
      for (const material of [].concat(child.material)) {
        material.wireframe = enabled;
      }
    });
  }

  frameObject(object) {
    object.updateWorldMatrix(true, true);
    const box = new THREE.Box3();
    const scratch = new THREE.Box3();
    object.traverse((child) => {
      if (!child.isMesh || !child.visible) return;
      const geometry = child.geometry;
      if (!geometry) return;
      if (!geometry.boundingBox) geometry.computeBoundingBox();
      if (!geometry.boundingBox) return;
      scratch.copy(geometry.boundingBox).applyMatrix4(child.matrixWorld);
      box.union(scratch);
    });
    if (box.isEmpty()) box.setFromObject(object);

    const size = box.getSize(new THREE.Vector3());
    const center = box.getCenter(new THREE.Vector3());
    const radius = Math.max(size.x, size.y, size.z) * 0.5 || 100;

    this.controls.target.copy(center);
    const params = new URLSearchParams(location.search);
    const zoom = Number(params.get("zoom") || 1) || 1;
    const elevation = params.has("elev") ? Number(params.get("elev")) : 0.42;
    const distance = (radius / Math.tan((this.camera.fov * Math.PI) / 360)) * 1.55 * zoom;
    const direction = new THREE.Vector3(0.55, Number.isFinite(elevation) ? elevation : 0.42, 1).normalize();
    this.camera.position.copy(center).addScaledVector(direction, distance);
    this.camera.near = Math.max(radius / 200, 0.1);
    this.camera.far = distance * 12;
    this.camera.updateProjectionMatrix();
    this.controls.minDistance = radius * 0.35;
    this.controls.maxDistance = radius * 12;
    this.controls.update();

    const gridSize = Math.ceil(radius * 2.6 / 100) * 100 || 400;
    this.grid.scale.setScalar(gridSize / 400);
    this.grid.position.set(center.x, box.min.y - radius * 0.02, center.z);
    this.grid.material.opacity = 0.5;
    this.grid.material.transparent = true;
  }

  screenshot() {
    this.renderer.render(this.scene, this.camera);
    return this.renderer.domElement.toDataURL("image/png");
  }

  disposeModel() {
    if (this.mixer) {
      this.mixer.stopAllAction();
      this.mixer = null;
    }
    this.actions.clear();
    this.current = null;
    if (!this.root) return;
    this.scene.remove(this.root);
    this.root.traverse((child) => {
      if (child.isMesh) child.geometry?.dispose();
    });
    this.root = null;
  }

  dispose() {
    this.disposed = true;
    this.renderer.setAnimationLoop(null);
    this.disposeModel();
    this.resizeObserver.disconnect();
    this.controls.dispose();
    this.renderer.dispose();
    this.renderer.domElement.remove();
  }
}
