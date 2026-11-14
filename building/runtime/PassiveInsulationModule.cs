using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Multi-layer insulation.</summary>
public class PassiveInsulationModule : ThermalModule
{
    /// <summary>0..1 wear.</summary>
    public float CurrentDegradation { get; set; }

    public override float GetAbsorptionFactor()
    {
        if (!IsHealthy) return 1f;
        float efficiency = ThermalDefinition?.ReflectiveEfficiency ?? 0f;
        return 1f - efficiency * (1f - Mathf.Clamp(CurrentDegradation, 0f, 1f));
    }
}
