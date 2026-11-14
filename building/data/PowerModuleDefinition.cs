using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Power device: battery, solar array or radioisotope generator.</summary>
[GlobalClass]
public partial class PowerModuleDefinition : ModuleDefinition
{
    /// <summary>Which device this is.</summary>
    [ExportGroup("Power")]
    [Export] public PowerDeviceKind Kind { get; set; } = PowerDeviceKind.Battery;

    /// <summary>Output at nominal conditions, in W (solar arrays: at 1361 W/m^2 flux).</summary>
    [Export] public float NominalOutputW { get; set; }

    /// <summary>Stored energy when full, in Wh.</summary>
    [Export] public float EnergyStorageWh { get; set; }

    /// <summary>Waste heat released into the pod, in W.</summary>
    [Export] public float WasteHeatW { get; set; }

    /// <summary>Fraction of output lost per year of mission time.</summary>
    [Export] public float DegradationRatePerYear { get; set; }

    /// <summary>Sets the slot type to Power.</summary>
    public PowerModuleDefinition() => SlotType = ModuleSlotType.Power;
}
