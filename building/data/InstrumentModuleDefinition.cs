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
        if (WarningLeadSeconds > 0f) t += $"\nMeteor warning {N(WarningLeadSeconds, "0")} s ahead";
        if (AllowsAutoDodge) t += "\nAuto-dodge (needs RCS)";
        if (FlareWarningDays > 0f) t += $"\nSolar flare warning {N(FlareWarningDays, "0")} day(s) ahead";
        if (LeakReduction > 0f) t += $"\nLeak rate -{N(LeakReduction * 100f, "0")} %";
        return t;
    }
}
