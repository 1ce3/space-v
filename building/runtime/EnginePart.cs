using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Common runtime of liquid engines and solid boosters.</summary>
public abstract partial class EnginePart : RocketPart
{
    /// <summary>Pressure where sea-level Isp applies (Pa).</summary>
    public const float SeaLevelPressurePa = 101325f;

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
        IsArmed = true;
    }

    /// <summary>Starts burning if armed, not burning, not broken and the subclass allows it (A_BUILDING.md 4.3).</summary>
    public void Ignite()
    {
        if (!IsArmed || IsIgnited || State == ComponentState.Broken) return;
        if (!TryConsumeIgnition()) return;
        IsIgnited = true;
        if (State == ComponentState.Idle) State = ComponentState.Active;
    }

    /// <summary>Stops burning (liquid only; solids cannot stop).</summary>
    public virtual void Shutdown() { }

    /// <summary>Subclass check before ignition; may consume an ignition. Returns false to refuse.</summary>
    protected abstract bool TryConsumeIgnition();

    /// <summary>Fraction of full thrust and flow from the throttle (liquid) or 1 (solid).</summary>
    protected abstract float GetThrottleFactor();

    /// <summary>Extra thrust multiplier (solid burn curve); 1 by default.</summary>
    protected virtual float GetThrustCurveFactor() => 1f;

    /// <summary>Thrust now for the ambient pressure (N); 0 when not burning.</summary>
    public float ComputeThrust(float atmPressure)
    {
        EnginePartDefinition def = EngineDefinition;
        if (def == null || !IsIgnited || State == ComponentState.Broken) return 0f;
        float ratio = Mathf.Clamp(atmPressure / SeaLevelPressurePa, 0f, 1f);
        float full = Mathf.Lerp(def.MaxThrustVac, def.GetMaxThrustSeaLevel(), ratio);
        return full * GetThrottleFactor() * GetThrustCurveFactor();
    }

    /// <summary>Propellant flow now (kg/s); 0 when not burning.</summary>
    public float GetMassFlowRate()
    {
        EnginePartDefinition def = EngineDefinition;
        if (def == null || !IsIgnited || State == ComponentState.Broken) return 0f;
        return def.GetMassFlowRate() * GetThrottleFactor();
    }

    /// <summary>Sets the gimbal angle, clamped to +-GimbalRangeDeg. Sign: AGENTS.md 7.</summary>
    public void SetGimbal(float angleDeg)
    {
        float range = EngineDefinition?.GimbalRangeDeg ?? 0f;
        GimbalAngleDeg = Mathf.Clamp(angleDeg, -range, range);
    }
}
