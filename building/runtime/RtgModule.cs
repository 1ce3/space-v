using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Radioisotope generator: steady power that decays slowly.</summary>
public class RtgModule : PowerModule
{
    /// <summary>Mission time used for decay (years). Cruise advances it.</summary>
    public float MissionElapsedYears { get; set; }

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        MissionElapsedYears = 0f;
    }

    /// <summary>Output after decay (W).</summary>
    public float ConvertDecayToElectricity()
    {
        // TODO(A1): NominalOutputW * (1 - DegradationRatePerYear * MissionElapsedYears), not below 0.
        return 0f;
    }

    public override float GetOutputW(float dynamicPressure, float solarFluxWm2) => ConvertDecayToElectricity();
}
