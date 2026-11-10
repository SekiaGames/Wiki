using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CategoryDef
{
    public string Id, Label, Glyph, Group, Title, Sub;
}

public static class Catalog
{
    public static readonly CategoryDef[] All =
    {
        new() { Id = "overview", Label = "概览", Glyph = "◈", Group = "浏览", Title = "资源总览", Sub = "一座只读的资源档案馆：单位、技能、装饰物、音效与贴图，全部直接取自 extract.pck。" },
        new() { Id = "units", Label = "单位", Glyph = "◆", Group = "资源", Title = "单位", Sub = "每个单位的历史、数值与真实模型。选中后可以播放动画、浏览技能与音效。" },
        new() { Id = "abilities", Label = "技能", Glyph = "✦", Group = "资源", Title = "技能", Sub = "技能数值、按钮位置与施法动画名。" },
        new() { Id = "doodads", Label = "装饰物", Glyph = "❖", Group = "资源", Title = "装饰物", Sub = "树木、岩石与场景道具，按风格聚类。" },
        new() { Id = "destructables", Label = "可破坏物", Glyph = "▣", Group = "资源", Title = "可破坏物", Sub = "可被摧毁的场景物件与掉落。" },
        new() { Id = "items", Label = "物品", Glyph = "◇", Group = "资源", Title = "物品", Sub = "掉落物品与商店货架。" },
        new() { Id = "upgrades", Label = "升级", Glyph = "▲", Group = "资源", Title = "升级", Sub = "科技升级与它们的依赖关系。" },
        new() { Id = "models", Label = "模型库", Glyph = "▤", Group = "媒体", Title = "模型库", Sub = "按目录浏览全部模型，含 portrait 版本。" },
        new() { Id = "sounds", Label = "音效", Glyph = "♪", Group = "媒体", Title = "音效", Sub = "单位语音、战斗音效与环境声。" },
        new() { Id = "textures", Label = "贴图", Glyph = "▩", Group = "媒体", Title = "贴图", Sub = "地形贴图、图标与界面素材。" },
        new() { Id = "tables", Label = "表格", Glyph = "▦", Group = "数据", Title = "表格", Sub = "Tables/ 下的原始数据表，可做交叉核对。" },
        new() { Id = "texts", Label = "文本", Glyph = "≡", Group = "数据", Title = "文本", Sub = "Texts/ 下的本地化字符串与图标字段。" },
    };

    public static CategoryDef Get(string id)
    {
        return All.FirstOrDefault(item => item.Id == id) ?? All[0];
    }

    public static readonly string[] Pending = Array.Empty<string>();

    public static bool IsPending(string id) => Pending.Contains(id);
}

public partial class Sidebar : PanelContainer
{
    readonly Dictionary<string, PanelContainer> _items = new();
    readonly Dictionary<string, Label> _counts = new();
    readonly Action<string> _onSelect;
    string _active = "overview";

    public Sidebar(Action<string> onSelect)
    {
        _onSelect = onSelect;
        CustomMinimumSize = new Vector2(236, 0);
        AddThemeStyleboxOverride("panel", Ui.Flat(Color.FromHtml("0b0b10"), Ui.LineSoft, 0, 0));

        VBoxContainer root = Ui.VBox(0);
        AddChild(root);
        root.AddChild(BuildBrand());
        root.AddChild(BuildNav());
        root.AddChild(Ui.Spacer());
        root.AddChild(BuildFoot());
    }

    static Control BuildBrand()
    {
        HBoxContainer row = Ui.HBox(12);
        row.AddThemeConstantOverride("separation", 12);

        PanelContainer mark = Ui.Panel(Color.FromHtml("141118"), Ui.LineStrong, 3);
        mark.CustomMinimumSize = new Vector2(34, 34);
        Label glyph = Ui.SerifLabel("W3", 14, Ui.Gold);
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        glyph.VerticalAlignment = VerticalAlignment.Center;
        mark.AddChild(glyph);
        row.AddChild(mark);

        VBoxContainer text = Ui.VBox(2);
        text.AddChild(Ui.SerifLabel("ARCHIVE", 14, Ui.Text));
        text.AddChild(Ui.Label("WAR3 ASSET WIKI", 9, Ui.Faint));
        row.AddChild(text);

        MarginContainer wrap = Ui.Margin(20, 18, 20, 16);
        wrap.AddChild(row);

        PanelContainer panel = new();
        panel.AddThemeStyleboxOverride("panel", Ui.Flat(Color.FromHtml("0b0b10"), Ui.LineSoft, 0, 0));
        panel.AddChild(wrap);
        return panel;
    }

