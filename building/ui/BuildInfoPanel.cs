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

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Building, nameof(Building))) return;
        if (!NodeGuard.Require(this, Root, nameof(Root))) return;
        BuildLayout();
        Building.DesignChanged += Refresh;
        Building.ConfirmRejected += OnConfirmRejected;
        Building.WindTunnelUpdated += ShowWindTunnelReport;
        if (Cursor != null) Cursor.DecouplerModeChanged += _ => RefreshMode();
        Callable.From(Refresh).CallDeferred();
    }

    private void BuildLayout()
    {
        _weight = new Label();
        _mass = new Label();
        _mode = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _gridLabel = new Label();
        _grid = new PodInventoryGrid { Building = Building, CellPixels = InventoryCellPixels, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        _palette = new HFlowContainer();
        _errors = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(_weight);
        AddChild(_mass);
        AddChild(_mode);
        AddChild(new HSeparator());
        AddChild(_gridLabel);
        AddChild(_grid);
        AddChild(_palette);
        AddChild(new HSeparator());
        AddChild(new Label { Text = "Kiểm tra thiết kế" });
        AddChild(_errors);

        AddChild(new HSeparator());
        AddButton("Wind tunnel", () => Building.ShowWindTunnel());
        _windTunnel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_windTunnel);

        _launch = AddButton("Phóng", () => Building.ConfirmDesign());
        AddButton("Nghiên cứu", () => Root.RequestResearch());
        AddButton("Xoá hết", () => Building.ClearAll());
        AddChild(new Label
        {
            Text = "Chuột trái: đặt   Chuột phải: xoá\nTab: chế độ decoupler   Esc: bỏ chọn\nLăn chuột: zoom\nKhông chọn gì: kéo part để di chuyển, để chuột trên part để xem thông số",
            Modulate = new Color(1f, 1f, 1f, 0.6f),
        });
    }

    private Button AddButton(string text, System.Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, ButtonMinHeight),
            // No keyboard focus: Tab/Esc must reach BuildCursor, not UI focus navigation.
            FocusMode = FocusModeEnum.None,
        };
        button.Pressed += onPressed;
        AddChild(button);
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
        _mass.Text = $"Khối lượng: {mp.TotalMass:0} kg (chưa tính payload)";

        RebuildSlots();
        RefreshMode();
        if (_windTunnel != null) _windTunnel.Text = ""; // stale after any change

        List<string> errors = Building.Validate();
        ShowErrors(errors);
        _launch.Disabled = errors.Count > 0;
    }

    private void RefreshMode()
    {
        _mode.Text = Cursor != null && Cursor.DecouplerMode ? "Chế độ decoupler: trỏ vào cạnh giữa hai part" : "";
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
    }

    private void OnConfirmRejected(string[] errors) => ShowErrors(new List<string>(errors));

    private void ShowErrors(List<string> errors)
    {
        if (errors.Count == 0)
        {
            _errors.Text = $"[color=#{OkColor.ToHtml(false)}]Hợp lệ, sẵn sàng phóng[/color]";
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
        ModuleSlotType.Thermal => "Nhiệt",
        ModuleSlotType.Power => "Nguồn",
        ModuleSlotType.Sensor => "Cảm biến",
        ModuleSlotType.Comms => "Liên lạc",
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
            _gridLabel.Text = "Túi đồ khoang lái: (chưa có pod)";
            return;
        }

        Vector2I size = def.InventorySize;
        int used = 0;
        List<PodInventory.Item> occupied = Building.GetOccupiedCells();
        foreach (PodInventory.Item item in occupied) used += item.Size.X * item.Size.Y;
        _gridLabel.Text = $"Túi đồ khoang lái {size.X} x {size.Y}: dùng {used}/{size.X * size.Y} ô";
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
        if (_palette.GetChildCount() == 0) _palette.AddChild(new Label { Text = "(chưa mở khoá module)" });
    }
}
