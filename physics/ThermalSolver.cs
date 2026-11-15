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
    private readonly List<string> _frozen = new();
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

    /// <summary>
    /// One cruise day (guide B 5 step 3): equilibrium temperature of the pod and its modules.
    /// Q_in = flux * Absorptivity * PodCrossAreaM2 + RTG WasteHeatW + RHU HeaterOutputW + electricLoadW;
    /// eps = BaseEmissivity * (1 - best working insulation efficiency); extended radiators reject MaxHeatRejectionW on top;
    /// T = ((Q_in - rejected) / (eps * sigma * PodRadiatingAreaM2))^(1/4). The pod and every module get T, then ThermalStateRules.
    /// electricLoadW = power actually used that day (PowerStepResult.ConsumedW).
    /// </summary>
    public ThermalStepResult SolveSteadyState(PartTree parts, float solarFluxWm2, float electricLoadW = 0f)
    {
        ThermalStepResult result = NewResult();
        CommandPodPart pod = PowerSolver.FindPod(parts);
        if (pod == null)
        {
            result.LowestTempRatio = 1f;
            return result;
        }

        float heatIn = Mathf.Max(0f, solarFluxWm2) * (_settings?.Absorptivity ?? 0.5f) * (_settings?.PodCrossAreaM2 ?? 3.1f)
            + Mathf.Max(0f, electricLoadW);
        float insulation = 0f;
        float rejected = 0f;
        foreach (PodModule module in pod.Modules)
        {
            switch (module)
            {
                case RtgModule rtg when rtg.State != ComponentState.Broken:
                    heatIn += rtg.PowerDefinition?.WasteHeatW ?? 0f;
                    break;
                case RadioisotopeHeaterModule rhu when rhu.State != ComponentState.Broken:
                    heatIn += rhu.EmitConstantDecayHeat();
                    break;
                case PassiveInsulationModule mli when mli.IsHealthy:
                    float efficiency = (mli.ThermalDefinition?.ReflectiveEfficiency ?? 0f) * (1f - Mathf.Clamp(mli.CurrentDegradation, 0f, 1f));
                    insulation = Mathf.Max(insulation, efficiency);
                    break;
                case ActiveRadiatorModule radiator:
                    rejected += radiator.GetHeatRejectionW();
                    break;
            }
        }

        float emissivity = Mathf.Max(0.01f, (_settings?.BaseEmissivity ?? 0.8f) * (1f - Mathf.Clamp(insulation, 0f, 1f)));
        float area = Mathf.Max(0.01f, _settings?.PodRadiatingAreaM2 ?? 12f);
        float net = Mathf.Max(1f, heatIn - rejected);
        float temp = Mathf.Pow(net / (emissivity * FlightMath.StefanBoltzmann * area), 0.25f);

        SetSteadyTemp(pod, temp, ref result);
        foreach (PodModule module in pod.Modules) SetSteadyTemp(module, temp, ref result);
        if (result.LowestTempRatio == float.MaxValue) result.LowestTempRatio = 1f;
        return result;
    }

    private void SetSteadyTemp(IFlightComponent c, float temp, ref ThermalStepResult result)
    {
        ComponentState before = c.State;
        c.TempK = temp;
        ThermalStateRules.Evaluate(c);

        float max = ThermalStateRules.MaxSafeTemp(c);
        float min = c.BaseDefinition?.MinSafeTempK ?? 0f;
        if (max > 0f) result.MaxTempRatio = Mathf.Max(result.MaxTempRatio, temp / max);
        if (min > 0f) result.LowestTempRatio = Mathf.Min(result.LowestTempRatio, temp / min);

        if (c.State == before) return;
        string id = c.BaseDefinition?.ModelId ?? "";
        if (c.State == ComponentState.Overheated) _overheated.Add(id);
        if (c.State == ComponentState.Frozen) _frozen.Add(id);
        if (c.State == ComponentState.Broken)
        {
            _broken.Add(id);
            if (c is RocketPart part) _brokenParts.Add(part);
        }
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
        return Mathf.Clamp(Mathf.Max(liquid.Throttle, liquid.LiquidDefinition?.MinThrottle ?? 0f), 0f, 1f);
    }

    private void CollectStates(PartTree parts, ref ThermalStepResult result)
    {
        foreach (RocketPart part in parts.ActiveParts)
        {
            float max = part.GetEffectiveMaxSafeTempK();
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
        _frozen.Clear();
        _brokenParts.Clear();
        return new ThermalStepResult
        {
            FrozenComponents = _frozen,
            LowestTempRatio = float.MaxValue,
            OverheatedComponents = _overheated,
            BrokenComponents = _broken,
            BrokenParts = _brokenParts,
        };
    }
}
