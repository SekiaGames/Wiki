using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ModelPreview : SubViewportContainer
{
    readonly SubViewport _viewport;
    readonly Node3D _pivot;
    readonly Camera3D _camera;
    Node _model;
    AnimationPlayer _player;
    Vector3 _focus;
    float _distance = 3f;
    float _zoom = 1f;
    bool _dragging;
    Color _teamColor = Color.FromHtml("c8322b");
    bool _teamColorEnabled;

    public event System.Action Loaded;

    public string ScenePath { get; private set; }
    public string Error { get; private set; }
    public List<string> Animations { get; } = new();

    public ModelPreview()
    {
        Stretch = true;
        CustomMinimumSize = new Vector2(520, 420);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Drag;

        _viewport = new SubViewport
        {
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            OwnWorld3D = true,
            HandleInputLocally = false,
            Msaa3D = Viewport.Msaa.Msaa4X,
        };
        AddChild(_viewport);

        Godot.Environment environment = new()
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = Color.FromHtml("0d0d12"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Color.FromHtml("d8d0c4"),
            AmbientLightEnergy = 0.6f,
            TonemapMode = Godot.Environment.ToneMapper.Linear,
        };
        WorldEnvironment world = new() { Environment = environment };
        _viewport.AddChild(world);

        _camera = new Camera3D { Fov = 42f, Near = 0.05f, Far = 2000f };
        _viewport.AddChild(_camera);

        DirectionalLight3D light = new()
        {
            LightEnergy = 0.8f,
            RotationDegrees = new Vector3(-38, -34, 0),
            ShadowEnabled = false,
        };
        _viewport.AddChild(light);

        _pivot = new Node3D();
        _viewport.AddChild(_pivot);
    }

    string _pendingScene;

    public void ShowScene(string relativeScenePath)
    {
        _pendingScene = relativeScenePath;
        CallDeferred(nameof(FlushPending));
    }

    void FlushPending()
    {
        if (_pendingScene == null) return;

        if (!_viewport.IsInsideTree())
        {
            CallDeferred(nameof(FlushPending));
            return;
        }

        string path = _pendingScene;
        _pendingScene = null;
        LoadScene(path);
    }

    void LoadScene(string relativeScenePath)
    {
        ClearModel();
        Error = null;
        ScenePath = relativeScenePath;

        PackedScene packed = Assets.LoadScene(relativeScenePath);
        if (packed == null)
        {
            Error = Assets.PackMounted ? $"模型不存在：{relativeScenePath}" : "extract.pck 未挂载，无法加载模型";
            return;
        }

        _model = packed.Instantiate();
        _pivot.AddChild(_model);

        foreach (Node node in Walk(_model))
        {
            if (node is AnimationTree tree) tree.Active = false;
        }

        _player = _model.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
        Animations.Clear();
        if (_player != null)
        {
            foreach (StringName libraryName in _player.GetAnimationLibraryList())
            {
                AnimationLibrary library = _player.GetAnimationLibrary(libraryName);
                if (library == null) continue;
                foreach (StringName animation in library.GetAnimationList())
                {
                    string name = animation.ToString();
                    if (name.Length > 0) Animations.Add(name);
                }
            }
        }

        ApplyDetailLayers(Data.MaterialDetailAlpha(Data.Wc3JsonFor(relativeScenePath)));
        Frame();
        ApplyTeamColor();
        Loaded?.Invoke();
    }

    public void Frame()
    {
        Aabb box = MergeBounds();
        if (box.Size.Length() <= 0.001f)
        {
            _focus = Vector3.Zero;
            _distance = 4f;
        }
        else
        {
            _focus = box.GetCenter();
            float aspect = _viewport.Size.Y > 1f ? _viewport.Size.X / _viewport.Size.Y : 1.6f;
            float fitted = Mathf.Max(box.Size.Y * 0.5f, box.Size.X * 0.5f / Mathf.Max(aspect, 0.5f));
            _distance = Mathf.Max(fitted, 0.3f) / Mathf.Tan(Mathf.DegToRad(_camera.Fov * 0.5f)) * 1.12f + box.Size.Z * 0.5f;
        }

        _zoom = 1f;
        UpdateCamera();
    }

    void UpdateCamera()
    {
        float distance = _distance * _zoom;
        _camera.Position = _focus + new Vector3(0, _focus.Y * 0.05f, distance);
        _camera.LookAt(_focus, Vector3.Up);
    }

    Aabb MergeBounds()
    {
        List<Aabb> boxes = new();

        foreach (Node node in Walk(_model))
        {
            if (node is MeshInstance3D { Mesh: not null } mesh && mesh.IsVisibleInTree() && !IsGlowMesh(mesh))
            {
                Aabb box = mesh.GlobalTransform * mesh.GetAabb();
                if (box.Size.Length() > 0.001f) boxes.Add(box);
            }
        }

        if (boxes.Count == 0) return new Aabb(Vector3.Zero, Vector3.Zero);

        List<float> sizes = boxes.Select(box => box.Size.Length()).OrderBy(value => value).ToList();
        float median = sizes[sizes.Count / 2];
        float limit = Mathf.Max(median * 2.2f, median + 1f);

        Aabb merged = default;
        bool has = false;
        foreach (Aabb box in boxes)
        {
            if (box.Size.Length() > limit) continue;
            merged = has ? merged.Merge(box) : box;
            has = true;
        }

        return has ? merged : boxes[0];
    }

    public void Play(string animation)
    {
        if (_player == null || string.IsNullOrEmpty(animation)) return;
        if (!Animations.Contains(animation)) return;
        _player.Play(animation);
    }

    public void SetSpeed(float speed)
    {
        if (_player != null) _player.SpeedScale = speed;
    }

    public void SetTeamColor(Color color, bool enabled)
    {
        _teamColor = color;
        _teamColorEnabled = enabled;
        ApplyTeamColor();
    }

    static int MaterialIndex(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath)) return -1;
        int mark = resourcePath.LastIndexOf(".mat", StringComparison.Ordinal);
        if (mark < 0) return -1;

        int start = mark + 4;
        int end = resourcePath.IndexOf('.', start);
        if (end < 0) return -1;
        return int.TryParse(resourcePath[start..end], out int index) ? index : -1;
    }

    void ApplyDetailLayers(float[] detailAlpha)
    {
        if (_model == null || detailAlpha == null) return;

        foreach (Node node in Walk(_model))
        {
            if (node is not MeshInstance3D { Mesh: not null } mesh) continue;

            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                Material material = mesh.GetSurfaceOverrideMaterial(i) ?? mesh.Mesh.SurfaceGetMaterial(i);
                if (material is not ShaderMaterial shader) continue;
                if (!shader.GetShaderParameter("use_detail").AsBool()) continue;

                int index = MaterialIndex(material.ResourcePath);
                if (index < 0 || index >= detailAlpha.Length) continue;
                if (detailAlpha[index] >= 0.01f) continue;

                shader.SetShaderParameter("use_detail", false);
            }
        }
    }

    static bool IsGlowMesh(MeshInstance3D mesh)
    {
        if (mesh.Mesh == null) return false;

        for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
        {
            Material material = mesh.GetSurfaceOverrideMaterial(i) ?? mesh.Mesh.SurfaceGetMaterial(i);
            if (material is not ShaderMaterial shader) continue;

            Texture2D texture = shader.GetShaderParameter("base_texture").As<Texture2D>();
            if (texture != null && texture.ResourcePath.Contains("TeamGlow", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    void ApplyTeamColor()
    {
        if (_model == null) return;

        foreach (Node node in Walk(_model))
        {
            if (node is not MeshInstance3D { Mesh: not null } mesh) continue;

            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                Material material = mesh.GetSurfaceOverrideMaterial(i) ?? mesh.Mesh.SurfaceGetMaterial(i);
                if (material is not ShaderMaterial shader) continue;

                Texture2D texture = shader.GetShaderParameter("base_texture").As<Texture2D>();
                bool glow = texture != null && texture.ResourcePath.Contains("TeamGlow", StringComparison.OrdinalIgnoreCase);

                bool usesTeam = shader.GetShaderParameter("use_team_color").AsBool();
                if (!usesTeam && !glow) continue;

                Color tint = _teamColorEnabled ? _teamColor : Color.FromHtml("9b98a5");
                shader.SetShaderParameter("use_team_color", true);
                shader.SetShaderParameter("team_color", tint);
                if (glow) shader.SetShaderParameter("layer_alpha", 0f);
            }
        }
    }

    void ClearModel()
    {
        if (_model == null) return;
        _pivot.RemoveChild(_model);
        _model.QueueFree();
        _model = null;
        _player = null;
        Animations.Clear();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            _dragging = button.Pressed;
        }

        if (@event is InputEventMouseMotion motion && _dragging)
        {
            _pivot.RotateY(-motion.Relative.X * 0.01f);
        }

        if (@event is InputEventMouseButton { Pressed: true } wheel && wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            float factor = wheel.ButtonIndex == MouseButton.WheelUp ? 0.88f : 1.14f;
            _zoom = Mathf.Clamp(_zoom * factor, 0.3f, 4f);
            UpdateCamera();
            AcceptEvent();
        }
    }

    static IEnumerable<Node> Walk(Node node)
    {
        if (node == null) yield break;
        yield return node;
        foreach (Node child in node.GetChildren())
        {
            foreach (Node inner in Walk(child)) yield return inner;
        }
    }
}
