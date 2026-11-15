// AI-assisted: written with Claude (Anthropic); reviewed by The Neptunians.
using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Control;

/// <summary>
/// Home hub (Continue lands here), built in code over the 3D launch site: last flight log (with "View full log"
/// to read the debrief again), Research, and the rocket design kept from last time (opens the workshop).
/// Emits; GameFlowManager switches the screens.
/// </summary>
public partial class HubScreen : Godot.Control
{
    [Signal] public delegate void ResearchRequestedEventHandler();
    [Signal] public delegate void BuildRequestedEventHandler();
    [Signal] public delegate void MenuRequestedEventHandler();

    private Godot.Control _logOverlay;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MenuBackground.AddTo(this);

        // Top bar: back to the menu, RP and max weight.
        var top = new HBoxContainer();
        AddChild(top);
        top.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        top.OffsetLeft = 24;
        top.OffsetRight = -24;
        top.OffsetTop = 20;
        top.AddThemeConstantOverride("separation", 12);
        top.AddChild(UiStyle.MakeButton("◀  Menu", () => EmitSignal(SignalName.MenuRequested), false, 16, 40));
        top.AddChild(new Godot.Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        ResearchCampaignEngine campaign = ResearchCampaignEngine.Instance;
        top.AddChild(Badge("RP", campaign?.CurrentRP.ToString() ?? "-"));
        top.AddChild(Badge("Max weight", campaign?.TechTree?.GetMaxBuildWeight().ToString() ?? "-"));

        // Cards: log on the left, research and design on the right.
        var center = UiStyle.AddFull(this, new CenterContainer { MouseFilter = MouseFilterEnum.Ignore });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 20);
        center.AddChild(row);
        row.AddChild(BuildLogCard());
        var right = new VBoxContainer();
        right.AddThemeConstantOverride("separation", 20);
        row.AddChild(right);
        right.AddChild(BuildResearchCard());
        right.AddChild(BuildDesignCard());
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel")) return;
        if (_logOverlay != null) CloseLog();
        else EmitSignal(SignalName.MenuRequested);
        GetViewport().SetInputAsHandled();
    }

    // ------------------------------------------------------------------ cards

    private Godot.Control BuildLogCard()
    {
        PanelContainer card = UiStyle.Panel(22);
        card.CustomMinimumSize = new Vector2(420, 420);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        card.AddChild(v);
        v.AddChild(UiStyle.MakeLabel("Last flight log", 24));

        if (!StoryProgress.HasLastLog)
        {
            v.AddChild(UiStyle.MakeLabel("No flight yet. Build a rocket in the workshop and launch it.", 16, UiStyle.Muted, true));
            return card;
        }

        var title = UiStyle.MakeLabel(StoryProgress.LastLogTitle, 20, OutcomeColor(StoryProgress.LastLogOutcome));
        v.AddChild(title);
        // Short version: the first rows of the summary, names left and values right.
        List<StatList.Row> rows = StatList.Parse(StoryProgress.LastLogBody);
        var stats = new StatList { FontSize = 15, SizeFlagsVertical = SizeFlags.ExpandFill };
        stats.SetRows(rows.GetRange(0, Mathf.Min(7, rows.Count)));
        v.AddChild(stats);
        v.AddChild(UiStyle.MakeButton("View full log", ShowLog));
        return card;
    }

    private Godot.Control BuildResearchCard()
    {
        PanelContainer card = UiStyle.Panel(22);
        card.CustomMinimumSize = new Vector2(360, 0);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        card.AddChild(v);
        v.AddChild(UiStyle.MakeLabel("Research", 24));
        TechTreeManager tree = ResearchCampaignEngine.Instance?.TechTree;
        int owned = tree?.UnlockedNodes.Count ?? 0;
        int total = tree?.AllNodes.Count ?? 0;
        v.AddChild(UiStyle.MakeLabel($"{owned} / {total} technologies unlocked. Spend research points on new parts.", 15, UiStyle.Muted, true));
        v.AddChild(UiStyle.MakeButton("Open research", () => EmitSignal(SignalName.ResearchRequested)));
        return card;
    }

    private Godot.Control BuildDesignCard()
    {
        PanelContainer card = UiStyle.Panel(22);
        card.CustomMinimumSize = new Vector2(360, 0);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        card.AddChild(v);
        v.AddChild(UiStyle.MakeLabel("Rocket design", 24));

        RocketDesign design = RocketDesign.LoadLast();
        if (design == null || design.IsEmpty)
        {
            v.AddChild(UiStyle.MakeLabel("No saved design. Start one in the workshop.", 15, UiStyle.Muted, true));
            v.AddChild(UiStyle.MakeButton("New design", () => EmitSignal(SignalName.BuildRequested), true));
            return card;
        }

        int weight = 0;
        var counts = new Dictionary<string, int>();
        foreach (RocketDesign.PartEntry entry in design.PartEntries)
        {
            ComponentDefinition def = PartCatalog.Instance?.GetDefinition(entry.ModelId);
            weight += def?.BuildWeight ?? 0;
            string name = def == null || string.IsNullOrEmpty(def.DisplayName) ? entry.ModelId : def.DisplayName;
            counts[name] = counts.TryGetValue(name, out int n) ? n + 1 : 1;
        }
        v.AddChild(UiStyle.MakeLabel(
            $"{design.PartEntries.Count} parts, {design.DecouplerEntries.Count} decouplers, {design.ModuleEntries.Count} modules · weight {weight}",
            15, UiStyle.Muted, true));
        var list = new List<string>();
        foreach ((string name, int n) in counts) list.Add(n > 1 ? $"{n} × {name}" : name);
        if (list.Count > 6) list = list.GetRange(0, 6);
        v.AddChild(UiStyle.MakeLabel("• " + string.Join("\n• ", list), 14, UiStyle.Text, true));
        v.AddChild(UiStyle.MakeButton("Open workshop", () => EmitSignal(SignalName.BuildRequested), true));
        return card;
    }

    // ------------------------------------------------------------------ full log

    private void ShowLog()
    {
        if (_logOverlay != null) return;
        var layer = UiStyle.AddFull(this, new Godot.Control());
        UiStyle.AddFull(layer, new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f) });
        var center = UiStyle.AddFull(layer, new CenterContainer());
        PanelContainer panel = UiStyle.Panel(28);
        panel.CustomMinimumSize = new Vector2(560, 0);
        center.AddChild(panel);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 14);
        panel.AddChild(v);
        v.AddChild(UiStyle.MakeLabel("Flight log", 16, UiStyle.Muted));
        v.AddChild(UiStyle.MakeLabel(StoryProgress.LastLogTitle, 30, OutcomeColor(StoryProgress.LastLogOutcome)));
        if (!string.IsNullOrEmpty(StoryProgress.LastLogDate)) v.AddChild(UiStyle.MakeLabel(StoryProgress.LastLogDate, 14, UiStyle.Muted));
        // Body and reward scroll when the log is long (small windows).
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 300), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 10);
        // Right margin keeps the values clear of the scroll bar.
        var pad = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        pad.AddThemeConstantOverride("margin_right", 16);
        pad.AddChild(body);
        scroll.AddChild(pad);
        v.AddChild(scroll);
        var summary = new StatList { FontSize = 16 };
        summary.SetRows(StatList.Parse(StoryProgress.LastLogBody));
        body.AddChild(summary);
        if (!string.IsNullOrEmpty(StoryProgress.LastLogReward))
        {
            var reward = new StatList { FontSize = 16 };
            reward.SetRows(StatList.Parse(StoryProgress.LastLogReward, UiStyle.Good));
            body.AddChild(reward);
        }
        v.AddChild(UiStyle.MakeButton("Close", CloseLog, true));
        _logOverlay = layer;
    }

    private void CloseLog()
    {
        _logOverlay?.QueueFree();
        _logOverlay = null;
    }

    // ------------------------------------------------------------------ helpers

    private static Godot.Control Badge(string caption, string value)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.PanelColor, 10, 10, UiStyle.PanelBorder));
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 8);
        h.AddChild(UiStyle.MakeLabel(caption, 14, UiStyle.Muted));
        h.AddChild(UiStyle.MakeLabel(value, 18));
        panel.AddChild(h);
        return panel;
    }

    private static Color OutcomeColor(int outcome) => outcome switch
    {
        0 => UiStyle.Good,
        2 => UiStyle.Warn,
        _ => UiStyle.Bad,
    };
}
