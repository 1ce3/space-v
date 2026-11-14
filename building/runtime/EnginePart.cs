using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Common runtime of liquid engines and solid boosters.</summary>
public abstract partial class EnginePart : RocketPart
{
    /// <summary>Set by staging (Arm). Only armed engines can ignite.</summary>
    public bool IsArmed { get; set; }
    /// <summary>Burning now. Physics may clear it on flameout.</summary>
    public bool IsIgnited { get; set; }
    /// <summary>Current gimbal angle (deg), within +-GimbalRangeDeg.</summary>
    public float GimbalAngleDeg { get; protected set; }

    public EnginePartDefinition EngineDefinition => Definition as EnginePartDefinition;

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        IsArmed = false;
        IsIgnited = false;
        GimbalAngleDeg = 0f;
    }

    /// <summary>Arms the engine (solid boosters also ignite).</summary>
    public virtual void Arm()
    {
        // TODO(A1)
    }

    /// <summary>Starts burning if allowed (A_BUILDING.md 4.3).</summary>
    public virtual void Ignite()
    {
        // TODO(A1)
    }

    /// <summary>Stops burning (liquid only; solids cannot stop).</summary>
    public virtual void Shutdown()
    {
        // TODO(A1)
    }

    /// <summary>Thrust now for the ambient pressure (N); 0 when not burning.</summary>
    public virtual float ComputeThrust(float atmPressure)
    {
        // TODO(A1): Lerp(MaxThrustVac, GetMaxThrustSeaLevel(), clamp(p / 101325)) * throttle factor.
        return 0f;
    }

    /// <summary>Propellant flow now (kg/s); 0 when not burning.</summary>
    public virtual float GetMassFlowRate()
    {
        // TODO(A1)
        return 0f;
    }

    /// <summary>Sets the gimbal angle, clamped to +-GimbalRangeDeg. Sign: AGENTS.md 7.</summary>
    public void SetGimbal(float angleDeg)
    {
        float range = EngineDefinition?.GimbalRangeDeg ?? 0f;
        GimbalAngleDeg = Mathf.Clamp(angleDeg, -range, range);
    }
}
