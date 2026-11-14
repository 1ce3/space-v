using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>Drag, fin lift and parachute drag summed over parts (guide B 4.3.2). Also used by WindTunnelAnalyzer (A).</summary>
public class AerodynamicSolver
{
    /// <summary>Dynamic pressure warning limit (Pa).</summary>
    public float MaxQLimit { get; set; }

    private readonly PhysicsSettings _settings;

    public AerodynamicSolver(PhysicsSettings settings)
    {
        _settings = settings;
        MaxQLimit = settings?.MaxQLimit ?? 40000f;
    }

    /// <summary>Aero force and torque on the parts. airVelBody = air velocity relative to the vehicle, vehicle-local frame (m/s).</summary>
    public AeroResult Solve(PartTree parts, Vector2 airVelBody, AtmosphereSample air, Vector2 com)
    {
        float speed = airVelBody.Length();
        // TODO(B2): per-part drag, fin lift, parachute drag, shielding, MachDragCurve.
        return new AeroResult
        {
            Force = Vector2.Zero,
            TorqueZ = 0f,
            CenterOfPressure = com,
            DynamicPressure = CalculateDynamicPressure(air.Density, speed),
            MachNumber = air.SpeedOfSound > 0f ? speed / air.SpeedOfSound : 0f,
        };
    }

    /// <summary>q = 0.5 rho v^2 (Pa).</summary>
    public float CalculateDynamicPressure(float airDensity, float speed) => 0.5f * airDensity * speed * speed;

    /// <summary>True when q is above MaxQLimit.</summary>
    public bool IsOverMaxQ(float dynamicPressure) => dynamicPressure > MaxQLimit;
}
