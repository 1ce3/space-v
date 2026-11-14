using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Steerable fin.</summary>
public partial class ControlFinPart : AeroSurfacePart
{
    /// <summary>Current deflection (deg), within +-MaxDeflectionDeg.</summary>
    public float DeflectionAngleDeg { get; private set; }

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        DeflectionAngleDeg = 0f;
    }

    /// <summary>Sets deflection, clamped. Sign: AGENTS.md 7.</summary>
    public void SetDeflection(float angleDeg)
    {
        float max = AeroDefinition?.MaxDeflectionDeg ?? 0f;
        DeflectionAngleDeg = Mathf.Clamp(angleDeg, -max, max);
    }
}
