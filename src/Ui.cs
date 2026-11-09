using Godot;

public static class Ui
{
    public static readonly Color Bg = Color.FromHtml("0a0a0d");
    public static readonly Color BgDeep = Color.FromHtml("07070a");
    public static readonly Color PanelBg = Color.FromHtml("111116");
    public static readonly Color Panel2 = Color.FromHtml("15151c");
    public static readonly Color Hover = Color.FromHtml("1a1a22");
    public static readonly Color Line = Color.FromHtml("232330");
    public static readonly Color LineSoft = Color.FromHtml("1b1b24");
    public static readonly Color LineStrong = Color.FromHtml("33333f");
    public static readonly Color Text = Color.FromHtml("e9e7e3");
    public static readonly Color Dim = Color.FromHtml("9b98a5");
    public static readonly Color Faint = Color.FromHtml("6b6976");
    public static readonly Color Gold = Color.FromHtml("d7b478");
    public static readonly Color Teal = Color.FromHtml("5aa7a2");
    public static readonly Color Danger = Color.FromHtml("c96a6a");

    static Font _font;
    public static Font Font => _font ??= new SystemFont
    {
        FontNames = new[] { "PingFang SC", "Hiragino Sans GB", "Microsoft YaHei", "Segoe UI", "Inter" },
    };

    static Font _serif;
    public static Font Serif => _serif ??= new SystemFont
    {
        FontNames = new[] { "Songti SC", "Georgia", "Times New Roman", "SimSun" },
    };

    public static Theme BuildTheme()
    {
        Theme theme = new()
        {
            DefaultFont = Font,
            DefaultFontSize = 13,
        };
        return theme;
    }

    public static StyleBoxFlat Flat(Color background, Color border, int radius = 5, int borderWidth = 1,
        int padH = 0, int padV = 0)
    {
        StyleBoxFlat box = new()
        {
            BgColor = background,
            BorderColor = border,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
        };

        if (borderWidth > 0) box.SetBorderWidthAll(borderWidth);
        if (padH > 0) box.ContentMarginLeft = box.ContentMarginRight = padH;
        if (padV > 0) box.ContentMarginTop = box.ContentMarginBottom = padV;
        return box;
    }

    public static Label Label(string text, int size = 13, Color? color = null, bool wrap = false)
    {
        Label label = new() { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Text);
        if (wrap) label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    public static Label SerifLabel(string text, int size = 15, Color? color = null)
    {
        Label label = Label(text, size, color);
        label.AddThemeFontOverride("font", Serif);
        return label;
    }

    public static Button TextButton(string text, int size = 12, Color? color = null)
    {
        Button button = new() { Text = text, Flat = true, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeColorOverride("font_color", color ?? Dim);
        button.AddThemeColorOverride("font_hover_color", Text);
        button.AddThemeColorOverride("font_pressed_color", Gold);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        return button;
    }

    public static Button ToolButton(string text, int size = 11, bool accent = false)
    {
        Button button = new() { Text = text, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeColorOverride("font_color", accent ? Gold : Dim);
        button.AddThemeColorOverride("font_hover_color", accent ? Text : Text);
        button.AddThemeColorOverride("font_pressed_color", Gold);
        button.AddThemeStyleboxOverride("normal", Flat(PanelBg, accent ? LineStrong : Line, 4, 1, 10, 4));
        button.AddThemeStyleboxOverride("hover", Flat(Hover, accent ? Gold : LineStrong, 4, 1, 10, 4));
        button.AddThemeStyleboxOverride("pressed", Flat(Panel2, Gold, 4, 1, 10, 4));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        return button;
    }

    public static Button Chip(string text, bool active, int size = 11)
    {
        Button button = new() { Text = text, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeColorOverride("font_color", active ? Text : Dim);
        button.AddThemeColorOverride("font_hover_color", Text);
        button.AddThemeColorOverride("font_pressed_color", Gold);
        button.AddThemeStyleboxOverride("normal", Flat(active ? Color.FromHtml("1d1a14") : PanelBg,
            active ? Color.FromHtml("4a3d24") : Line, 3, 1, 9, 3));
        button.AddThemeStyleboxOverride("hover", Flat(Hover, active ? Gold : LineStrong, 3, 1, 9, 3));
        button.AddThemeStyleboxOverride("pressed", Flat(Panel2, Gold, 3, 1, 9, 3));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        return button;
    }

    public static PanelContainer Panel(Color background, Color border, int radius = 5, int borderWidth = 1)
    {
        PanelContainer panel = new();
        panel.AddThemeStyleboxOverride("panel", Flat(background, border, radius, borderWidth));
        return panel;
    }

    public static MarginContainer Margin(int left, int top, int right, int bottom)
    {
        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", left);
        margin.AddThemeConstantOverride("margin_top", top);
        margin.AddThemeConstantOverride("margin_right", right);
        margin.AddThemeConstantOverride("margin_bottom", bottom);
        return margin;
    }

    public static VBoxContainer VBox(int separation)
    {
        VBoxContainer box = new();
        box.AddThemeConstantOverride("separation", separation);
        return box;
    }

    public static HBoxContainer HBox(int separation)
    {
        HBoxContainer box = new();
        box.AddThemeConstantOverride("separation", separation);
        return box;
    }

    public static Control Spacer()
    {
        return new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
    }

    public static HSeparator Separator()
    {
        HSeparator line = new();
        StyleBoxLine style = new() { Color = LineSoft, Thickness = 1 };
        line.AddThemeStyleboxOverride("separator", style);
        return line;
    }

    public static void Clear(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    public static ScrollContainer Scroll(bool horizontal = false)
    {
        ScrollContainer scroll = new()
        {
            HorizontalScrollMode = horizontal ? ScrollContainer.ScrollMode.Auto : ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        return scroll;
    }

    public static TextureRect Image(Texture2D texture, int size, bool pixel = false)
    {
        TextureRect rect = new()
        {
            Texture = texture,
            CustomMinimumSize = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (pixel) rect.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        return rect;
    }
}
