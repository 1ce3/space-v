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
        // TODO(A1): 1 - ReflectiveEfficiency * (1 - CurrentDegradation) when operating.
        return 1f;
    }
}
