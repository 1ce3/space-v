using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Phase 2 (PHASE2_PLAN.md 2.1): sensor or comms instrument placed in the pod inventory grid.</summary>
[GlobalClass]
public partial class InstrumentModuleDefinition : ModuleDefinition
{
    [ExportGroup("Instrument")]
    [Export] public InstrumentKind Kind { get; set; } = InstrumentKind.Comms;
    /// <summary>Comms: seconds of warning before a meteor field.</summary>
    [Export] public float WarningLeadSeconds { get; set; }
    /// <summary>RadiationSensor: days of warning before a solar flare.</summary>
    [Export] public float FlareWarningDays { get; set; }
    /// <summary>FuelSensor / PressureSensor: 0..1 cut of the leak rate.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float LeakReduction { get; set; }
    /// <summary>Comms: with RCS on board, the player may let the ship dodge meteors by itself.</summary>
    [Export] public bool AllowsAutoDodge { get; set; }

    public InstrumentModuleDefinition()
    {
        SlotType = ModuleSlotType.Comms;
    }

    public override string GetFormattedTooltip()
    {
        string t = base.GetFormattedTooltip();
        if (WarningLeadSeconds > 0f) t += $"\nCảnh báo thiên thạch trước {WarningLeadSeconds:0} s";
        if (AllowsAutoDodge) t += "\nTự né (cần RCS)";
        if (FlareWarningDays > 0f) t += $"\nBáo bão mặt trời trước {FlareWarningDays:0} ngày";
        if (LeakReduction > 0f) t += $"\nGiảm rò rỉ {LeakReduction:P0}";
        return t;
    }
}
