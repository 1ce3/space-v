using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Reaction wheel or RCS.</summary>
public abstract class AttitudeActuatorModule : PodModule
{
    /// <summary>Normalized torque command -1..1 (Control writes it).</summary>
    public float CommandedTorque { get; protected set; }

    public AcsModuleDefinition AcsDefinition => Definition as AcsModuleDefinition;

    /// <summary>Max torque (N*m).</summary>
    public float GetMaxTorque() => AcsDefinition?.ControlAuthority ?? 0f;

    /// <summary>Sets the normalized torque command, clamped -1..1. Sign: AGENTS.md 7.</summary>
    public void CommandTorque(float normalized)
    {
        CommandedTorque = Mathf.Clamp(normalized, -1f, 1f);
    }
}
