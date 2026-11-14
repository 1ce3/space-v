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

    /// <summary>Recreates the buttons from GetAvailableParts().</summary>
    public void Rebuild()
    {
        foreach (Node child in GetChildren()) child.QueueFree();
        _buttons.Clear();

        var byGroup = new Dictionary<string, List<PartDefinition>>();
        foreach (PartDefinition def in Building.GetAvailableParts())
        {
            string group = GetGroup(def);
            if (!byGroup.TryGetValue(group, out List<PartDefinition> list)) byGroup[group] = list = new List<PartDefinition>();
            list.Add(def);
        }

        foreach (string group in GroupOrder)
        {
            if (!byGroup.TryGetValue(group, out List<PartDefinition> defs)) continue;
            var header = new Label { Text = group };
            header.AddThemeColorOverride("font_color", HeaderColor);
            AddChild(header);
            foreach (PartDefinition def in defs)
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
                    // No keyboard focus: Tab/Esc must reach BuildCursor, not UI focus navigation.
                    FocusMode = FocusModeEnum.None,
                };
                button.AddThemeConstantOverride("icon_max_width", IconMaxWidth);
                string id = def.ModelId;
                button.Pressed += () => Cursor.Select(id);
                AddChild(button);
                _buttons[id] = button;
            }
        }
        // Modules are not in the palette: they are installed from the pod slot list in BuildInfoPanel (A7).
    }

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
