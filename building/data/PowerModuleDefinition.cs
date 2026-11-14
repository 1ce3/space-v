using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Power device: battery, solar array or RTG.</summary>
[GlobalClass]
public partial class PowerModuleDefinition : ModuleDefinition
{
    [ExportGroup("Power")]
    [Export] public PowerDeviceKind Kind { get; set; } = PowerDeviceKind.Battery;
    /// <summary>Output at 1361 W/m^2 (solar) or at launch (RTG) (W).</summary>
    [Export] public float NominalOutputW { get; set; } = 0f;
    /// <summary>Battery capacity (Wh).</summary>
    [Export] public float EnergyStorageWh { get; set; } = 0f;
    /// <summary>Heat released (W).</summary>
    [Export] public float WasteHeatW { get; set; } = 0f;
    /// <summary>RTG output loss per year (fraction).</summary>
    [Export] public float DegradationRatePerYear { get; set; } = 0f;

    public PowerModuleDefinition()
    {
        SlotType = ModuleSlotType.Power;
    }
}
