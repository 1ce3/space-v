using RocketGame.Building;

namespace RocketGame.Physics;

/// <summary>Electric supply and demand of the pod modules (guide B 5, P1 in flight).</summary>
public class PowerSolver
{
    private readonly PhysicsSettings _settings;

    public PowerSolver(PhysicsSettings settings) => _settings = settings;

    /// <summary>One flight tick (P1).</summary>
    public PowerStepResult Step(PartTree parts, float dynamicPressure, float solarFluxWm2, float deltaTime)
    {
        // TODO(B6): supply vs demand, battery drain, brownout.
        return new PowerStepResult { BatteryFraction = 1f };
    }

    /// <summary>One cruise day: 24 h balance, battery charge, brownout.</summary>
    public PowerStepResult SolveSteadyState(PartTree parts, float solarFluxWm2)
    {
        // TODO(B5): solar, RTG, battery, CruiseDutyFactor, brownout sets IsPowered = false.
        return new PowerStepResult { BatteryFraction = 1f };
    }
}
