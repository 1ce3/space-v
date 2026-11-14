using System.Collections.Generic;
using Godot;
using RocketGame.Building;

namespace RocketGame.Physics;

/// <summary>Entry heating, ablation and radiative cooling in flight; steady-state temperature in cruise (guide B 4.5, 5).</summary>
public class ThermalSolver
{
    /// <summary>Sutton-Graves constant of the current body.</summary>
    public float SuttonGravesConstant { get; set; }
    /// <summary>Nose radius used for heat flux (m).</summary>
    public float NoseRadius { get; set; } = 1f;

    private readonly PhysicsSettings _settings;
    private readonly List<string> _overheated = new();
    private readonly List<string> _broken = new();
    private readonly List<RocketPart> _brokenParts = new();

    public ThermalSolver(PhysicsSettings settings)
    {
        _settings = settings;
        NoseRadius = settings?.NoseRadiusM ?? 1f;
    }

    /// <summary>Loads the Sutton-Graves constant of the body.</summary>
    public void Configure(CelestialBodyDefinition body) => SuttonGravesConstant = body.SuttonGravesConstant;

    /// <summary>Convective heat flux q = k * sqrt(rho / Rn) * v^3 (W/m^2).</summary>
    public float CalculateHeatFlux(float airDensity, float speed)
    {
        if (airDensity <= 0f || NoseRadius <= 0f) return 0f;
        return SuttonGravesConstant * Mathf.Sqrt(airDensity / NoseRadius) * speed * speed * speed;
    }

    /// <summary>One flight tick: heat into the leading part or heat shield, cooling of every part.</summary>
    public ThermalStepResult Step(PartTree parts, float heatFlux, float deltaTime)
    {
        // TODO(B3): ablation through HeatShieldPart.Ablate, ApplyThermalLoad, radiative cooling.
        return NewResult();
    }

    /// <summary>One cruise day: equilibrium temperature of the pod and its modules.</summary>
    public ThermalStepResult SolveSteadyState(PartTree parts, float solarFluxWm2)
    {
        // TODO(B5): Q_in / (eps sigma A) ^ 1/4, insulation, RHU, radiator, ThermalStateRules.
        return NewResult();
    }

    private ThermalStepResult NewResult()
    {
        _overheated.Clear();
        _broken.Clear();
        _brokenParts.Clear();
        return new ThermalStepResult
        {
            LowestTempRatio = float.MaxValue,
            OverheatedComponents = _overheated,
            BrokenComponents = _broken,
            BrokenParts = _brokenParts,
        };
    }
}
