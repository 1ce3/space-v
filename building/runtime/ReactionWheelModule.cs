using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Reaction wheel: torque without propellant until saturated.</summary>
public class ReactionWheelModule : AttitudeActuatorModule
{
    /// <summary>Stored angular momentum (N*m*s). Physics writes it.</summary>
    public float StoredMomentumNms { get; set; }
    /// <summary>True at max storage. Physics writes it.</summary>
    public bool IsSaturated { get; set; }

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        StoredMomentumNms = 0f;
        IsSaturated = false;
    }

    /// <summary>Wheel capacity (N*m*s).</summary>
    public float MaxMomentumStorageNms => AcsDefinition?.MaxMomentumStorageNms ?? 0f;

    /// <summary>Unloads stored momentum (needs RCS to counter it).</summary>
    public void DumpMomentum()
    {
        StoredMomentumNms = 0f;
        IsSaturated = false;
    }
}
