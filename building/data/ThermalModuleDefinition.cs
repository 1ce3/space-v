using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Thermal device: insulation, radiator or radioisotope heater.</summary>
[GlobalClass]
public partial class ThermalModuleDefinition : ModuleDefinition
{
    [ExportGroup("Thermal")]
    [Export] public ThermalDeviceKind Kind { get; set; } = ThermalDeviceKind.PassiveInsulation;
    [Export] public float ThermalConductivity { get; set; } = 0f;
    /// <summary>Radiator capacity when extended (W).</summary>
    [Export] public float MaxHeatRejectionW { get; set; } = 0f;
    /// <summary>Insulation: fraction of radiation blocked (0..1).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float ReflectiveEfficiency { get; set; } = 0f;
    /// <summary>RHU constant heat (W).</summary>
    [Export] public float HeaterOutputW { get; set; } = 0f;

    public ThermalModuleDefinition()
    {
        SlotType = ModuleSlotType.Thermal;
    }

    public override string GetFormattedTooltip() => base.GetFormattedTooltip() + $"\n{Kind}" + (ReflectiveEfficiency > 0f ? $", reflects {N(ReflectiveEfficiency * 100f, "0")} %" : "") + (MaxHeatRejectionW > 0f ? $", rejects {N(MaxHeatRejectionW, "0")} W" : "") + (HeaterOutputW > 0f ? $", heats {N(HeaterOutputW, "0")} W" : "");
}
