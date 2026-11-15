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

    public override string GetFormattedTooltip() => base.GetFormattedTooltip() + $"\n{Kind}" + (NominalOutputW > 0f ? $", {N(NominalOutputW, "0")} W" : "") + (EnergyStorageWh > 0f ? $", {N(EnergyStorageWh, "0")} Wh" : "") + (WasteHeatW > 0f ? $", {N(WasteHeatW, "0")} W waste heat" : "");
}
