// AI-assisted: parts of this file were written with Claude (Anthropic); reviewed by The Neptunians.
using System.Collections.Generic;
using System.Text;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Right panel: weight, mass, CoM, pod slots, validation errors and the Phóng / Nghiên cứu / Xoá hết buttons.</summary>
public partial class BuildInfoPanel : VBoxContainer
{
    [Export] public BuildingSystem Building { get; set; }
    [Export] public BuildScreenRoot Root { get; set; }
    [Export] public BuildCursor Cursor { get; set; }
    [Export] public Color ErrorColor { get; set; } = new(1f, 0.45f, 0.4f);
    [Export] public Color OkColor { get; set; } = new(0.5f, 1f, 0.55f);
    [Export] public Color OverWeightColor { get; set; } = new(1f, 0.6f, 0.2f);
    [Export] public int ButtonMinHeight { get; set; } = 40;
    [Export] public int SlotButtonMinHeight { get; set; } = 28;
    [Export] public Color EmptySlotColor { get; set; } = new(1f, 1f, 1f, 0.55f);
    [Export] public Color FilledSlotColor { get; set; } = new(0.6f, 0.85f, 1f);
    /// <summary>Pixel size of one pod inventory cell.</summary>
    [Export] public int InventoryCellPixels { get; set; } = 46;

