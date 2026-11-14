using Godot;

namespace RocketGame.Building;

/// <summary>Wind tunnel result (P2).</summary>
public struct AeroReport
{
    /// <summary>Drag force (N).</summary>
    public float DragN;
    /// <summary>Side force (N).</summary>
    public float LiftN;
    /// <summary>Vehicle-local (m).</summary>
    public Vector2 CenterOfPressure;
    /// <summary>Vehicle-local (m).</summary>
    public Vector2 CenterOfMass;
    /// <summary>True when the center of pressure is behind the center of mass.</summary>
    public bool IsStable;
}
