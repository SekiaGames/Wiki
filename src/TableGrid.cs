using Godot;
using System;
using System.Collections.Generic;

public partial class TableGrid : VBoxContainer
{
    const int ColumnPageSize = 8;
    const int RowPageSize = 40;
    const int CellWidth = 132;

    readonly Table _table;
    readonly GridContainer _grid = new();
    readonly Label _columnLabel = Ui.Label("—", 10, Ui.Faint);
    readonly Label _rowLabel = Ui.Label("—", 10, Ui.Faint);
    readonly VBoxContainer _footer = Ui.VBox(0);

    int _columnPage;
    int _rowLimit = RowPageSize;

    public TableGrid(Table table)
    {
        _table = table;
        AddThemeConstantOverride("separation", 10);

        AddChild(BuildColumnBar());

        _grid.AddThemeConstantOverride("h_separation", 10);
        _grid.AddThemeConstantOverride("v_separation", 4);
        _grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(_grid);

        AddChild(_footer);
        Refresh();
    }

    int ColumnCount => _table.Columns.Length;
    int ColumnPages => Math.Max(1, (ColumnCount + ColumnPageSize - 1) / ColumnPageSize);
    int ColumnStart => _columnPage * ColumnPageSize;
    int ColumnEnd => Math.Min(ColumnCount, ColumnStart + ColumnPageSize);

    Control BuildColumnBar()
    {
        HBoxContainer bar = Ui.HBox(8);
        bar.AddChild(_columnLabel);

        Button previous = Ui.ToolButton("‹ 上一组列", 10);
        previous.Pressed += () => SetColumnPage(_columnPage - 1);
        bar.AddChild(previous);

        Button next = Ui.ToolButton("下一组列 ›", 10);
        next.Pressed += () => SetColumnPage(_columnPage + 1);
        bar.AddChild(next);

        bar.AddChild(Ui.Spacer());
        bar.AddChild(_rowLabel);
        return bar;
    }

    void SetColumnPage(int page)
    {
        _columnPage = Mathf.Clamp(page, 0, ColumnPages - 1);
        Refresh();
    }

    void Refresh()
    {
        _columnLabel.Text = $"列 {ColumnStart + 1}–{ColumnEnd} / {ColumnCount}";
        int rows = Math.Min(_rowLimit, _table.Rows.Count);
        _rowLabel.Text = $"{rows} / {_table.Rows.Count} 行";

        Ui.Clear(_grid);
        Ui.Clear(_footer);

        _grid.Columns = ColumnEnd - ColumnStart + 1;
        _grid.AddChild(HeaderCell("#"));
        for (int column = ColumnStart; column < ColumnEnd; column++)
        {
            _grid.AddChild(HeaderCell(_table.Columns[column]));
        }

        for (int row = 0; row < rows; row++)
        {
            string[] cells = _table.Rows[row];
            _grid.AddChild(Cell((row + 1).ToString(), Ui.Faint));
            for (int column = ColumnStart; column < ColumnEnd; column++)
            {
                _grid.AddChild(Cell(column < cells.Length ? cells[column] : "", Ui.Dim));
            }
        }

        if (rows < _table.Rows.Count)
        {
            Button more = Ui.ToolButton($"加载更多行（还有 {_table.Rows.Count - rows} 行）", 10);
            more.Pressed += () =>
            {
                _rowLimit += RowPageSize;
                Refresh();
            };
            MarginContainer wrap = Ui.Margin(0, 4, 0, 0);
            wrap.AddChild(more);
            _footer.AddChild(wrap);
        }
    }

    static Label HeaderCell(string text)
    {
        Label label = Ui.Label(text, 10, Ui.Gold);
        label.CustomMinimumSize = new Vector2(CellWidth, 0);
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.TooltipText = text;
        return label;
    }

    static Label Cell(string text, Color color)
    {
        Label label = Ui.Label(string.IsNullOrEmpty(text) ? "—" : text, 10, color);
        label.CustomMinimumSize = new Vector2(CellWidth, 0);
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.TooltipText = text;
        return label;
    }
}
