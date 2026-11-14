using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Thermal control device: passive insulation, active radiator or radioisotope heater.</summary>
[GlobalClass]
public partial class ThermalModuleDefinition : ModuleDefinition
{
    /// <summary>Which device this is.</summary>
    [ExportGroup("Thermal")]
    [Export] public ThermalDeviceKind Kind { get; set; } = ThermalDeviceKind.PassiveInsulation;

    /// <summary>Thermal conductivity of the module, in W/(m*K).</summary>
    [Export] public float ThermalConductivity { get; set; }

    /// <summary>Largest heat the module can reject, in W.</summary>
    [Export] public float MaxHeatRejectionW { get; set; }

    /// <summary>Fraction of incoming radiation reflected (0..1).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float ReflectiveEfficiency { get; set; }

    /// <summary>Constant heat output, in W.</summary>
    [Export] public float HeaterOutputW { get; set; }

    /// <summary>Sets the slot type to Thermal.</summary>
    public ThermalModuleDefinition() => SlotType = ModuleSlotType.Thermal;
}
