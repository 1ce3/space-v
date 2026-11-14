using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Battery.</summary>
public class BatteryModule : PowerModule
{
    /// <summary>Energy left (Wh).</summary>
    public float StoredEnergyWh { get; set; }

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        StoredEnergyWh = PowerDefinition?.EnergyStorageWh ?? 0f;
    }

    /// <summary>Removes up to wattHours; returns what was actually drawn (Wh). Negative charges.</summary>
    public float Draw(float wattHours)
    {
        // TODO(A1): clamp StoredEnergyWh to 0..EnergyStorageWh.
        return 0f;
    }
}
