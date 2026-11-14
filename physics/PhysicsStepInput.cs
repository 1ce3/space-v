using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>State of the vehicle for one physics tick. Filled by VehicleRoot (C).</summary>
public struct PhysicsStepInput
{
    /// <summary>Center of mass in world 2D (m).</summary>
    public Vector2 Position;
    /// <summary>Velocity in world 2D (m/s).</summary>
    public Vector2 LinearVelocity;
    /// <summary>VehicleRoot.Rotation.Z (rad).</summary>
    public float BodyRotationRad;
    /// <summary>FlightMath.AttitudeDeg of the body (deg).</summary>
    public float PitchAngleDeg;
    /// <summary>Angular velocity about +Z (deg/s).</summary>
    public float AngularVelocityDegPerSec;
    /// <summary>Mass properties in the vehicle-local frame.</summary>
    public MassProperties Mass;
    /// <summary>Parts still attached to the vehicle.</summary>
    public PartTree Parts;
    /// <summary>Tick length (s).</summary>
    public float DeltaTime;
}
