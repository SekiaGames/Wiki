using Godot;

public partial class App : Control
{
    public static App Current { get; private set; }

    Sidebar _sidebar;
    Control _pageHost;
    Control _page;
    Label _crumb;
    Label _topStat;
    string _category = "overview";
    string _detail;

    public override void _Ready()
    {
        Current = this;
        Theme = Ui.BuildTheme();

        Assets.Init();
        Lighting.Load();
        Data.Warm();

        BuildShell();
        Navigate("overview");

        CallDeferred(nameof(WarmMedia));

        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--pack")
            {
                PackReport();
                return;
            }

            if (arg.StartsWith("--inspect="))
            {
                Inspect(arg["--inspect=".Length..]);
                return;
            }

            if (!arg.StartsWith("--shot=")) continue;
            string value = arg["--shot=".Length..];
            int at = value.IndexOf('@');
            CaptureShot(at < 0 ? value : value[..at], at < 0 ? null : value[(at + 1)..]);
        }
    }

    void PackReport()
    {
        GD.Print($"[pack] mounted={Assets.PackMounted} path={Assets.PckPath}");
        GD.Print($"[pack] res://assets open = {DirAccess.Open("res://assets") != null}");
        GD.Print($"[pack] root dirs = {string.Join(" | ", DirAccess.Open("res://").GetDirectories())}");

        string[] buckets = DirAccess.Open("res://.godot/exported")?.GetDirectories() ?? System.Array.Empty<string>();
        int total = 0;
        int shaderLike = 0;
        foreach (string bucket in buckets)
        {
            DirAccess dir = DirAccess.Open($"res://.godot/exported/{bucket}");
            if (dir == null) continue;
            foreach (string file in dir.GetFiles())
            {
                total++;
                if (file.Contains("war3_model") || file.Contains("gdshader") || file.Contains("missing_texture")) shaderLike++;
            }
        }
        GD.Print($"[pack] exported buckets={buckets.Length} files={total} shaderLike={shaderLike}");

        foreach (string path in new[]
                 {
                     "res://assets/shaders/war3_model_opaque.gdshader",
                     "res://assets/fallback/missing_texture.png",
                     "res://extract/Tables/UnitsunitUI.json",
                     "res://extract/Units/Human/Footman/Footman.tscn",
                 })
        {
            GD.Print($"[pack] res={ResourceLoader.Exists(path)} file={FileAccess.FileExists(path)} {path}");
        }

        GetTree().Quit();
    }

    void Inspect(string relativeScene)
    {
        GD.Print($"[inspect] scene = {relativeScene}");
        PackedScene packed = Assets.LoadScene(relativeScene);
        if (packed == null)
        {
            GD.Print("[inspect] load failed");
            GetTree().Quit();
            return;
        }

        Node root = packed.Instantiate();
        AddChild(root);

        Aabb box = default;
        bool has = false;
        int index = 0;
        foreach (Node node in Walk(root))
        {
            if (node is not MeshInstance3D { Mesh: not null } mesh) continue;

            Aabb local = mesh.GlobalTransform * mesh.GetAabb();
            GD.Print($"[inspect] mesh{index} {mesh.Name} visible={mesh.Visible} inTree={mesh.IsVisibleInTree()} surfaces={mesh.Mesh.GetSurfaceCount()} aabb={local}");

            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                Material material = mesh.GetSurfaceOverrideMaterial(i) ?? mesh.Mesh.SurfaceGetMaterial(i);
                if (material is ShaderMaterial shader)
                {
                    Texture2D baseTex = shader.GetShaderParameter("base_texture").As<Texture2D>();
                    Texture2D detailTex = shader.GetShaderParameter("detail_texture").As<Texture2D>();
                    GD.Print($"[inspect]    mat{i} base={shader.GetShaderParameter("use_base_texture")} "
                             + $"baseTex={baseTex?.ResourcePath} "
                             + $"detail={shader.GetShaderParameter("use_detail")} detailTex={detailTex?.ResourcePath} "
                             + $"detailColor={shader.GetShaderParameter("detail_color")} "
                             + $"baseColor={shader.GetShaderParameter("base_color")} "
                             + $"team={shader.GetShaderParameter("use_team_color")} scale={shader.GetShaderParameter("color_scale")} alpha={shader.GetShaderParameter("layer_alpha")}");
                }
                else
                {
                    GD.Print($"[inspect]    mat{i} {material?.GetType().Name ?? "null"}");
                }
            }

            box = has ? box.Merge(local) : local;
            has = true;
            index++;
        }

        GD.Print($"[inspect] bounds = {box} size={box.Size}");

        foreach (Node node in Walk(root))
        {
            if (node is not AnimationPlayer player) continue;
            GD.Print($"[inspect] player {player.Name} libs={string.Join(",", player.GetAnimationLibraryList())}");
            foreach (StringName name in player.GetAnimationLibraryList())
            {
                AnimationLibrary library = player.GetAnimationLibrary(name);
                if (library != null) GD.Print($"[inspect]   {name}: {library.GetAnimationList().Count} anims = {string.Join(",", library.GetAnimationList())}");
            }
        }

        GetTree().Quit();
    }

    static System.Collections.Generic.IEnumerable<Node> Walk(Node node)
    {
        yield return node;
        foreach (Node child in node.GetChildren())
        {
            foreach (Node inner in Walk(child)) yield return inner;
        }
    }

    async void CaptureShot(string file, string nav)
    {
        string clip = null;
        float seek = -1f;
        if (!string.IsNullOrEmpty(nav))
        {
            int pipe = nav.IndexOf('|');
            if (pipe > 0)
            {
                clip = nav[(pipe + 1)..];
                nav = nav[..pipe];
                int at = clip.IndexOf('@');
                if (at > 0)
                {
                    float.TryParse(clip[(at + 1)..], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out seek);
                    clip = clip[..at];
                }
            }
            int colon = nav.IndexOf(':');
            if (colon > 0) Navigate(nav[..colon], nav[(colon + 1)..]);
            else Navigate(nav);
        }

        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
        if (!string.IsNullOrEmpty(clip) && _page is DetailPage detail)
        {
            detail.ApplyClip(clip, seek);
            if (OS.HasEnvironment("WIKI_DEBUG_PARTICLES")) detail.DumpParticles();
            await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);
        }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

        Image image = GetViewport().GetTexture().GetImage();
        image.SavePng(file);
        GD.Print($"[shot] {file}");
        GetTree().Quit();
    }

    void BuildShell()
    {
        ColorRect background = new() { Color = Ui.Bg, MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        HBoxContainer shell = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        shell.AddThemeConstantOverride("separation", 1);
        shell.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shell);

        _sidebar = new Sidebar(category => Navigate(category));
        shell.AddChild(_sidebar);

        VBoxContainer main = Ui.VBox(0);
        main.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        main.SizeFlagsVertical = SizeFlags.ExpandFill;
        shell.AddChild(main);

        main.AddChild(BuildTopbar());

        _pageHost = Ui.Margin(26, 18, 26, 18);
        _pageHost.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _pageHost.SizeFlagsVertical = SizeFlags.ExpandFill;
        main.AddChild(_pageHost);
    }

    Control BuildTopbar()
    {
        PanelContainer bar = new() { CustomMinimumSize = new Vector2(0, 54) };
        StyleBoxFlat style = Ui.Flat(Ui.Bg, Ui.LineSoft, 0, 0);
        style.BorderWidthBottom = 1;
        style.BorderColor = Ui.LineSoft;
        bar.AddThemeStyleboxOverride("panel", style);

        MarginContainer pad = Ui.Margin(26, 0, 26, 0);
        HBoxContainer row = Ui.HBox(8);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        pad.AddChild(row);
        bar.AddChild(pad);

        row.AddChild(Ui.Label("THE ARCHIVE", 10, Ui.Faint));
        row.AddChild(Ui.Label("/", 10, Ui.Faint));
        _crumb = Ui.Label("概览", 11, Ui.Dim);
        row.AddChild(_crumb);

        row.AddChild(Ui.Spacer());

        _topStat = Ui.Label("", 10, Ui.Faint);
        row.AddChild(_topStat);
        row.AddChild(Ui.Label(Assets.PackMounted ? "● 已连接 extract.pck" : "● 未挂载 extract.pck", 10,
            Assets.PackMounted ? Ui.Teal : Ui.Danger));

        return bar;
    }

    void WarmMedia()
    {
        Data.WarmMedia();
        _sidebar.RefreshCounts();
        UpdateTopStat();

        if (_detail == null)
        {
            Navigate(_category);
        }
    }

    void UpdateTopStat()
    {
        _topStat.Text = $"{Data.Units?.Count ?? 0} 单位 · {Data.Abilities?.Count ?? 0} 技能 · "
                        + $"{Data.Models?.Count ?? 0} 模型 · {Data.Textures?.Count ?? 0} 贴图 · {Data.Sounds?.Count ?? 0} 音效";
    }

    public void Navigate(string category, string id = null)
    {
        _category = category;
        _detail = string.IsNullOrEmpty(id) ? null : id;

        CategoryDef meta = Catalog.Get(category);
        _crumb.Text = _detail == null ? $"{meta.Group} · {meta.Label}" : $"{meta.Label} · {_detail}";

        _sidebar.SetActive(category);
        Ui.Clear(_pageHost);

        Control page = category switch
        {
            "overview" => new OverviewPage(),
            "units" or "abilities" or "doodads" or "destructables" or "items" or "upgrades"
                or "models" or "sounds" or "textures" or "tables" or "texts" =>
                _detail == null ? (Control)new ListPage(category) : new DetailPage(category, _detail),
            _ => new PendingPage(category),
        };

        _pageHost.AddChild(page);
        _page = page;
        UpdateTopStat();
    }
}