    Control BuildNav()
    {
        VBoxContainer nav = Ui.VBox(16);
        nav.AddThemeConstantOverride("separation", 16);

        foreach (IGrouping<string, CategoryDef> group in Catalog.All.GroupBy(item => item.Group))
        {
            VBoxContainer block = Ui.VBox(2);
            MarginContainer titleWrap = Ui.Margin(10, 0, 10, 6);
            titleWrap.AddChild(Ui.Label(group.Key, 9, Ui.Faint));
            block.AddChild(titleWrap);

            foreach (CategoryDef category in group)
            {
                block.AddChild(BuildItem(category));
            }

            nav.AddChild(block);
        }

        MarginContainer wrap = Ui.Margin(10, 14, 10, 20);
        wrap.AddChild(nav);
        return wrap;
    }

    Control BuildItem(CategoryDef category)
    {
        PanelContainer item = new()
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(0, 30),
        };
        item.AddThemeStyleboxOverride("panel", Ui.Flat(Colors.Transparent, Colors.Transparent, 4, 1));

        HBoxContainer row = Ui.HBox(9);
        row.AddThemeConstantOverride("separation", 9);
        MarginContainer pad = Ui.Margin(10, 6, 10, 6);
        pad.MouseFilter = Control.MouseFilterEnum.Ignore;

        Label glyph = Ui.Label(category.Glyph, 10, Ui.Faint);
        glyph.CustomMinimumSize = new Vector2(14, 0);
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        row.AddChild(glyph);

        Label label = Ui.Label(category.Label, 12, Ui.Dim);
        row.AddChild(label);

        Label count = Ui.Label("—", 10, Ui.Faint);
        count.HorizontalAlignment = HorizontalAlignment.Right;
        count.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(count);

        pad.AddChild(row);
        item.AddChild(pad);

        item.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) _onSelect(category.Id);
        };
        item.MouseEntered += () =>
        {
            if (_active != category.Id) item.AddThemeStyleboxOverride("panel", Ui.Flat(Ui.PanelBg, Colors.Transparent, 4, 1));
            label.AddThemeColorOverride("font_color", Ui.Text);
        };
        item.MouseExited += () =>
        {
            if (_active != category.Id) item.AddThemeStyleboxOverride("panel", Ui.Flat(Colors.Transparent, Colors.Transparent, 4, 1));
            label.AddThemeColorOverride("font_color", _active == category.Id ? Ui.Text : Ui.Dim);
        };

        _items[category.Id] = item;
        _counts[category.Id] = count;
        return item;
    }

    Control BuildFoot()
    {
        VBoxContainer foot = Ui.VBox(3);
        foot.AddChild(Ui.Separator());

        MarginContainer wrap = Ui.Margin(20, 12, 20, 16);
        VBoxContainer inner = Ui.VBox(3);
        inner.AddChild(Ui.Label($"●  {(Assets.PackMounted ? "extract.pck 已挂载" : "extract.pck 未挂载")}", 10,
            Assets.PackMounted ? Ui.Gold : Ui.Danger));
        inner.AddChild(Ui.Label("只读预览，不写入任何资源", 10, Ui.Faint));
        wrap.AddChild(inner);
        foot.AddChild(wrap);
        return foot;
    }

    public void SetActive(string category)
    {
        _active = category;

        foreach (KeyValuePair<string, PanelContainer> pair in _items)
        {
            bool active = pair.Key == category;
            pair.Value.AddThemeStyleboxOverride("panel", active
                ? Ui.Flat(Color.FromHtml("1a1710"), Color.FromHtml("443722"), 4, 1)
                : Ui.Flat(Colors.Transparent, Colors.Transparent, 4, 1));
        }
    }

    public void RefreshCounts()
    {
        SetCount("units", Data.Units?.Count);
        SetCount("abilities", Data.Abilities?.Count);
        SetCount("doodads", Data.Doodads?.Count);
        SetCount("destructables", Data.Destructables?.Count);
        SetCount("items", Data.Items?.Count);
        SetCount("upgrades", Data.Upgrades?.Count);
        SetCount("models", Data.Models?.Count);
        SetCount("sounds", Data.Sounds?.Count);
        SetCount("textures", Data.Textures?.Count);
        SetCount("tables", Data.TableFiles?.Count);
        SetCount("texts", Data.TextFiles?.Count);
    }

    void SetCount(string id, int? count)
    {
        if (_counts.TryGetValue(id, out Label label)) label.Text = count.HasValue ? count.Value.ToString() : "—";
    }
}
