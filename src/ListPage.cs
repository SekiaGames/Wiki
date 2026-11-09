using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ListItem
{
    public string Id, Title, Meta;
    public Texture2D Icon;
    public string IconPath;
    public List<(string Label, string Value)> Stats = new();
    public string SoundPath;
    public string Glyph;
}

public partial class ListPage : Page
{
    const int PageSize = 120;

    readonly string _category;
    readonly GridContainer _grid = new();
    readonly Label _countLabel = Ui.Label("—", 11, Ui.Faint);
    readonly HBoxContainer _chips = Ui.HBox(6);
    readonly List<ListItem> _all = new();
    readonly VBoxContainer _footer = Ui.VBox(0);

    string _query = "";
    string _race = "all";
    string _klass = "all";
    string _doodadCategory = "all";
    string _family = "all";
    int _limit = PageSize;

    AudioStreamPlayer _audio;

    public ListPage(string category)
    {
        _category = category;
        CategoryDef meta = Catalog.Get(category);

        ScrollContainer scroll = Ui.Scroll();
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(scroll);

        VBoxContainer root = Ui.VBox(14);
        root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(root);

        root.AddChild(PageParts.PageHeader(meta));
        root.AddChild(BuildFilterBar());

        _grid.AddThemeConstantOverride("h_separation", 16);
        _grid.AddThemeConstantOverride("v_separation", 16);
        _grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _grid.Columns = 4;
        root.AddChild(_grid);
        root.AddChild(_footer);

        scroll.Resized += () => UpdateColumns(scroll.Size.X);
        UpdateColumns(scroll.Size.X);

        BuildData();
        Refresh();
    }

    void UpdateColumns(float width)
    {
        int columns = Mathf.Clamp((int)((width + 16) / 244), 1, 8);
        if (columns != _grid.Columns) _grid.Columns = columns;
    }

    Control BuildFilterBar()
    {
        VBoxContainer bar = Ui.VBox(8);

        HBoxContainer row = Ui.HBox(6);
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(_chips);
        _chips.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        if (_category == "units")
        {
            AddChip("全部", "all", "race");
            AddChip("人族", "human", "race");
            AddChip("兽族", "orc", "race");
            AddChip("不死", "undead", "race");
            AddChip("暗夜", "nightelf", "race");
            AddChip("中立", "neutral", "race");
        }

        if (_category == "doodads")
        {
            AddChip("全部", "all", "doodad");
            AddChip("环境", "E", "doodad");
            AddChip("物件", "O", "doodad");
            AddChip("结构", "S", "doodad");
            AddChip("水面", "W", "doodad");
            AddChip("悬崖", "C", "doodad");
            AddChip("特殊", "Z", "doodad");
        }

        if (_category is "models" or "sounds" or "textures")
        {
            AddChip("全部", "all", "family");
        }

        if (_category == "units")
        {
            OptionButton klass = new() { CustomMinimumSize = new Vector2(96, 0) };
            StyleOption(klass);
            klass.AddItem("全部类型", 0);
            klass.AddItem("英雄", 1);
            klass.AddItem("单位", 2);
            klass.AddItem("建筑", 3);
            klass.ItemSelected += index =>
            {
                _klass = index switch { 1 => "hero", 2 => "unit", 3 => "building", _ => "all" };
                _limit = PageSize;
                Refresh();
            };
            row.AddChild(klass);
        }

        LineEdit search = new()
        {
            PlaceholderText = _category == "units" ? "搜索名称 / id / 英文名" : "搜索名称",
            CustomMinimumSize = new Vector2(240, 0),
            ClearButtonEnabled = true,
        };
        StyleInput(search);
        search.TextChanged += text =>
        {
            _query = text;
            _limit = PageSize;
            Refresh();
        };
        row.AddChild(search);

        row.AddChild(_countLabel);
        bar.AddChild(row);

        HBoxContainer second = Ui.HBox(6);
        second.Alignment = BoxContainer.AlignmentMode.Begin;
        if (_category is "models" or "sounds" or "textures")
        {
            BuildFamilyChips(second);
        }
        bar.AddChild(second);

        return bar;
    }

    void BuildFamilyChips(HBoxContainer row)
    {
        List<MediaRow> rows = _category switch
        {
            "models" => Data.Models,
            "sounds" => Data.Sounds,
            _ => Data.Textures,
        } ?? new List<MediaRow>();

        foreach (IGrouping<string, MediaRow> group in rows.GroupBy(item => item.Family)
                     .OrderByDescending(group => group.Count()))
        {
            string family = group.Key;
            Button chip = Ui.Chip($"{Families.Label(family)} {group.Count()}", family == _family, 10);
            chip.Pressed += () =>
            {
                _family = family;
                _limit = PageSize;
                foreach (Button sibling in row.GetChildren().OfType<Button>()) sibling.QueueFree();
                BuildFamilyChips(row);
                Refresh();
            };
            row.AddChild(chip);
        }
    }

