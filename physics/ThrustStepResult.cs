using Godot;

namespace RocketGame.Physics;

/// <summary>Propulsion result of one tick.</summary>
public struct ThrustStepResult
{
    /// <summary>Total thrust (N).</summary>
    public Vector2 ThrustForceVector;
    /// <summary>Thrust torque about the center of mass (N*m).</summary>
    public float TorqueZ;
    /// <summary>Propellant burned (kg).</summary>
    public float MassBurnedKg;
    /// <summary>Propellant lost to leaks (kg).</summary>
    public float MassLeakedKg;
}
