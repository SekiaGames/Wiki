using Godot;
using System.Collections.Generic;

public partial class OverviewPage : Page
{
    public OverviewPage()
    {
        CategoryDef meta = Catalog.Get("overview");

        ScrollContainer scroll = Ui.Scroll();
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(scroll);

        VBoxContainer root = Ui.VBox(20);
        root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(root);

        root.AddChild(PageParts.PageHeader(meta));

        GridContainer grid = new() { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 14);
        grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(grid);

        foreach (CategoryDef category in Catalog.All)
        {
            if (category.Id == "overview") continue;
            grid.AddChild(StatCard(category));
        }

        root.AddChild(Ui.Separator());
        root.AddChild(DataPanel());
    }

    Control StatCard(CategoryDef category)
    {
        (int? count, string note) = CountFor(category.Id);

        PanelContainer card = Ui.Panel(Ui.PanelBg, Ui.Line);
        card.MouseFilter = MouseFilterEnum.Stop;
        card.MouseDefaultCursorShape = CursorShape.PointingHand;
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        VBoxContainer box = Ui.VBox(4);
        MarginContainer pad = Ui.Margin(16, 14, 16, 14);
        pad.AddChild(box);
        card.AddChild(pad);

        HBoxContainer head = Ui.HBox(8);
        head.AddChild(Ui.Label(category.Glyph, 11, Ui.Faint));
        head.AddChild(Ui.Label(category.Label, 12, Ui.Dim));
        box.AddChild(head);

        Label number = Ui.SerifLabel(count.HasValue ? count.Value.ToString("N0") : "—", 26, Ui.Gold);
        box.AddChild(number);
        box.AddChild(Ui.Label(note, 10, Ui.Faint, true));

        card.MouseEntered += () => card.AddThemeStyleboxOverride("panel", Ui.Flat(Ui.Hover, Ui.LineStrong));
        card.MouseExited += () => card.AddThemeStyleboxOverride("panel", Ui.Flat(Ui.PanelBg, Ui.Line));
        card.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            {
                App.Current.Navigate(category.Id);
            }
        };

        return card;
    }

    static (int?, string) CountFor(string category)
    {
        return category switch
        {
            "units" => (Data.Units?.Count, "来自 Tables/*UnitUI.json"),
            "abilities" => (Data.Abilities?.Count, "来自 Tables/*AbilityData.json"),
            "doodads" => (Data.Doodads?.Count, "来自 Tables/*Doodads.json"),
            "models" => (Data.Models?.Count, "extract 内全部 .glb"),
            "sounds" => (Data.Sounds?.Count, "extract 内全部 .wav"),
            "textures" => (Data.Textures?.Count, "extract 内全部 .png"),
            _ => (null, "后续阶段接入"),
        };
    }

    Control DataPanel()
    {
        PanelContainer panel = Ui.Panel(Ui.PanelBg, Ui.Line);
        VBoxContainer box = Ui.VBox(6);
        MarginContainer pad = Ui.Margin(16, 14, 16, 14);
        pad.AddChild(box);
        panel.AddChild(pad);

        box.AddChild(Ui.Label("数据源", 11, Ui.Dim));
        box.AddChild(Kv("extract.pck", Assets.PackMounted ? Assets.PckPath : "未挂载"));
        box.AddChild(Kv("资源目录", Assets.DiskRoot));
        box.AddChild(Kv("表格", Data.TableFiles == null ? "—" : $"{Data.TableFiles.Count} 个 json"));
        box.AddChild(Kv("文本", Data.TextFiles == null ? "—" : $"{Data.TextFiles.Count} 个 json"));
        return panel;
    }

    static Control Kv(string key, string value)
    {
        HBoxContainer row = Ui.HBox(10);
        Label name = Ui.Label(key, 11, Ui.Faint);
        name.CustomMinimumSize = new Vector2(84, 0);
        row.AddChild(name);
        row.AddChild(Ui.Label(value, 11, Ui.Dim));
        return row;
    }
}
