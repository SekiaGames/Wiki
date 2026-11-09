using Godot;

public partial class Page : Control
{
    protected Page()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
    }
}

public static class PageParts
{
    public static Control Header(string eyebrow, string title, string sub)
    {
        VBoxContainer box = Ui.VBox(5);
        box.AddChild(Ui.Label(eyebrow, 9, Ui.Faint));

        Label heading = Ui.SerifLabel(title, 22, Ui.Text);
        box.AddChild(heading);

        Label description = Ui.Label(sub, 12, Ui.Dim, true);
        description.CustomMinimumSize = new Vector2(0, 0);
        box.AddChild(description);
        return box;
    }

    public static Control PageHeader(CategoryDef meta)
    {
        return Header($"THE ARCHIVE / {meta.Group}", meta.Title, meta.Sub);
    }

    public static Control SectionTitle(string text)
    {
        Label label = Ui.Label(text, 10, Ui.Faint);
        return label;
    }

    public static Control Empty(string title, string note)
    {
        VBoxContainer box = Ui.VBox(6);
        box.AddThemeConstantOverride("separation", 6);
        box.AddChild(Ui.Label(title, 14, Ui.Dim));
        box.AddChild(Ui.Label(note, 12, Ui.Faint, true));

        MarginContainer margin = Ui.Margin(20, 24, 20, 24);
        margin.AddChild(box);
        return margin;
    }
}

public partial class PendingPage : Page
{
    public PendingPage(string category)
    {
        CategoryDef meta = Catalog.Get(category);
        ScrollContainer scroll = Ui.Scroll();
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(scroll);

        VBoxContainer root = Ui.VBox(18);
        root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(root);

        root.AddChild(PageParts.PageHeader(meta));
        root.AddChild(Ui.Separator());

        PanelContainer panel = Ui.Panel(Ui.PanelBg, Ui.Line);
        panel.AddChild(PageParts.Empty("该分类尚未接入", $"{meta.Title} 目前只保留入口，数据接入排在后面的阶段。"));
        root.AddChild(panel);
    }
}
