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

    private Label _weight;
    private Label _mass;
    private Label _mode;
    private VBoxContainer _slots;
    private PopupMenu _slotMenu;
    private readonly List<ModuleDefinition> _menuModules = new();
    private const int RemoveItemId = 10000;
    private const int NoModuleItemId = 10001;
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
        _slots = new VBoxContainer();
        _slotMenu = new PopupMenu();
        _slotMenu.IdPressed += OnSlotMenuPressed;
        AddChild(_slotMenu);
        _errors = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(_weight);
        AddChild(_mass);
        AddChild(_mode);
        AddChild(new HSeparator());
        AddChild(new Label { Text = "Slot của pod" });
        AddChild(_slots);
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

    /// <summary>
    /// A10 interim view of the pod inventory grid: one button per installed module (click: remove) and an "add" button
    /// whose menu installs at the first free cell. A11 replaces this with the drag-and-drop grid.
    /// </summary>
    private void RebuildSlots()
    {
        foreach (Node child in _slots.GetChildren())
        {
            _slots.RemoveChild(child);
            child.QueueFree();
        }

        CommandPodPartDefinition def = Building.GetPod()?.PodDefinition;
        if (def == null)
        {
            _slots.AddChild(new Label { Text = "(chưa có pod)" });
            return;
        }

        Vector2I size = def.InventorySize;
        int used = 0;
        foreach (PodInventory.Item item in Building.GetOccupiedCells()) used += item.Size.X * item.Size.Y;
        _slots.AddChild(new Label { Text = $"Lưới {size.X} x {size.Y}: dùng {used}/{size.X * size.Y} ô" });

        foreach (KeyValuePair<Vector2I, ModuleDefinition> pair in Building.InstalledModules)
        {
            Vector2I cell = pair.Key;
            ModuleDefinition module = pair.Value;
            Vector2I s = PodInventory.SizeOf(module);
            var button = new Button
            {
                Text = $"{NameOf(module)}  [{s.X}x{s.Y}] ô ({cell.X}, {cell.Y})",
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, SlotButtonMinHeight),
                FocusMode = FocusModeEnum.None,
                TooltipText = module.GetFormattedTooltip() + "\nBấm để gỡ",
                Modulate = FilledSlotColor,
            };
            button.Pressed += () => Building.RemoveModule(cell);
            _slots.AddChild(button);
        }

        var add = new Button
        {
            Text = "+ Thêm module",
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0, SlotButtonMinHeight),
            FocusMode = FocusModeEnum.None,
            Modulate = EmptySlotColor,
        };
        add.Pressed += () => OpenAddMenu(add);
        _slots.AddChild(add);
    }

    private void OpenAddMenu(Godot.Control anchor)
    {
        _menuModules.Clear();
        _menuModules.AddRange(Building.GetAvailableModules());
        _slotMenu.Clear();
        var occupied = Building.GetOccupiedCells();
        for (int i = 0; i < _menuModules.Count; i++)
        {
            ModuleDefinition m = _menuModules[i];
            Vector2I s = PodInventory.SizeOf(m);
            _slotMenu.AddItem($"{NameOf(m)}  [{s.X}x{s.Y}]  (weight {m.BuildWeight}, {m.DryMass:0} kg)", i);
            int idx = _slotMenu.GetItemIndex(i);
            _slotMenu.SetItemTooltip(idx, m.GetFormattedTooltip());
            bool fits = PodInventory.FindFreeCell(Building.InventorySize, occupied, s) != null;
            _slotMenu.SetItemDisabled(idx, !fits || m.BuildWeight > Building.Budget.Remaining);
        }
        if (_menuModules.Count == 0)
        {
            _slotMenu.AddItem("(chưa mở khoá module)", NoModuleItemId);
            _slotMenu.SetItemDisabled(_slotMenu.ItemCount - 1, true);
        }
        var at = (Vector2I)(anchor.GlobalPosition + new Vector2(0f, anchor.Size.Y));
        _slotMenu.Popup(new Rect2I(at, new Vector2I((int)anchor.Size.X, 0)));
    }

    private void OnSlotMenuPressed(long id)
    {
        if (id < 0 || id >= _menuModules.Count) return; // includes NoModuleItemId
        Building.InstallModule(_menuModules[(int)id].ModelId);
    }
}
