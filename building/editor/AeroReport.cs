using Godot;

namespace RocketGame.Building;

/// <summary>Wind tunnel result (P2).</summary>
public struct AeroReport
{
    /// <summary>Drag force along the flow (N).</summary>
    public float DragN;
    /// <summary>Force across the flow (N); positive pushes toward the right of the direction of travel.</summary>
    public float LiftN;
    /// <summary>Vehicle-local (m).</summary>
    public Vector2 CenterOfPressure;
    /// <summary>Vehicle-local (m).</summary>
    public Vector2 CenterOfMass;
    /// <summary>Center of mass minus center of pressure along the rocket (m); positive = stable.</summary>
    public float StabilityMarginM;
    /// <summary>True when the aero torque turns the nose back into the flow (center of pressure behind the center of mass).</summary>
    public bool IsStable;
}
