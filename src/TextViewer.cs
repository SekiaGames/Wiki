using Godot;
using System;
using System.Collections.Generic;

public partial class TextViewer : VBoxContainer
{
    const int PageSize = 200;

    readonly List<KeyValuePair<string, string>> _all = new();
    readonly GridContainer _grid = new();
    readonly Label _countLabel = Ui.Label("—", 10, Ui.Faint);
    readonly VBoxContainer _footer = Ui.VBox(0);

    string _query = "";
    int _limit = PageSize;

    public TextViewer(Dictionary<string, string> keys)
    {
        foreach (KeyValuePair<string, string> pair in keys)
        {
            if (string.IsNullOrEmpty(pair.Key)) continue;
            _all.Add(pair);
        }
        _all.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

        AddThemeConstantOverride("separation", 10);
        AddChild(BuildBar());

        _grid.Columns = 2;
        _grid.AddThemeConstantOverride("h_separation", 16);
        _grid.AddThemeConstantOverride("v_separation", 4);
        _grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(_grid);

        AddChild(_footer);
        Refresh();
    }

    Control BuildBar()
    {
        HBoxContainer row = Ui.HBox(8);

        LineEdit search = new()
        {
            PlaceholderText = "搜索 key 或内容",
            CustomMinimumSize = new Vector2(280, 0),
            ClearButtonEnabled = true,
        };
        search.AddThemeFontSizeOverride("font_size", 11);
        search.AddThemeColorOverride("font_color", Ui.Text);
        search.AddThemeColorOverride("font_placeholder_color", Ui.Faint);
        search.AddThemeStyleboxOverride("normal", Ui.Flat(Ui.PanelBg, Ui.Line, 4, 1, 9, 4));
        search.AddThemeStyleboxOverride("focus", Ui.Flat(Ui.Panel2, Ui.LineStrong, 4, 1, 9, 4));
        search.TextChanged += text =>
        {
            _query = text;
            _limit = PageSize;
            Refresh();
        };
        row.AddChild(search);

        row.AddChild(_countLabel);
        return row;
    }

    IEnumerable<KeyValuePair<string, string>> Filter()
    {
        if (string.IsNullOrWhiteSpace(_query)) return _all;

        string query = _query.Trim().ToLowerInvariant();
        List<KeyValuePair<string, string>> hits = new();
        foreach (KeyValuePair<string, string> pair in _all)
        {
            if (pair.Key.ToLowerInvariant().Contains(query) || pair.Value.ToLowerInvariant().Contains(query))
            {
                hits.Add(pair);
            }
        }
        return hits;
    }

    void Refresh()
    {
        List<KeyValuePair<string, string>> hits = new(Filter());
        int shown = Math.Min(_limit, hits.Count);
        _countLabel.Text = hits.Count == _all.Count
            ? $"{shown} / {_all.Count} 条"
            : $"{shown} / {hits.Count}（已筛 / 共 {_all.Count}）";

        Ui.Clear(_grid);
        Ui.Clear(_footer);

        for (int i = 0; i < shown; i++)
        {
            Label key = Ui.Label(hits[i].Key, 10, Ui.Faint);
            key.CustomMinimumSize = new Vector2(320, 0);
            key.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            key.TooltipText = hits[i].Key;
            _grid.AddChild(key);

            Label value = Ui.Label(hits[i].Value, 11, Ui.Dim, true);
            value.CustomMinimumSize = new Vector2(360, 0);
            _grid.AddChild(value);
        }

        if (hits.Count > shown)
        {
            Button more = Ui.ToolButton($"加载更多（还有 {hits.Count - shown} 条）", 10);
            more.Pressed += () =>
            {
                _limit += PageSize;
                Refresh();
            };
            MarginContainer wrap = Ui.Margin(0, 4, 0, 0);
            wrap.AddChild(more);
            _footer.AddChild(wrap);
        }
    }
}
