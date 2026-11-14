using Godot;

namespace RocketGame.Physics;

/// <summary>Aerodynamic result for a set of parts.</summary>
public struct AeroResult
{
    /// <summary>Total aero force (N).</summary>
    public Vector2 Force;
    /// <summary>Aero torque about the given center of mass (N*m).</summary>
    public float TorqueZ;
    /// <summary>Center of pressure (m, same frame as the input).</summary>
    public Vector2 CenterOfPressure;
    /// <summary>q (Pa).</summary>
    public float DynamicPressure;
    /// <summary>Mach number.</summary>
    public float MachNumber;
}