    void AddChip(string text, string value, string kind)
    {
        bool active = kind switch
        {
            "race" => _race == value,
            "doodad" => _doodadCategory == value,
            "family" => _family == value,
            _ => false,
        };

        Button chip = Ui.Chip(text, active);
        chip.Pressed += () =>
        {
            switch (kind)
            {
                case "race": _race = value; break;
                case "doodad": _doodadCategory = value; break;
                case "family": _family = value; break;
            }

            foreach (Button sibling in _chips.GetChildren().OfType<Button>())
            {
                sibling.AddThemeColorOverride("font_color", Ui.Dim);
                sibling.AddThemeStyleboxOverride("normal", Ui.Flat(Ui.PanelBg, Ui.Line, 3, 1, 9, 3));
            }

            chip.AddThemeColorOverride("font_color", Ui.Text);
            chip.AddThemeStyleboxOverride("normal", Ui.Flat(Color.FromHtml("1d1a14"), Color.FromHtml("4a3d24"), 3, 1, 9, 3));

            _limit = PageSize;
            Refresh();
        };
        _chips.AddChild(chip);
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

    static void StyleInput(LineEdit input)
    {
        input.AddThemeFontSizeOverride("font_size", 11);
        input.AddThemeColorOverride("font_color", Ui.Text);
        input.AddThemeColorOverride("font_placeholder_color", Ui.Faint);
        input.AddThemeStyleboxOverride("normal", Ui.Flat(Ui.PanelBg, Ui.Line, 4, 1, 9, 4));
        input.AddThemeStyleboxOverride("focus", Ui.Flat(Ui.Panel2, Ui.LineStrong, 4, 1, 9, 4));
    }

    void BuildData()
    {
        _all.Clear();

        switch (_category)
        {
            case "units":
                foreach (UnitRow unit in Data.Units ?? new List<UnitRow>())
                {
                    _all.Add(new ListItem
                    {
                        Id = unit.Id,
                        Title = unit.Name,
                        Meta = $"{unit.Id} · {unit.En} · {Data.ClassLabel(unit.Klass)} · {Data.RaceLabel(unit.Race)}",
                        IconPath = unit.Icon,
                        Stats =
                        {
                            ("HP", unit.Hp.ToString()),
                            ("护甲", unit.Armor.ToString()),
                            ("移速", unit.Speed.ToString()),
                        },
                    });
                }
                break;

            case "abilities":
                foreach (AbilityRow ability in Data.Abilities ?? new List<AbilityRow>())
                {
                    _all.Add(new ListItem
                    {
                        Id = ability.Id,
                        Title = ability.Name,
                        Meta = $"{ability.Id} · {ability.En}",
                        IconPath = ability.Icon,
                        Stats =
                        {
                            ("等级", ability.Levels.ToString()),
                            ("按钮位", string.IsNullOrEmpty(ability.Pos) ? "—" : ability.Pos),
                        },
                    });
                }
                break;

            case "doodads":
                foreach (DoodadRow doodad in Data.Doodads ?? new List<DoodadRow>())
                {
                    _all.Add(new ListItem
                    {
                        Id = doodad.Id,
                        Title = doodad.Name,
                        Meta = $"{doodad.Id} · {CategoryLabel(doodad.Category)} · {doodad.Base}",
                        Glyph = "❖",
                        Stats = { ("变体", doodad.Variants.ToString()) },
                    });
                }
                break;

            case "models":
                foreach (MediaRow row in Data.Models ?? new List<MediaRow>())
                {
                    _all.Add(new ListItem
                    {
                        Id = row.Path,
                        Title = row.Name,
                        Meta = row.Dir,
                        Glyph = "◇",
                        Stats = { ("来源", row.FamilyLabel) },
                    });
                }
                break;

            case "sounds":
                foreach (MediaRow row in Data.Sounds ?? new List<MediaRow>())
                {
                    _all.Add(new ListItem
                    {
                        Id = row.Path,
                        Title = row.Name,
                        Meta = row.Dir,
                        Glyph = "▶",
                        SoundPath = row.Path,
                        Stats = { ("来源", row.FamilyLabel) },
                    });
                }
                break;

            case "textures":
                foreach (MediaRow row in Data.Textures ?? new List<MediaRow>())
                {
                    _all.Add(new ListItem
                    {
                        Id = row.Path,
                        Title = row.Name,
                        Meta = row.Dir,
                        Stats = { ("来源", row.FamilyLabel) },
                    });
                }
                break;
        }
    }

    static string CategoryLabel(string category) => category switch
    {
        "E" => "环境",
        "O" => "物件",
        "S" => "结构",
        "W" => "水面",
        "C" => "悬崖",
        "Z" => "特殊",
        _ => category,
    };

    IEnumerable<ListItem> Filter()
    {
        IEnumerable<ListItem> items = _all;

        if (_category == "units")
        {
            if (_race != "all") items = items.Where(item => Data.UnitById[item.Id].Race == _race);
            if (_klass != "all") items = items.Where(item => Data.UnitById[item.Id].Klass == _klass);
        }

        if (_category == "doodads" && _doodadCategory != "all")
        {
            items = items.Where(item => Data.DoodadById[item.Id].Category == _doodadCategory);
        }

        if (_category is "models" or "sounds" or "textures" && _family != "all")
        {
            List<MediaRow> source = _category switch
            {
                "models" => Data.Models,
                "sounds" => Data.Sounds,
                _ => Data.Textures,
            } ?? new List<MediaRow>();
            HashSet<string> keep = source.Where(row => row.Family == _family).Select(row => row.Path).ToHashSet();
            items = items.Where(item => keep.Contains(item.Id));
        }

        if (!string.IsNullOrWhiteSpace(_query))
        {
            string query = _query.Trim().ToLowerInvariant();
            items = items.Where(item =>
                item.Title.ToLowerInvariant().Contains(query)
                || item.Id.ToLowerInvariant().Contains(query)
                || item.Meta.ToLowerInvariant().Contains(query));
        }

        return items;
    }

    void Refresh()
    {
        List<ListItem> items = Filter().ToList();
        _countLabel.Text = items.Count < _all.Count ? $"{Math.Min(_limit, items.Count)} / {items.Count}（已筛 / 共 {_all.Count}）" : $"{Math.Min(_limit, items.Count)} / {_all.Count}";

        Ui.Clear(_grid);
        Ui.Clear(_footer);

        foreach (ListItem item in items.Take(_limit))
        {
            _grid.AddChild(BuildCard(item));
        }

        if (items.Count > _limit)
        {
            Button more = Ui.ToolButton($"加载更多（还有 {items.Count - _limit} 项）", 11);
            more.Pressed += () =>
            {
                _limit += PageSize;
                Refresh();
            };
            MarginContainer wrap = Ui.Margin(0, 6, 0, 12);
            wrap.AddChild(more);
            _footer.AddChild(wrap);
        }
    }

    Control BuildCard(ListItem item)
    {
        PanelContainer card = Ui.Panel(Ui.PanelBg, Ui.Line);
        card.MouseFilter = MouseFilterEnum.Stop;
        card.MouseDefaultCursorShape = CursorShape.PointingHand;
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        card.ClipContents = true;

        VBoxContainer box = Ui.VBox(0);
        box.MouseFilter = MouseFilterEnum.Ignore;
        card.AddChild(box);

        box.AddChild(BuildThumb(item));

        VBoxContainer body = Ui.VBox(4);
        MarginContainer pad = Ui.Margin(12, 10, 12, 12);
        pad.MouseFilter = MouseFilterEnum.Ignore;
        pad.AddChild(body);
        box.AddChild(pad);

        Label title = Ui.Label(item.Title, 13, Ui.Text);
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        body.AddChild(title);

        Label meta = Ui.Label(item.Meta, 10, Ui.Faint);
        meta.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        body.AddChild(meta);

        HBoxContainer stats = Ui.HBox(6);
        foreach ((string label, string value) in item.Stats)
        {
            HBoxContainer pill = Ui.HBox(3);
            pill.AddChild(Ui.Label(label, 9, Ui.Faint));
            pill.AddChild(Ui.Label(value, 10, Ui.Dim));
            stats.AddChild(pill);
        }
        body.AddChild(stats);

        card.MouseEntered += () => card.AddThemeStyleboxOverride("panel", Ui.Flat(Ui.Hover, Ui.LineStrong));
        card.MouseExited += () => card.AddThemeStyleboxOverride("panel", Ui.Flat(Ui.PanelBg, Ui.Line));
        card.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            {
                App.Current.Navigate(_category, item.Id);
            }
        };

        return card;
    }