    private Label _weight;
    private Label _mass;
    private Label _mode;
    private Label _gridLabel;
    private PodInventoryGrid _grid;
    private HFlowContainer _palette;
    private RichTextLabel _errors;
    private Button _launch;
    private Label _windTunnel;
    private OptionButton _materialPick;
    private Button _brush;
    private Label _materialInfo;
    private Label _status;
    /// <summary>General info and wind tunnel result as name (left) / value (right) rows.</summary>
    private StatList _general;
    private StatList _aeroStats;
    private Label _podName;
    /// <summary>Width of the right panel in build_screen.tscn; the material panel sits left of it.</summary>
    [Export] public float RightPanelWidth { get; set; } = 320f;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Building, nameof(Building))) return;
        if (!NodeGuard.Require(this, Root, nameof(Root))) return;
        BuildLayout();
        Building.DesignChanged += Refresh;
        Building.ConfirmRejected += OnConfirmRejected;
        Building.WindTunnelUpdated += ShowWindTunnelReport;
        if (Cursor != null)
        {
            Cursor.DecouplerModeChanged += _ => RefreshMode();
            Cursor.MaterialBrushChanged += _ => RefreshMode();
        }
        Callable.From(Refresh).CallDeferred();
    }

    private void BuildLayout()
    {
        Label Wrap(Label l) { l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.SizeFlagsHorizontal = SizeFlags.ExpandFill; return l; }
        _weight = Wrap(new Label());
        _mass = Wrap(new Label());
        _mode = Wrap(new Label());
        _status = Wrap(new Label());
        _podName = Wrap(new Label());
        _podName.AddThemeFontSizeOverride("font_size", 18);
        _gridLabel = Wrap(new Label());
        _grid = new PodInventoryGrid { Building = Building, CellPixels = InventoryCellPixels, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        _palette = new HFlowContainer();
        _errors = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };

        // UI redesign: four tabs (Instruments, Material, Aero test, Launch), general info under them.
        var tabBar = new HBoxContainer();
        tabBar.AddThemeConstantOverride("separation", 2);
        AddChild(tabBar);
        AddChild(new HSeparator());

        VBoxContainer instruments = AddTab(tabBar, "Instruments");
        instruments.AddChild(_podName);
        instruments.AddChild(_gridLabel);
        instruments.AddChild(_grid);
        instruments.AddChild(_palette);

        // Material selector: its own tab, so it no longer covers the build grid.
        BuildMaterialSection(AddTab(tabBar, "Material"));

        VBoxContainer aero = AddTab(tabBar, "Aero test");
        aero.AddChild(Wrap(new Label { Text = "Blows air over the rocket at the test speed and angle: drag, side force and whether the nose turns back into the flow.", Modulate = new Color(1f, 1f, 1f, 0.6f) }));
        AddButton(aero, "Run wind tunnel", () => Building.ShowWindTunnel());
        _windTunnel = Wrap(new Label());
        aero.AddChild(_windTunnel);
        _aeroStats = new StatList();
        aero.AddChild(_aeroStats);

        VBoxContainer launch = AddTab(tabBar, "Launch");
        launch.AddChild(Wrap(new Label { Text = "Check result" }));
        launch.AddChild(_errors);
        _launch = AddButton(launch, "Launch", () => Building.ConfirmDesign());
        _launch.AddThemeFontSizeOverride("font_size", 20);
        _launch.CustomMinimumSize = new Vector2(0, ButtonMinHeight + 10);
        var actions = new HFlowContainer();
        launch.AddChild(actions);
        AddButton(actions, "Research", () => Root.RequestResearch());
        AddButton(actions, "Clear all", () => Building.ClearAll());

        // General info: always visible.
        AddChild(new HSeparator());
        var info = Wrap(new Label { Text = "General info" });
        info.AddThemeColorOverride("font_color", new Color(0.7f, 0.85f, 1f));
        AddChild(info);
        _general = new StatList();
        AddChild(_general);
        AddChild(_mode);

        VBoxContainer help = AddSection("Keys", false);
        help.AddChild(Wrap(new Label
        {
            Text = "Left click: place   Right click: remove\nTab: decoupler mode   Esc: deselect\nMouse wheel: zoom\nNothing selected: drag a part to move it, hover a part for its stats",
            Modulate = new Color(1f, 1f, 1f, 0.6f),
        }));

        SelectTab(0);
    }

    private readonly System.Collections.Generic.List<(Button Tab, VBoxContainer Content)> _tabs = new();

    /// <summary>Adds a tab button to the bar and an (initially hidden) content box under it.</summary>
    private VBoxContainer AddTab(HBoxContainer bar, string title)
    {
        int index = _tabs.Count;
        var tab = new Button
        {
            Text = title, ToggleMode = true, FocusMode = FocusModeEnum.None,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 36),
        };
        tab.AddThemeFontSizeOverride("font_size", 13);
        // Four tabs share the panel width: thin side padding so "Instruments" is not cut off.
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
        {
            if (tab.GetThemeStylebox(state) is StyleBox box && box.Duplicate() is StyleBox thin)
            {
                thin.ContentMarginLeft = 4;
                thin.ContentMarginRight = 4;
                tab.AddThemeStyleboxOverride(state, thin);
            }
        }
        tab.Pressed += () => SelectTab(index);
        bar.AddChild(tab);
        var content = new VBoxContainer { Visible = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 8);
        AddChild(content);
        _tabs.Add((tab, content));
        return content;
    }

    /// <summary>Shows one tab of the right panel.</summary>
    public void SelectTab(int index)
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Tab.SetPressedNoSignal(i == index);
            _tabs[i].Tab.Modulate = i == index ? new Color(1f, 0.9f, 0.55f) : Colors.White;
            _tabs[i].Content.Visible = i == index;
        }
    }

    /// <summary>Collapsible section: a header button (▼ / ►) over a content box. Returns the content box.</summary>
    private VBoxContainer AddSection(string title, bool expanded)
    {
        AddChild(new HSeparator());
        var header = new Button
        {
            Flat = true, FocusMode = FocusModeEnum.None, Alignment = HorizontalAlignment.Left,
            ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var content = new VBoxContainer { Visible = expanded, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        void Sync() => header.Text = (content.Visible ? "▼  " : "►  ") + title;
        header.Pressed += () => { content.Visible = !content.Visible; Sync(); };
        Sync();
        AddChild(header);
        AddChild(content);
        return content;
    }

    // ------------------------------------------------------------------ A12: hull materials

    private void BuildMaterialSection(VBoxContainer box)
    {
        box.AddChild(new Label { Text = "For tanks, pods and decoupler shells", AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1f, 1f, 1f, 0.6f) });
        _materialPick = new OptionButton { FocusMode = FocusModeEnum.None, ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _materialPick.ItemSelected += _ => OnMaterialPicked();
        box.AddChild(_materialPick);
        _materialInfo = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1f, 1f, 1f, 0.75f) };
        box.AddChild(_materialInfo);
        var row = new HFlowContainer();
        _brush = new Button { Text = "Paint brush", ToggleMode = true, FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _brush.Toggled += on => Cursor?.SetMaterialBrush(on ? SelectedMaterialId() : null);
        var all = new Button { Text = "Apply to whole rocket", FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        all.Pressed += () => { if (SelectedMaterialId() is string id) Building.ApplyMaterialToAll(id); };
        row.AddChild(_brush);
        row.AddChild(all);
        box.AddChild(row);
        RefreshMaterials();
    }

    private string SelectedMaterialId() =>
        _materialPick == null || _materialPick.Selected < 0 ? null : _materialPick.GetItemMetadata(_materialPick.Selected).AsString();

    /// <summary>Unlocked materials only; keeps the current pick when it is still there.</summary>
    private void RefreshMaterials()
    {
        if (_materialPick == null) return;
        string keep = SelectedMaterialId();
        _materialPick.Clear();
        List<MaterialDefinition> list = Building.Catalog?.GetUnlockedMaterials() ?? new List<MaterialDefinition>();
        for (int i = 0; i < list.Count; i++)
        {
            MaterialDefinition m = list[i];
            _materialPick.AddItem($"{m.DisplayName} (T{m.Tier}, +{m.BuildWeightCost} W)", i);
            _materialPick.SetItemMetadata(i, m.MaterialId);
            if (m.MaterialId == keep) _materialPick.Select(i);
        }
        if (_materialPick.Selected < 0 && list.Count > 0) _materialPick.Select(0);
        UpdateMaterialInfo();
    }

    private void OnMaterialPicked()
    {
        UpdateMaterialInfo();
        if (_brush.ButtonPressed) Cursor?.SetMaterialBrush(SelectedMaterialId());
    }

    private void UpdateMaterialInfo()
    {
        MaterialDefinition m = SelectedMaterialId() is string id ? Building.Catalog?.GetMaterial(id) : null;
        _materialInfo.Text = m == null ? "(no material)" :
            $"Mass x{m.MassMultiplier:0.##}, heat {m.MaxTempBonusK:+0;-0;0} K, radiation shielding {m.RadiationShielding:P0}\n" +
            $"Impact x{m.ImpactToleranceMultiplier:0.##}, strength x{m.StrengthMultiplier:0.##}";
    }

    private Button AddButton(Godot.Control parent, string text, System.Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, ButtonMinHeight),
            // No keyboard focus: Tab/Esc must reach BuildCursor, not UI focus navigation.
            FocusMode = FocusModeEnum.None,
        };
        button.Pressed += onPressed;
        parent.AddChild(button);
        return button;
    }

    /// <summary>Re-reads the design and validation.</summary>
    public void Refresh()
    {
        BuildBudget budget = Building.Budget;
        string max = budget.MaxWeight >= RocketAssembler.NoWeightLimit ? "∞" : budget.MaxWeight.ToString();
        _weight.Text = $"Weight: {budget.CurrentWeight} / {max}";
        _weight.Modulate = budget.CurrentWeight > budget.MaxWeight ? OverWeightColor : Colors.White;

        MassProperties mp = Building.GetPreviewMass();
        _mass.Text = $"Mass: {mp.TotalMass:0} kg (without payload)";

        RebuildSlots();
        RefreshMaterials();
        RefreshMode();
        if (_windTunnel != null) _windTunnel.Text = ""; // stale after any change
        _aeroStats?.SetRows(System.Array.Empty<StatList.Row>());

        List<string> errors = Building.Validate();
        ShowErrors(errors);
        _launch.Disabled = errors.Count > 0;
        _status.Text = errors.Count == 0 ? "Ready to launch" : $"{errors.Count} problem(s): see the Launch tab";
        _status.Modulate = errors.Count == 0 ? OkColor : ErrorColor;
        _general.SetRows(new[]
        {
            new StatList.Row("Weight", $"{budget.CurrentWeight} / {max}", budget.CurrentWeight > budget.MaxWeight ? OverWeightColor : (Color?)null),
            new StatList.Row("Mass", $"{mp.TotalMass:0} kg"),
            new StatList.Row("Parts", $"{Building.Grid.Parts.Count}"),
            new StatList.Row("Status", errors.Count == 0 ? "Ready to launch" : $"{errors.Count} problem(s)", errors.Count == 0 ? OkColor : ErrorColor),
        });
    }

    private void RefreshMode()
    {
        string text = Cursor != null && Cursor.DecouplerMode ? "Decoupler mode: point at the edge between two parts" : "";
        if (Cursor?.MaterialBrushId != null)
            text += (text.Length > 0 ? "\n" : "") + (Cursor.DecouplerMode ? "Paint brush: click an edge with a decoupler shell" : "Paint brush: click a tank / pod (Tab: decoupler shell)");
        _mode.Text = text;
        if (_brush != null) _brush.SetPressedNoSignal(Cursor?.MaterialBrushId != null);
    }

    private void ShowWindTunnelReport()
    {
        AeroReport r = Building.LastAeroReport;
        if (Building.Grid.Parts.Count == 0)
        {
            _windTunnel.Text = "Nothing to test.";
            return;
        }
        _windTunnel.Text =
            $"{Building.WindTunnelSpeedMps:0} m/s at {Building.WindTunnelAltitudeM / 1000f:0} km, {Building.WindTunnelAngleDeg:0.#}°\n" +
            $"Drag {r.DragN / 1000f:0.0} kN, side force {r.LiftN / 1000f:0.0} kN\n" +
            (r.IsStable
                ? $"Stable (margin {r.StabilityMarginM:0.0} m): the nose turns back into the flow"
                : $"Unstable (margin {r.StabilityMarginM:0.0} m): keep SAS on, add fins low or move mass up");
        _windTunnel.Modulate = r.IsStable ? OkColor : ErrorColor;
        _windTunnel.Text = r.IsStable
            ? "The nose turns back into the flow."
            : "Keep SAS on, add fins low or move mass up.";
        _aeroStats.SetRows(new[]
        {
            new StatList.Row("Air speed", $"{Building.WindTunnelSpeedMps:0} m/s"),
            new StatList.Row("Altitude", $"{Building.WindTunnelAltitudeM / 1000f:0} km"),
            new StatList.Row("Angle", $"{Building.WindTunnelAngleDeg:0.#}°"),
            new StatList.Row("Drag", $"{r.DragN / 1000f:0.0} kN"),
            new StatList.Row("Side force", $"{r.LiftN / 1000f:0.0} kN"),
            new StatList.Row("Stability margin", $"{r.StabilityMarginM:0.0} m"),
            new StatList.Row("Result", r.IsStable ? "Stable" : "Unstable", r.IsStable ? OkColor : ErrorColor),
        });
    }

    private void OnConfirmRejected(string[] errors) => ShowErrors(new List<string>(errors));

    private void ShowErrors(List<string> errors)
    {
        if (errors.Count == 0)
        {
            _errors.Text = $"[color=#{OkColor.ToHtml(false)}]Valid, ready to launch[/color]";
            return;
        }
        var sb = new StringBuilder();
        string color = ErrorColor.ToHtml(false);
        foreach (string e in errors) sb.Append($"[color=#{color}]• {e}[/color]\n");
        _errors.Text = sb.ToString();
    }

    private static string SlotTypeName(ModuleSlotType type) => type switch
    {
        ModuleSlotType.Avionics => "Avionics",
        ModuleSlotType.Thermal => "Thermal",
        ModuleSlotType.Power => "Power",
        ModuleSlotType.Sensor => "Sensors",
        ModuleSlotType.Comms => "Comms",
        _ => type.ToString(),
    };

    private static string NameOf(ComponentDefinition def) => string.IsNullOrEmpty(def.DisplayName) ? def.ModelId : def.DisplayName;

    /// <summary>A11: pod inventory grid (drag and drop) and the palette of unlocked modules under it.</summary>
    private void RebuildSlots()
    {
        foreach (Node child in _palette.GetChildren())
        {
            _palette.RemoveChild(child);
            child.QueueFree();
        }

        CommandPodPartDefinition def = Building.GetPod()?.PodDefinition;
        _grid.Visible = def != null;
        _palette.Visible = def != null;
        if (def == null)
        {
            _gridLabel.Text = "Pod inventory: (no pod)";
            _podName.Text = "No command pod";
            return;
        }

        _podName.Text = NameOf(def);
        Vector2I size = def.InventorySize;
        int used = 0;
        List<PodInventory.Item> occupied = Building.GetOccupiedCells();
        foreach (PodInventory.Item item in occupied) used += item.Size.X * item.Size.Y;
        _gridLabel.Text = $"Pod inventory {size.X} x {size.Y}: {used}/{size.X * size.Y} cells used";
        _grid.Refresh();

        foreach (ModuleDefinition module in Building.GetAvailableModules())
        {
            bool fits = PodInventory.FindFreeCell(size, occupied, PodInventory.SizeOf(module)) != null;
            bool affordable = module.BuildWeight <= Building.Budget.Remaining;
            var item = new ModulePaletteItem { CustomMinimumSize = new Vector2(0, SlotButtonMinHeight) };
            item.Setup(module, fits && affordable, InventoryCellPixels);
            string id = module.ModelId;
            item.Pressed += () => Building.InstallModule(id);
            _palette.AddChild(item);
        }
        if (_palette.GetChildCount() == 0) _palette.AddChild(new Label { Text = "(no modules unlocked)" });
    }
}
