using Godot;
using RocketGame.Building;

namespace RocketGame.Physics;

/// <summary>Thrust, propellant flow and flameout of every burning engine (guide B 4.3.1).</summary>
public class PropulsionSolver
{
    /// <summary>Thrust of the engines in parts; drains feed tanks and solid propellant. Forces in the vehicle-local frame.</summary>
    public ThrustStepResult Solve(PartTree parts, float ambientPressure, Vector2 com, float deltaTime)
    {
        // TODO(B2): Lerp(vacuum, sea level) thrust by pressure, throttle mapping, gimbal, drain FeedTanks, BurnCurve, flameout.
        return new ThrustStepResult();
    }
}
