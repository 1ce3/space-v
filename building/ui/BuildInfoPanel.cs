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

    private Label _weight;
    private Label _mass;
    private Label _mode;
    private Label _slots;
    private RichTextLabel _errors;
    private Button _launch;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Building, nameof(Building))) return;
        if (!NodeGuard.Require(this, Root, nameof(Root))) return;
        BuildLayout();
        Building.DesignChanged += Refresh;
        Building.ConfirmRejected += OnConfirmRejected;
        if (Cursor != null) Cursor.DecouplerModeChanged += _ => RefreshMode();
        Callable.From(Refresh).CallDeferred();
    }

    private void BuildLayout()
    {
        _weight = new Label();
        _mass = new Label();
        _mode = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _slots = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
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

        _launch = AddButton("Phóng", () => Building.ConfirmDesign());
        AddButton("Nghiên cứu", () => Root.RequestResearch());
        AddButton("Xoá hết", () => Building.ClearAll());
        AddChild(new Label
        {
            Text = "Chuột trái: đặt   Chuột phải: xoá\nTab: chế độ decoupler   Esc: bỏ chọn\nLăn chuột: zoom",
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

        _slots.Text = DescribeSlots();
        RefreshMode();

        List<string> errors = Building.Validate();
        ShowErrors(errors);
        _launch.Disabled = errors.Count > 0;
    }

    private void RefreshMode()
    {
        _mode.Text = Cursor != null && Cursor.DecouplerMode ? "Chế độ decoupler: trỏ vào cạnh giữa hai part" : "";
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

    private string DescribeSlots()
    {
        CommandPodPart pod = Building.GetPod();
        CommandPodPartDefinition def = pod?.PodDefinition;
        if (def == null) return "(chưa có pod)";
        var sb = new StringBuilder();
        foreach (ModuleSlotType type in System.Enum.GetValues<ModuleSlotType>())
        {
            int count = def.GetSlotCount(type);
            for (int i = 0; i < count; i++)
            {
                string name = Building.InstalledModules.TryGetValue((type, i), out ModuleDefinition m)
                    ? (string.IsNullOrEmpty(m.DisplayName) ? m.ModelId : m.DisplayName)
                    : "trống";
                sb.Append($"{type} {i + 1}: {name}\n");
            }
        }
        // TODO(A7): click a slot to install / remove a module.
        return sb.Length > 0 ? sb.ToString().TrimEnd() : "(pod không có slot)";
    }
}