    Control BuildThumb(ListItem item)
    {
        PanelContainer thumb = new() { CustomMinimumSize = new Vector2(0, 104), ClipContents = true };
        thumb.AddThemeStyleboxOverride("panel", Ui.Flat(Ui.Panel2, Ui.LineSoft, 0, 0));
        thumb.MouseFilter = MouseFilterEnum.Ignore;

        CenterContainer center = new() { MouseFilter = MouseFilterEnum.Ignore };
        thumb.AddChild(center);

        Texture2D texture = item.Icon ?? (string.IsNullOrEmpty(item.IconPath) ? null : Data.IconFor(item.IconPath));
        if (texture == null && _category == "textures") texture = Assets.LoadTexture(item.Id);

        if (texture != null)
        {
            center.AddChild(Ui.Image(texture, _category == "textures" ? 96 : 68));
        }
        else
        {
            center.AddChild(Ui.Label(item.Glyph ?? "·", _category == "sounds" ? 22 : 20, Ui.Faint));
        }

        if (item.SoundPath != null)
        {
            thumb.MouseFilter = MouseFilterEnum.Stop;
            thumb.MouseDefaultCursorShape = CursorShape.PointingHand;
            thumb.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                {
                    PlaySound(item.SoundPath);
                }
            };
        }

        return thumb;
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
