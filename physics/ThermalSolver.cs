using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;

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
    private readonly Dictionary<RocketPart, ComponentState> _stateBefore = new();

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

    /// <summary>
    /// One flight tick (guide B 4.5). velDirBody = unit velocity in the vehicle-local frame (zero when at rest), com = local
    /// center of mass, airTemperatureK = ambient temperature for radiative exchange (3 K in vacuum).
    /// Base first with a heat shield: the shield ablates; once its ablator is gone while still heated it burns through (Broken).
    /// Otherwise the most forward unshielded part takes the heat. Every part radiates; burning engines heat themselves (P1).
    /// </summary>
    public ThermalStepResult Step(PartTree parts, float heatFlux, float deltaTime,
        Vector2 velDirBody = default, Vector2 com = default, float airTemperatureK = 3f)
    {
        ThermalStepResult result = NewResult();
        if (parts == null) return result;

        _stateBefore.Clear();
        foreach (RocketPart part in parts.ActiveParts) _stateBefore[part] = part.State;

        float partition = _settings?.HeatPartition ?? 0.5f;
        if (heatFlux > 0f && velDirBody != Vector2.Zero)
        {
            HeatShieldPart shield = BaseFirst(velDirBody) ? FindWorkingShield(parts) : null;
            if (shield != null)
            {
                result.MassAblatedKg += HeatShield(shield, heatFlux * shield.FrontalArea * partition * deltaTime);
            }
            else
            {
                RocketPart leading = FindLeadingPart(parts, velDirBody, com);
                leading?.ApplyThermalLoad(heatFlux * leading.FrontalArea * partition * deltaTime);
            }
        }

        CoolAndHeatEngines(parts, deltaTime, airTemperatureK);
        CollectStates(parts, ref result);
        return result;
    }

    /// <summary>One cruise day: equilibrium temperature of the pod and its modules.</summary>
    public ThermalStepResult SolveSteadyState(PartTree parts, float solarFluxWm2)
    {
        // TODO(B5): Q_in / (eps sigma A) ^ 1/4, insulation, RHU, radiator, ThermalStateRules.
        return NewResult();
    }

    private bool BaseFirst(Vector2 velDirBody)
    {
        float cone = Mathf.DegToRad(_settings?.ShieldingConeDeg ?? 30f);
        return Mathf.Abs(new Vector2(0f, -1f).AngleTo(velDirBody)) < cone;
    }

    private static HeatShieldPart FindWorkingShield(PartTree parts)
    {
        foreach (RocketPart part in parts.ActiveParts)
        {
            if (part is HeatShieldPart shield && shield.State != ComponentState.Broken) return shield;
        }
        return null;
    }

    /// <summary>Ablates; leftover heat warms the shield; heat with no ablator left means burn-through. Returns kg ablated.</summary>
    private static float HeatShield(HeatShieldPart shield, float heatJoule)
    {
        float before = shield.AblatorRemainingKg;
        float leftover = shield.Ablate(heatJoule);
        if (leftover > 0f && shield.AblatorRemainingKg <= 0f)
        {
            shield.State = ComponentState.Broken;
            GD.Print($"[Physics] Heat shield {shield.ModelId} burned through (ablator gone)");
        }
        else if (leftover > 0f)
        {
            shield.ApplyThermalLoad(leftover);
        }
        return before - shield.AblatorRemainingKg;
    }

    /// <summary>Unshielded part furthest along the velocity (largest r . v).</summary>
    private static RocketPart FindLeadingPart(PartTree parts, Vector2 velDirBody, Vector2 com)
    {
        RocketPart best = null;
        float bestDot = float.MinValue;
        foreach (RocketPart part in parts.ActiveParts)
        {
            if (part.IsShielded || part is DecouplerPart) continue;
            float dot = (part.GetLocalPosition() - com).Dot(velDirBody);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = part;
            }
        }
        return best;
    }

    private void CoolAndHeatEngines(PartTree parts, float dt, float airTemperatureK)
    {
        float emissivity = _settings?.PartEmissivity ?? 0.8f;
        float engineHeat = _settings?.EngineHeatW ?? 0f;
        float airT4 = airTemperatureK * airTemperatureK * airTemperatureK * airTemperatureK;
        foreach (RocketPart part in parts.ActiveParts)
        {
            float t = part.TempK;
            float radiated = emissivity * FlightMath.StefanBoltzmann * part.SideArea * (t * t * t * t - airT4) * dt;
            float engine = part is EnginePart e && e.IsIgnited ? engineHeat * EngineFactor(e) * dt : 0f;
            part.ApplyThermalLoad(engine - radiated);
        }
    }

    private static float EngineFactor(EnginePart engine)
    {
        if (engine is not LiquidEnginePart liquid) return 1f;
        if (liquid.Throttle <= 0f) return 0f;
        return Mathf.Lerp(liquid.LiquidDefinition?.MinThrottle ?? 0f, 1f, liquid.Throttle);
    }

    private void CollectStates(PartTree parts, ref ThermalStepResult result)
    {
        foreach (RocketPart part in parts.ActiveParts)
        {
            float max = part.Definition?.MaxSafeTempK ?? 0f;
            float min = part.Definition?.MinSafeTempK ?? 0f;
            if (max > 0f) result.MaxTempRatio = Mathf.Max(result.MaxTempRatio, part.TempK / max);
            if (min > 0f) result.LowestTempRatio = Mathf.Min(result.LowestTempRatio, part.TempK / min);

            _stateBefore.TryGetValue(part, out ComponentState before);
            if (part.State == before) continue;
            if (part.State == ComponentState.Overheated) _overheated.Add(part.ModelId);
            if (part.State == ComponentState.Broken)
            {
                _broken.Add(part.ModelId);
                _brokenParts.Add(part);
            }
        }
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
