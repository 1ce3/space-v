// AI-assisted: parts of this file were written with Claude (Anthropic); reviewed by The Neptunians.
using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Left panel: unlocked parts grouped as Pod, Tank, Engine, Booster, EDL, Khác (A_BUILDING.md 5.5).</summary>
public partial class BuildPalettePanel : VBoxContainer
{
    [Export] public BuildingSystem Building { get; set; }
    [Export] public BuildCursor Cursor { get; set; }
    [Export] public int ButtonMinHeight { get; set; } = 34;
    [Export] public int IconMaxWidth { get; set; } = 28;
    [Export] public Color HeaderColor { get; set; } = new(0.7f, 0.85f, 1f);
    [Export] public Color SelectedColor { get; set; } = new(1f, 0.85f, 0.3f);
    /// <summary>Width of the vertical category tabs (sidebar dock).</summary>
    [Export] public int SidebarWidth { get; set; } = 78;

    /// <summary>Palette groups in display order.</summary>
    public static readonly string[] GroupOrder = { "Pod", "Tank", "Engine", "Booster", "EDL", "Other" };

    private readonly Dictionary<string, Button> _buttons = new();

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Building, nameof(Building))) return;
        if (!NodeGuard.Require(this, Cursor, nameof(Cursor))) return;
        Cursor.SelectionChanged += OnSelectionChanged;
        Cursor.DecouplerModeChanged += _ => OnSelectionChanged(Cursor.SelectedModelId ?? "");
        Callable.From(Rebuild).CallDeferred();
    }

    /// <summary>Palette group of a part definition.</summary>
    public static string GetGroup(PartDefinition def) => def switch
    {
        DecouplerPartDefinition => "Other",
        _ => def.PartGroup switch
        {
            PartGroup.CommandPod => "Pod",
            PartGroup.CoreTank => "Tank",
            PartGroup.LiquidEngine => "Engine",
            PartGroup.SolidBooster => "Booster",
            PartGroup.Fairing or PartGroup.Parachute or PartGroup.HeatShield => "EDL",
            _ => "Other",
        },
    };

    /// <summary>Category shown in the list (UI redesign: one tab per group).</summary>
    private string _activeGroup;

    /// <summary>Recreates the back button, the category tabs and the part list of the active tab.</summary>
    public void Rebuild()
    {
        foreach (Node child in GetChildren()) child.QueueFree();
        _buttons.Clear();

        var back = new Button { Text = "◀  Back", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, ButtonMinHeight), Alignment = HorizontalAlignment.Left };
        back.Pressed += () => (Owner as BuildScreenRoot)?.RequestHome();
        AddChild(back);

        var byGroup = new Dictionary<string, List<PartDefinition>>();
        foreach (PartDefinition def in Building.GetAvailableParts())
        {
            string group = GetGroup(def);
            if (!byGroup.TryGetValue(group, out List<PartDefinition> list)) byGroup[group] = list = new List<PartDefinition>();
            list.Add(def);
        }
        if (_activeGroup == null || !byGroup.ContainsKey(_activeGroup))
        {
            _activeGroup = null;
            foreach (string g in GroupOrder) if (byGroup.ContainsKey(g)) { _activeGroup = g; break; }
        }

        // Vertical category tabs on the left (a sidebar dock), the parts of the active category beside them.
        var body = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 6);
        AddChild(body);

        var dock = new PanelContainer { CustomMinimumSize = new Vector2(SidebarWidth, 0), SizeFlagsVertical = SizeFlags.ExpandFill };
        dock.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.25f), CornerRadiusTopLeft = 6, CornerRadiusBottomLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomRight = 6 });
        body.AddChild(dock);
        var sidebar = new VBoxContainer();
        sidebar.AddThemeConstantOverride("separation", 2);
        dock.AddChild(sidebar);
        foreach (string group in GroupOrder)
        {
            if (!byGroup.ContainsKey(group)) continue;
            string g = group;
            bool active = g == _activeGroup;
            var tab = new Button
            {
                Text = $"{GroupTitle(g)}\n{byGroup[g].Count}", ToggleMode = true, FocusMode = FocusModeEnum.None,
                ButtonPressed = active, CustomMinimumSize = new Vector2(0, 52), Flat = !active,
            };
            tab.AddThemeFontSizeOverride("font_size", 13);
            if (active)
            {
                var style = new StyleBoxFlat
                {
                    BgColor = new Color(SelectedColor, 0.18f), BorderColor = SelectedColor, BorderWidthLeft = 3,
                    CornerRadiusTopRight = 4, CornerRadiusBottomRight = 4,
                };
                tab.AddThemeStyleboxOverride("pressed", style);
                tab.AddThemeStyleboxOverride("hover_pressed", style);
                tab.AddThemeColorOverride("font_pressed_color", SelectedColor);
                tab.AddThemeColorOverride("font_hover_pressed_color", SelectedColor);
            }
            tab.Pressed += () => { _activeGroup = g; Callable.From(Rebuild).CallDeferred(); };
            sidebar.AddChild(tab);
        }

        var partList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        partList.AddThemeConstantOverride("separation", 2);
        body.AddChild(partList);
        if (_activeGroup == null) return;

        var header = new Label { Text = GroupTitle(_activeGroup) };
        header.AddThemeColorOverride("font_color", HeaderColor);
        partList.AddChild(header);
        foreach (PartDefinition def in byGroup[_activeGroup])
        {
            var button = new Button
            {
                Text = $"{DisplayNameOf(def)}  ({def.BuildWeight})",
                Icon = def.Icon,
                ExpandIcon = true,
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, ButtonMinHeight),
                TooltipText = def.GetFormattedTooltip(),
                ToggleMode = true,
                ClipText = true,
                // No keyboard focus: Tab/Esc must reach BuildCursor, not UI focus navigation.
                FocusMode = FocusModeEnum.None,
            };
            button.AddThemeConstantOverride("icon_max_width", IconMaxWidth);
            string id = def.ModelId;
            button.Pressed += () => Cursor.Select(id);
            partList.AddChild(button);
            _buttons[id] = button;
        }
        OnSelectionChanged(Cursor.SelectedModelId ?? "");
        // Modules are not in the palette: they are installed from the pod inventory in BuildInfoPanel (A11).
    }

    /// <summary>Tab title of a palette group.</summary>
    public static string GroupTitle(string group) => group == "Other" ? "Other" : group;

    private void OnSelectionChanged(string modelId)
    {
        foreach ((string id, Button button) in _buttons)
        {
            bool selected = id == modelId || (Cursor.DecouplerMode && PartCatalog.Instance?.GetDefinition(id) is DecouplerPartDefinition);
            button.SetPressedNoSignal(selected);
            button.Modulate = selected ? SelectedColor : Colors.White;
        }
    }

    private static string DisplayNameOf(ComponentDefinition def) => string.IsNullOrEmpty(def.DisplayName) ? def.ModelId : def.DisplayName;
}
