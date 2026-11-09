using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class DetailPage : Page
{
    static readonly (string Label, Color Color)[] TeamColors =
    {
        ("中立", Color.FromHtml("9b98a5")),
        ("红", Color.FromHtml("c8322b")),
        ("蓝", Color.FromHtml("2554c7")),
        ("青", Color.FromHtml("1be7ba")),
        ("紫", Color.FromHtml("551a8b")),
        ("黄", Color.FromHtml("fff400")),
        ("橙", Color.FromHtml("fe8a0e")),
        ("绿", Color.FromHtml("1cb500")),
        ("粉", Color.FromHtml("edb9b9")),
    };

    static readonly Dictionary<string, string> SoundKindLabel = new()
    {
        ["What"] = "回应", ["Yes"] = "确认", ["YesAttack"] = "攻击应答", ["Attack"] = "攻击",
        ["Death"] = "死亡", ["Birth"] = "出生", ["Pissed"] = "烦躁", ["Warcry"] = "战吼",
        ["Ready"] = "就绪", ["Other"] = "其他",
    };

    readonly string _category;
    readonly string _id;
    readonly VBoxContainer _root;
    readonly VBoxContainer _tabs = Ui.VBox(8);
    ModelPreview _preview;
    OptionButton _animationSelect;
    AudioStreamPlayer _audio;

    public DetailPage(string category, string id)
    {
        _category = category;
        _id = id;

        ScrollContainer scroll = Ui.Scroll();
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(scroll);

        _root = Ui.VBox(14);
        _root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_root);

        switch (category)
        {
            case "units": BuildUnit(); break;
            case "doodads": BuildDoodad(); break;
            case "models": BuildModel(); break;
            case "abilities": BuildAbility(); break;
            case "sounds": BuildSound(); break;
            case "textures": BuildTexture(); break;
            default: _root.AddChild(PageParts.Empty("没有内容", "该分类暂不支持详情页。")); break;
        }
    }

    Control BackRow(string label)
    {
        HBoxContainer row = Ui.HBox(10);
        Button back = Ui.TextButton($"← {label}", 11);
        back.Pressed += () => App.Current.Navigate(_category);
        row.AddChild(back);
        row.AddChild(Ui.Label($"/ {_id}", 11, Ui.Faint));
        return row;
    }

    Control TitleBlock(string title, string subtitle, string tip, Texture2D icon)
    {
        HBoxContainer row = Ui.HBox(16);

        if (icon != null)
        {
            PanelContainer iconPanel = Ui.Panel(Ui.Panel2, Ui.Line, 4);
            iconPanel.CustomMinimumSize = new Vector2(84, 84);
            CenterContainer center = new();
            center.AddChild(Ui.Image(icon, 72));
            iconPanel.AddChild(center);
            row.AddChild(iconPanel);
        }

        VBoxContainer text = Ui.VBox(4);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        text.AddChild(Ui.SerifLabel(title, 22, Ui.Text));
        text.AddChild(Ui.Label(subtitle, 11, Ui.Faint));
        if (!string.IsNullOrEmpty(tip))
        {
            Label tipLabel = Ui.Label(tip, 12, Ui.Dim, true);
            tipLabel.MaxLinesVisible = 3;
            tipLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            text.AddChild(tipLabel);
        }
        row.AddChild(text);
        return row;
    }

    Control _stage;

    Control BuildStage(string scenePath, out ModelPreview preview)
    {
        Control wrapper = new()
        {
            CustomMinimumSize = new Vector2(0, StageHeight()),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Pass,
        };
        _stage = wrapper;

        PanelContainer frame = Ui.Panel(Ui.Panel2, Ui.Line, 4);
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        frame.MouseFilter = MouseFilterEnum.Pass;
        wrapper.AddChild(frame);

        preview = new ModelPreview();
        frame.AddChild(preview);

        Label hint = Ui.Label("拖动旋转 · 滚轮缩放", 10, Ui.Faint);
        hint.SetAnchorsPreset(LayoutPreset.BottomLeft);
        hint.OffsetLeft = 12;
        hint.OffsetTop = -26;
        hint.OffsetBottom = -10;
        hint.MouseFilter = MouseFilterEnum.Ignore;
        wrapper.AddChild(hint);

        Label path = Ui.Label(scenePath ?? "", 10, Ui.Faint);
        path.SetAnchorsPreset(LayoutPreset.TopLeft);
        path.OffsetLeft = 12;
        path.OffsetTop = 10;
        path.MouseFilter = MouseFilterEnum.Ignore;
        path.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        wrapper.AddChild(path);

        preview.ShowScene(scenePath);
        if (preview.Error != null)
        {
            Label error = Ui.Label(preview.Error, 12, Ui.Danger);
            error.SetAnchorsPreset(LayoutPreset.Center);
            error.GrowHorizontal = GrowDirection.Both;
            error.GrowVertical = GrowDirection.Both;
            error.MouseFilter = MouseFilterEnum.Ignore;
            wrapper.AddChild(error);
        }

        return wrapper;
    }

    Control BuildToolbar(ModelPreview preview, Action onVariant = null, OptionButton variantSelect = null)
    {
        HBoxContainer bar = Ui.HBox(8);

        _animationSelect = new OptionButton { CustomMinimumSize = new Vector2(180, 0) };
        StyleOption(_animationSelect);
        FillAnimations(preview);
        preview.Loaded += () => FillAnimations(preview);
        _animationSelect.ItemSelected += index =>
        {
            if (index >= 0 && index < preview.Animations.Count) preview.Play(preview.Animations[(int)index]);
        };
        bar.AddChild(_animationSelect);

        Button play = Ui.ToolButton("▶ 播放", 11, true);
        play.Pressed += () => preview.Play(CurrentAnimation(preview));
        bar.AddChild(play);

        Button pause = Ui.ToolButton("⏸ 暂停", 11);
        pause.Pressed += () =>
        {
            preview.TogglePause();
            pause.Text = preview.IsPaused ? "▶ 继续" : "⏸ 暂停";
        };
        bar.AddChild(pause);

        Button replay = Ui.ToolButton("重播", 11);
        replay.Pressed += () => preview.Frame();
        bar.AddChild(replay);

        Button particles = Ui.ToolButton(preview.ParticlesEnabled ? "粒子 开" : "粒子 关", 11);
        particles.Pressed += () =>
        {
            preview.SetParticles(!preview.ParticlesEnabled);
            particles.Text = preview.ParticlesEnabled ? "粒子 开" : "粒子 关";
        };
        bar.AddChild(particles);

        OptionButton team = new() { CustomMinimumSize = new Vector2(88, 0) };
        StyleOption(team);
        for (int i = 0; i < TeamColors.Length; i++) team.AddItem(TeamColors[i].Label, i);
        team.Selected = 0;
        team.ItemSelected += index =>
        {
            (string _, Color color) = TeamColors[(int)index];
            preview.SetTeamColor(color, index != 0);
        };
        bar.AddChild(team);

        if (variantSelect != null)
        {
            bar.AddChild(variantSelect);
        }

        bar.AddChild(Ui.Spacer());
        _animationCount = Ui.Label($"{preview.Animations.Count} 条动画", 10, Ui.Faint);
        bar.AddChild(_animationCount);

        VBoxContainer stack = Ui.VBox(8);
        stack.AddChild(bar);
        stack.AddChild(BuildScrubber(preview));
        HBoxContainer forms = Ui.HBox(6);
        forms.Visible = false;
        stack.AddChild(forms);
        preview.Loaded += () => FillFormButtons(forms, preview);
        FillFormButtons(forms, preview);
        return stack;
    }

    float StageHeight()
    {
        if (!IsInsideTree()) return 640f;

        Vector2 size = GetViewportRect().Size;
        float viewportHeight = size.Y > 1f ? size.Y : 1000f;
        return Mathf.Clamp(viewportHeight - 360f, 360f, 1400f);
    }

    void UpdateStageHeight()
    {
        if (_stage == null) return;

        float height = StageHeight();
        if (!Mathf.IsEqualApprox(_stage.CustomMinimumSize.Y, height))
        {
            _stage.CustomMinimumSize = new Vector2(0, height);
        }
    }

    HSlider _scrubber;
    Label _scrubLabel;
    bool _scrubbing;

    Control BuildScrubber(ModelPreview preview)
    {
        HBoxContainer row = Ui.HBox(8);
        HBoxContainer box = Ui.HBox(0);
        box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(box);

        _scrubber = new HSlider
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.001,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(200, 0),
        };
        _scrubber.DragStarted += () => _scrubbing = true;
        _scrubber.DragEnded += changed =>
        {
            _scrubbing = false;
            preview.Seek((float)_scrubber.Value);
        };
        _scrubber.ValueChanged += value =>
        {
            if (_scrubbing) preview.Seek((float)value);
        };
        box.AddChild(_scrubber);

        _scrubLabel = Ui.Label("0.0 / 0.0s", 10, Ui.Faint);
        _scrubLabel.CustomMinimumSize = new Vector2(92, 0);
        _scrubLabel.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(_scrubLabel);
        return row;
    }

    public override void _Process(double delta)
    {
        UpdateStageHeight();
        if (_preview == null || _scrubber == null) return;

        float length = _preview.AnimationLength;
        if (length > 0.01f && !Mathf.IsEqualApprox((float)_scrubber.MaxValue, length))
        {
            _scrubber.MaxValue = length;
        }
        if (_scrubbing) return;

        float position = Mathf.Clamp(_preview.AnimationPosition, 0f, (float)_scrubber.MaxValue);
        _scrubber.SetValueNoSignal(position);
        if (_scrubLabel != null)
        {
            _scrubLabel.Text = $"{position:0.0} / {length:0.0}s";
        }
    }

    static readonly string[] LevelNumbers = { "", "一级", "二级", "三级", "四级", "五级" };

    static int UpgradeLevel(string clip)
    {
        if (!clip.StartsWith("Stand", StringComparison.OrdinalIgnoreCase)) return -1;
        if (clip.StartsWith("Stand Work", StringComparison.OrdinalIgnoreCase)) return -1;
        if (clip.StartsWith("Stand Portrait", StringComparison.OrdinalIgnoreCase)) return -1;
        if (clip.Length == "Stand".Length) return 1;
        if (!clip.StartsWith("Stand Upgrade", StringComparison.OrdinalIgnoreCase)) return -1;
        string tail = clip["Stand Upgrade".Length..].Trim().ToLowerInvariant();
        return tail switch
        {
            "first" => 2,
            "second" => 3,
            "third" => 4,
            _ => -1,
        };
    }

    static string LevelName(int level)
    {
        return level > 0 && level < LevelNumbers.Length ? LevelNumbers[level] : $"{level}级";
    }

    public void ApplyClip(string clip, float seekSeconds = -1f)
    {
        if (_preview == null || string.IsNullOrEmpty(clip)) return;
        if (_preview.Animations.Count == 0)
        {
            _preview.Loaded += () => ApplyClip(clip, seekSeconds);
            return;
        }

        if (seekSeconds >= 0f)
        {
            _preview.SetParticles(true);
            _preview.Play(clip);
            _preview.Seek(seekSeconds);
            _preview.Pause();
        }
        else if (UpgradeLevel(clip) > 0)
        {
            _preview.PlayStatic(clip);
            _preview.SetParticles(false);
        }
        else
        {
            _preview.SetParticles(true);
            _preview.Play(clip);
        }
        _preview.Frame();
        int index = _preview.Animations.IndexOf(clip);
        if (index >= 0 && _animationSelect != null) _animationSelect.Selected = index;
    }

    void FillFormButtons(HBoxContainer host, ModelPreview preview)
    {
        Ui.Clear(host);
        host.Visible = false;
        if (preview.Animations.Count == 0) return;

        List<(int Level, string Clip)> forms = new();
        List<(int Level, string Clip)> upgrades = new();
        foreach (string clip in preview.Animations)
        {
            int level = UpgradeLevel(clip);
            if (level > 0) forms.Add((level, clip));
            else if (clip.StartsWith("Birth Upgrade", StringComparison.OrdinalIgnoreCase)) upgrades.Add((UpgradeLevel("Stand" + clip["Birth".Length..]), clip));
        }

        if (forms.Count == 0) return;
        forms.Sort((a, b) => a.Level.CompareTo(b.Level));

        host.AddChild(Ui.Label("形态", 10, Ui.Faint));
        foreach ((int level, string clip) in forms)
        {
            string clipName = clip;
            Button button = Ui.ToolButton(LevelName(level), 11);
            button.Pressed += () =>
            {
                preview.PlayStatic(clipName);
                preview.SetParticles(false);
                preview.Frame();
                int index = preview.Animations.IndexOf(clipName);
                if (index >= 0) _animationSelect.Selected = index;
            };
            host.AddChild(button);
        }

        if (upgrades.Count > 0)
        {
            upgrades.Sort((a, b) => a.Level.CompareTo(b.Level));
            host.AddChild(Ui.Label("升级动画", 10, Ui.Faint));
            foreach ((int level, string clip) in upgrades)
            {
                string clipName = clip;
                Button button = Ui.ToolButton($"升到{LevelName(level)}", 11);
                button.Pressed += () =>
                {
                    preview.SetParticles(true);
                    preview.Play(clipName);
                    int index = preview.Animations.IndexOf(clipName);
                    if (index >= 0) _animationSelect.Selected = index;
                };
                host.AddChild(button);
            }
        }

        host.Visible = true;
    }

    Label _animationCount;

    void FillAnimations(ModelPreview preview)
    {
        _animationSelect.Clear();
        for (int i = 0; i < preview.Animations.Count; i++) _animationSelect.AddItem(preview.Animations[i], i);

        int stand = preview.Animations.FindIndex(name => name.StartsWith("Stand", StringComparison.OrdinalIgnoreCase));
        if (stand < 0) stand = preview.Animations.FindIndex(name => name.Contains("Stand", StringComparison.OrdinalIgnoreCase));
        _animationSelect.Selected = Mathf.Max(0, stand);

        if (_animationCount != null) _animationCount.Text = $"{preview.Animations.Count} 条动画";
        if (preview.Animations.Count > 0) preview.Play(CurrentAnimation(preview));
    }

    string CurrentAnimation(ModelPreview preview)
    {
        int index = _animationSelect.Selected;
        return index >= 0 && index < preview.Animations.Count ? preview.Animations[index] : null;
    }

    static void StyleOption(OptionButton option)
    {
        option.AddThemeFontSizeOverride("font_size", 11);
        option.AddThemeColorOverride("font_color", Ui.Dim);
        option.AddThemeStyleboxOverride("normal", Ui.Flat(Ui.PanelBg, Ui.Line, 4, 1, 8, 3));
        option.AddThemeStyleboxOverride("hover", Ui.Flat(Ui.Hover, Ui.LineStrong, 4, 1, 8, 3));
        option.AddThemeStyleboxOverride("pressed", Ui.Flat(Ui.Panel2, Ui.Gold, 4, 1, 8, 3));
        option.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    Control BuildTabs(params (string Label, Func<Control> Build)[] tabs)
    {
        HBoxContainer row = Ui.HBox(4);
        HBoxContainer body = Ui.HBox(0);
        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        PanelContainer content = Ui.Panel(Ui.PanelBg, Ui.Line);
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MarginContainer pad = Ui.Margin(16, 14, 16, 16);
        VBoxContainer host = Ui.VBox(2);
        host.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        pad.AddChild(host);
        content.AddChild(pad);

        List<Button> buttons = new();
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            Button button = Ui.TextButton(tabs[i].Label, 12);
            buttons.Add(button);
            row.AddChild(button);
            button.Pressed += () =>
            {
                for (int k = 0; k < buttons.Count; k++)
                {
                    bool active = k == index;
                    buttons[k].AddThemeColorOverride("font_color", active ? Ui.Gold : Ui.Faint);
                }
                Ui.Clear(host);
                host.AddChild(tabs[index].Build());
            };
        }

        buttons[0].AddThemeColorOverride("font_color", Ui.Gold);

        VBoxContainer box = Ui.VBox(8);
        box.AddChild(row);
        box.AddChild(content);
        host.AddChild(tabs[0].Build());
        return box;
    }

    // ---------- 单位 ----------

    void BuildUnit()
    {
        UnitRow unit = Data.UnitById != null && Data.UnitById.TryGetValue(_id, out UnitRow found) ? found : null;
        _root.AddChild(BackRow("单位"));

        if (unit == null)
        {
            _root.AddChild(PageParts.Empty("没有这个单位", $"id = {_id}"));
            return;
        }

        _root.AddChild(TitleBlock(unit.Name,
            $"{unit.Id} · {unit.En} · {Data.RaceLabel(unit.Race)} · {Data.ClassLabel(unit.Klass)}",
            unit.Tip, Data.IconFor(unit)));

        string glb = Data.FindModel(unit.ModelBase);
        string scene = Data.SceneFor(glb);
        Control stage = BuildStage(scene, out ModelPreview preview);
        _preview = preview;

        _root.AddChild(stage);
        _root.AddChild(BuildToolbar(preview));

        List<string> sounds = string.IsNullOrEmpty(glb) ? new List<string>() : Data.DirFiles(glb[..glb.LastIndexOf('/')], ".wav");

        _root.AddChild(BuildTabs(
            ("技能", () => UnitSkills(unit)),
            ("音效", () => UnitSounds(unit, sounds)),
            ("属性", () => UnitAttributes(unit)),
            ("原始数据", () => RawBlock(new Dictionary<string, Dictionary<string, string>>
            {
                ["UnitUI"] = unit.UnitUI,
                ["UnitData"] = unit.UnitData,
                ["UnitBalance"] = unit.UnitBalance,
                ["UnitAbilities"] = unit.UnitAbilities,
            }))));
    }

    Control UnitSkills(UnitRow unit)
    {
        if (unit.HeroAbilities.Count == 0)
        {
            return PageParts.Empty("没有英雄技能", "普通单位与建筑没有登记 heroAbilList。");
        }

        GridContainer grid = new() { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);

        foreach (string skillId in unit.HeroAbilities)
        {
            if (Data.AbilityById == null || !Data.AbilityById.TryGetValue(skillId, out AbilityRow ability)) continue;

            PanelContainer card = Ui.Panel(Ui.Panel2, Ui.Line, 4);
            HBoxContainer row = Ui.HBox(10);
            MarginContainer pad = Ui.Margin(10, 8, 10, 8);
            pad.AddChild(row);
            card.AddChild(pad);

            Texture2D icon = Data.IconFor(ability);
            row.AddChild(icon != null ? Ui.Image(icon, 44) : Ui.Label("✦", 20, Ui.Faint));

            VBoxContainer text = Ui.VBox(2);
            text.AddChild(Ui.Label(ability.Name, 12, Ui.Text));
            text.AddChild(Ui.Label($"{ability.Id} · 按钮位 {ability.Pos}", 10, Ui.Faint));
            row.AddChild(text);

            card.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                {
                    _preview?.Play(ability.Anim);
                }
            };
            grid.AddChild(card);
        }

        if (grid.GetChildCount() == 0) return PageParts.Empty("没有可用技能", "heroAbilList 里的 id 在技能表里没有命中。");
        return grid;
    }

    Control UnitSounds(UnitRow unit, List<string> files)
    {
        if (files.Count == 0) return PageParts.Empty("没有音效", "模型目录里没有 .wav 文件。");

        VBoxContainer list = Ui.VBox(2);
        foreach (string file in files)
        {
            string kind = SoundKindLabel.TryGetValue(Data.SoundKind(file), out string label) ? label : "其他";
            HBoxContainer row = Ui.HBox(10);
            Label kindLabel = Ui.Label(kind, 10, Ui.Faint);
            kindLabel.CustomMinimumSize = new Vector2(56, 0);
            row.AddChild(kindLabel);
            row.AddChild(Ui.Label(file, 12, Ui.Dim));

            Button play = Ui.ToolButton("▶ 试听", 10);
            string path = $"{unit.ModelWc3[..unit.ModelWc3.LastIndexOf('/')]}/{file}";
            play.Pressed += () => PlaySound(path);
            row.AddChild(play);

            MarginContainer pad = Ui.Margin(0, 3, 0, 3);
            pad.AddChild(row);
            list.AddChild(pad);
        }
        return list;
    }

    Control UnitAttributes(UnitRow unit)
    {
        List<(string, string)> rows = new()
        {
            ("生命", unit.Hp.ToString()),
            ("魔法", unit.Mana.ToString()),
            ("护甲", unit.Armor.ToString()),
            ("移速", unit.Speed.ToString()),
            ("等级", unit.Level.ToString()),
            ("金", unit.Gold.ToString()),
            ("木", unit.Lumber.ToString()),
            ("种族", Data.RaceLabel(unit.Race)),
            ("类型", Data.ClassLabel(unit.Klass)),
            ("模型", unit.ModelWc3),
            ("音效组", unit.UnitUI.GetValueOrDefault("unitSound", "")),
            ("按钮位", unit.UnitUI.GetValueOrDefault("unitClass", "")),
        };

        GridContainer grid = new() { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 6);

        foreach ((string label, string value) in rows)
        {
            HBoxContainer row = Ui.HBox(10);
            Label key = Ui.Label(label, 11, Ui.Faint);
            key.CustomMinimumSize = new Vector2(64, 0);
            row.AddChild(key);
            row.AddChild(Ui.Label(value, 11, Ui.Dim));
            grid.AddChild(row);
        }
        return grid;
    }

    // ---------- 装饰物 ----------

    void BuildDoodad()
    {
        DoodadRow doodad = Data.DoodadById != null && Data.DoodadById.TryGetValue(_id, out DoodadRow found) ? found : null;
        _root.AddChild(BackRow("装饰物"));

        if (doodad == null)
        {
            _root.AddChild(PageParts.Empty("没有这个装饰物", $"id = {_id}"));
            return;
        }

        _root.AddChild(TitleBlock(doodad.Name, $"{doodad.Id} · {doodad.En} · {doodad.File}", "", null));

        List<(int Index, string Glb)> variants = new();
        for (int i = 0; i < doodad.Variants; i++)
        {
            string suffix = doodad.Variants > 1 ? i.ToString() : "";
            string glb = Data.FindModel(doodad.Base + suffix) ?? Data.FindModel(doodad.Base);
            if (glb != null) variants.Add((i, glb));
        }

        OptionButton variantSelect = null;
        if (variants.Count > 1)
        {
            variantSelect = new OptionButton { CustomMinimumSize = new Vector2(96, 0) };
            StyleOption(variantSelect);
            for (int i = 0; i < variants.Count; i++) variantSelect.AddItem($"变体 {variants[i].Index + 1}", i);
            _root.AddChild(variantSelect);
        }

        string scene = variants.Count > 0 ? Data.SceneFor(variants[0].Glb) : null;
        _root.AddChild(BuildStage(scene, out ModelPreview preview));
        _preview = preview;

        if (variantSelect != null)
        {
            variantSelect.ItemSelected += index =>
            {
                if (index < 0 || index >= variants.Count) return;
                preview.ShowScene(Data.SceneFor(variants[(int)index].Glb));
            };
        }

        _root.AddChild(BuildToolbar(preview));

        _root.AddChild(BuildTabs(
            ("属性", () => SimpleGrid(new List<(string, string)>
            {
                ("类别", doodad.Category),
                ("模型", doodad.File),
                ("变体", doodad.Variants.ToString()),
                ("缩放", doodad.Scale.ToString("0.###")),
            })),
            ("原始数据", () => RawBlock(new Dictionary<string, Dictionary<string, string>>()))));
    }

    // ---------- 模型 ----------

    void BuildModel()
    {
        string dir = _id.Contains('/') ? _id[.._id.LastIndexOf('/')] : "";
        string baseName = _id[( _id.LastIndexOf('/') + 1)..];
        baseName = baseName[..baseName.LastIndexOf('.')];

        _root.AddChild(BackRow("模型库"));
        _root.AddChild(TitleBlock(baseName, _id, "", null));

        _root.AddChild(BuildStage(Data.SceneFor(_id), out ModelPreview preview));
        _preview = preview;
        _root.AddChild(BuildToolbar(preview));

        List<string> siblings = Data.DirFiles(dir, "");
        _root.AddChild(BuildTabs(
            ("文件", () => SimpleGrid(new List<(string, string)>
            {
                ("目录", dir),
                ("文件数", siblings.Count.ToString()),
                ("动画", preview.Animations.Count.ToString()),
            })),
            ("同目录", () =>
            {
                VBoxContainer list = Ui.VBox(2);
                foreach (string name in siblings) list.AddChild(Ui.Label(name, 11, Ui.Dim));
                return list;
            })));
    }

    // ---------- 技能 ----------

    void BuildAbility()
    {
        AbilityRow ability = Data.AbilityById != null && Data.AbilityById.TryGetValue(_id, out AbilityRow found) ? found : null;
        _root.AddChild(BackRow("技能"));

        if (ability == null)
        {
            _root.AddChild(PageParts.Empty("没有这个技能", $"id = {_id}"));
            return;
        }

        _root.AddChild(TitleBlock(ability.Name, $"{ability.Id} · {ability.En}", ability.Tip, Data.IconFor(ability)));

        _root.AddChild(BuildTabs(
            ("属性", () => SimpleGrid(new List<(string, string)>
            {
                ("等级", ability.Levels.ToString()),
                ("冷却", ability.Cool.ToString()),
                ("耗魔", ability.Cost.ToString()),
                ("按钮位", ability.Pos),
                ("施法动画", ability.Anim),
                ("图标", ability.Icon),
                ("种族", ability.Race),
            })),
            ("原始数据", () => RawBlock(new Dictionary<string, Dictionary<string, string>> { ["AbilityData"] = ability.Raw }))));
    }

    // ---------- 音效 / 贴图 ----------

    void BuildSound()
    {
        _root.AddChild(BackRow("音效"));
        _root.AddChild(TitleBlock(_id[( _id.LastIndexOf('/') + 1)..], _id, "", null));

        PanelContainer panel = Ui.Panel(Ui.Panel2, Ui.Line);
        panel.CustomMinimumSize = new Vector2(0, 120);
        CenterContainer center = new();
        Button play = Ui.ToolButton("▶  播放", 13, true);
        play.CustomMinimumSize = new Vector2(140, 40);
        play.Pressed += () => PlaySound(_id);
        center.AddChild(play);
        panel.AddChild(center);
        _root.AddChild(panel);

        _root.AddChild(BuildTabs(("信息", () => SimpleGrid(new List<(string, string)>
        {
            ("路径", _id),
            ("目录", _id.Contains('/') ? _id[.._id.LastIndexOf('/')] : ""),
        }))));
    }

    void BuildTexture()
    {
        _root.AddChild(BackRow("贴图"));
        _root.AddChild(TitleBlock(_id[( _id.LastIndexOf('/') + 1)..], _id, "", null));

        Texture2D texture = Assets.LoadTexture(_id);
        string size = texture == null ? "无法读取" : $"{texture.GetWidth()} × {texture.GetHeight()}";

        PanelContainer panel = Ui.Panel(Ui.Panel2, Ui.Line);
        panel.CustomMinimumSize = new Vector2(0, 420);
        CenterContainer center = new();
        if (texture != null) center.AddChild(Ui.Image(texture, 360));
        else center.AddChild(Ui.Label("无法读取该贴图", 12, Ui.Danger));
        panel.AddChild(center);
        _root.AddChild(panel);

        _root.AddChild(BuildTabs(("信息", () => SimpleGrid(new List<(string, string)>
        {
            ("尺寸", size),
            ("路径", _id),
        }))));
    }

    // ---------- 共用 ----------

    static Control SimpleGrid(List<(string Label, string Value)> rows)
    {
        GridContainer grid = new() { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 6);

        foreach ((string label, string value) in rows)
        {
            HBoxContainer row = Ui.HBox(10);
            Label key = Ui.Label(label, 11, Ui.Faint);
            key.CustomMinimumSize = new Vector2(64, 0);
            row.AddChild(key);
            Label text = Ui.Label(string.IsNullOrEmpty(value) ? "—" : value, 11, Ui.Dim);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            text.CustomMinimumSize = new Vector2(320, 0);
            row.AddChild(text);
            grid.AddChild(row);
        }
        return grid;
    }

    static Control RawBlock(Dictionary<string, Dictionary<string, string>> blocks)
    {
        VBoxContainer list = Ui.VBox(10);
        if (blocks.Count == 0)
        {
            list.AddChild(Ui.Label("原始表行暂不展示。", 11, Ui.Faint));
            return list;
        }

        foreach (KeyValuePair<string, Dictionary<string, string>> block in blocks)
        {
            list.AddChild(Ui.Label(block.Key, 10, Ui.Faint));
            foreach (KeyValuePair<string, string> pair in block.Value)
            {
                HBoxContainer row = Ui.HBox(10);
                Label key = Ui.Label(pair.Key, 10, Ui.Faint);
                key.CustomMinimumSize = new Vector2(148, 0);
                row.AddChild(key);
                Label value = Ui.Label(pair.Value, 10, Ui.Dim);
                value.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                value.CustomMinimumSize = new Vector2(420, 0);
                row.AddChild(value);
                list.AddChild(row);
            }
        }
        return list;
    }

    void PlaySound(string path)
    {
        AudioStream stream = Assets.LoadAudio(path);
        if (stream == null) return;

        _audio ??= new AudioStreamPlayer();
        if (_audio.GetParent() == null) AddChild(_audio);

        _audio.Stream = stream;
        _audio.Play();
    }
}
