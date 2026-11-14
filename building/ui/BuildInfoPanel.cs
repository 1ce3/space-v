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
    private ModuleSlotType _menuSlotType;
    private int _menuSlotIndex;
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
        _ => type.ToString(),
    };

    private static string NameOf(ComponentDefinition def) => string.IsNullOrEmpty(def.DisplayName) ? def.ModelId : def.DisplayName;

    /// <summary>One button per pod slot; clicking opens the module menu for that slot.</summary>
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

        int total = 0;
        foreach (ModuleSlotType type in System.Enum.GetValues<ModuleSlotType>())
        {
            int count = def.GetSlotCount(type);
            for (int i = 0; i < count; i++)
            {
                total++;
                bool filled = Building.InstalledModules.TryGetValue((type, i), out ModuleDefinition module);
                var button = new Button
                {
                    Text = $"{SlotTypeName(type)} {i + 1}: {(filled ? NameOf(module) : "trống")}",
                    Alignment = HorizontalAlignment.Left,
                    CustomMinimumSize = new Vector2(0, SlotButtonMinHeight),
                    FocusMode = FocusModeEnum.None,
                    TooltipText = filled ? module.GetFormattedTooltip() : "Bấm để gắn module",
                    Modulate = filled ? FilledSlotColor : EmptySlotColor,
                };
                ModuleSlotType slotType = type;
                int slotIndex = i;
                button.Pressed += () => OpenSlotMenu(slotType, slotIndex, button);
                _slots.AddChild(button);
            }
        }
        if (total == 0) _slots.AddChild(new Label { Text = "(pod không có slot)" });
    }

    private void OpenSlotMenu(ModuleSlotType type, int index, Godot.Control anchor)
    {
        _menuSlotType = type;
        _menuSlotIndex = index;
        _menuModules.Clear();
        _menuModules.AddRange(Building.GetAvailableModules(type));
        _slotMenu.Clear();

        Building.InstalledModules.TryGetValue((type, index), out ModuleDefinition installed);
        int freed = installed?.BuildWeight ?? 0;
        for (int i = 0; i < _menuModules.Count; i++)
        {
            ModuleDefinition m = _menuModules[i];
            _slotMenu.AddItem($"{NameOf(m)}  (weight {m.BuildWeight}, {m.DryMass:0} kg)", i);
            int idx = _slotMenu.GetItemIndex(i);
            _slotMenu.SetItemTooltip(idx, m.GetFormattedTooltip());
            bool affordable = m.BuildWeight - freed <= Building.Budget.Remaining;
            bool same = installed != null && installed.ModelId == m.ModelId;
            _slotMenu.SetItemDisabled(idx, !affordable || same);
        }
        if (_menuModules.Count == 0)
        {
            _slotMenu.AddItem("(chưa mở khoá module cho slot này)", NoModuleItemId);
            _slotMenu.SetItemDisabled(_slotMenu.ItemCount - 1, true);
        }
        if (installed != null)
        {
            _slotMenu.AddSeparator();
            _slotMenu.AddItem($"Gỡ {NameOf(installed)}", RemoveItemId);
        }

        var at = (Vector2I)(anchor.GlobalPosition + new Vector2(0f, anchor.Size.Y));
        _slotMenu.Popup(new Rect2I(at, new Vector2I((int)anchor.Size.X, 0)));
    }

    private void OnSlotMenuPressed(long id)
    {
        if (id == RemoveItemId)
        {
            Building.RemoveModule(_menuSlotType, _menuSlotIndex);
            return;
        }
        if (id < 0 || id >= _menuModules.Count) return; // includes NoModuleItemId
        Building.InstallModule(_menuModules[(int)id].ModelId, _menuSlotIndex);
    }
}
