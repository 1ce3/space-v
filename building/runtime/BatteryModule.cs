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

    /// <summary>Charge left, 0..1.</summary>
    public float GetChargeFraction()
    {
        float capacity = PowerDefinition?.EnergyStorageWh ?? 0f;
        return capacity > 0f ? StoredEnergyWh / capacity : 0f;
    }

    /// <summary>Removes up to wattHours; returns what was actually drawn (Wh). Negative charges.</summary>
    public float Draw(float wattHours)
    {
        if (State == ComponentState.Broken) return 0f;
        float capacity = PowerDefinition?.EnergyStorageWh ?? 0f;
        float before = StoredEnergyWh;
        StoredEnergyWh = Mathf.Clamp(StoredEnergyWh - wattHours, 0f, capacity);
        return before - StoredEnergyWh;
    }
}
