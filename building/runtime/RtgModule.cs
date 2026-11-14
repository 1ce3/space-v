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
        if (State == ComponentState.Broken || PowerDefinition == null) return 0f;
        float factor = 1f - PowerDefinition.DegradationRatePerYear * MissionElapsedYears;
        return Mathf.Max(0f, PowerDefinition.NominalOutputW * factor);
    }

    public override float GetOutputW(float dynamicPressure, float solarFluxWm2) => ConvertDecayToElectricity();
}
