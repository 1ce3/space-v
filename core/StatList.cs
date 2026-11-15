using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;

namespace RocketGame.Core;

/// <summary>
/// Two-column list for results and stats: property name on the left, value aligned on the right.
/// Rows without a value span the whole width (section titles). Used by the debrief, the flight log,
/// the build screen's general info and the wind tunnel result.
/// </summary>
public partial class StatList : GridContainer
{
    public int FontSize { get; set; } = 15;
    public Color NameColor { get; set; } = new(0.68f, 0.72f, 0.79f);
    public Color ValueColor { get; set; } = new(0.94f, 0.95f, 0.97f);
    public Color TitleColor { get; set; } = new(0.7f, 0.85f, 1f);

    public StatList()
    {
        Columns = 2;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("h_separation", 16);
        AddThemeConstantOverride("v_separation", 4);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>One row; value null = full-width title row; color overrides the value color.</summary>
    public readonly record struct Row(string Name, string Value, Color? Color = null);

    /// <summary>Replaces all rows.</summary>
    public void SetRows(IEnumerable<Row> rows)
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        foreach (Row row in rows) AddRow(row);
    }

    public void AddRow(Row row)
    {
        bool title = row.Value == null;
        var name = new Label
        {
            Text = row.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        name.AddThemeFontSizeOverride("font_size", FontSize);
        name.AddThemeColorOverride("font_color", title ? TitleColor : NameColor);
        AddChild(name);

        var value = new Label { Text = row.Value ?? "", HorizontalAlignment = HorizontalAlignment.Right };
        value.AddThemeFontSizeOverride("font_size", FontSize);
        value.AddThemeColorOverride("font_color", row.Color ?? ValueColor);
        AddChild(value);
    }

    /// <summary>
    /// Rows from text lines "Name: value" or "Name   value" (two or more spaces); other lines become titles.
    /// Empty lines are skipped.
    /// </summary>
    public static List<Row> Parse(string text, Color? valueColor = null)
    {
        var rows = new List<Row>();
        foreach (string raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            Match gap = Regex.Match(line, @"^(.*?\S)\s{2,}(\S.*)$");
            if (gap.Success)
            {
                rows.Add(new Row(gap.Groups[1].Value, gap.Groups[2].Value, valueColor));
                continue;
            }
            int colon = line.IndexOf(':');
            if (colon > 0 && colon < line.Length - 1)
            {
                rows.Add(new Row(line[..colon].Trim(), line[(colon + 1)..].Trim(), valueColor));
                continue;
            }
            rows.Add(new Row(line.TrimEnd(':'), null));
        }
        return rows;
    }

    /// <summary>
    /// Shows a label's text as two columns: hides the label and puts a StatList right after it.
    /// The label keeps its text, so callers can still read it. Calling again refreshes the list.
    /// </summary>
    public static StatList ReplaceLabel(Label label, Color? valueColor = null, int fontSize = 0)
    {
        if (label?.GetParent() is not Node parent) return null;
        string key = "StatList_" + label.Name;
        var list = parent.GetNodeOrNull<StatList>(key);
        if (list == null)
        {
            list = new StatList { Name = key };
            int size = label.GetThemeFontSize("font_size");
            if (fontSize > 0) list.FontSize = fontSize;
            else if (size > 0) list.FontSize = size;
            parent.AddChild(list);
            parent.MoveChild(list, label.GetIndex() + 1);
        }
        label.Visible = false;
        list.SetRows(Parse(label.Text, valueColor));
        return list;
    }
}
