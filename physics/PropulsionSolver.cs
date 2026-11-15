using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>Thrust, propellant flow and flameout of every burning engine (guide B 4.3.1). Works in the vehicle-local frame.</summary>
public class PropulsionSolver
{
    /// <summary>Sea-level pressure used to blend vacuum and sea-level thrust (Pa).</summary>
    public const float SeaLevelPressurePa = 101325f;

    private readonly List<FuelTankPart> _tanksWithFuel = new();

    /// <summary>
    /// Thrust of the burning engines in parts, drains feed tanks and solid propellant, shuts engines down when empty.
    /// Forces and torque are in the vehicle-local frame; com is the local center of mass (m).
    /// partLoads (optional) receives the thrust of each engine; events (optional) receives EngineFlameout.
    /// </summary>
    public ThrustStepResult Solve(PartTree parts, float ambientPressure, Vector2 com, float deltaTime,
        List<PartLoad> partLoads = null, List<FlightEvent> events = null)
    {
        var result = new ThrustStepResult();
        if (parts == null) return result;
        float pressureRatio = Mathf.Clamp(ambientPressure / SeaLevelPressurePa, 0f, 1f);

        foreach (RocketPart part in parts.ActiveParts)
        {
            if (part is not EnginePart engine || !engine.IsIgnited || engine.State == ComponentState.Broken) continue;
            EnginePartDefinition def = engine.EngineDefinition;
            if (def == null) continue;

            float factor = engine is LiquidEnginePart liquid ? LiquidThrottleFactor(liquid) : 1f;
            if (factor <= 0f) continue;

            float thrust = Mathf.Lerp(def.MaxThrustVac, def.GetMaxThrustSeaLevel(), pressureRatio) * factor;
            float burned = def.GetMassFlowRate() * factor * deltaTime;

            bool flameout;
            if (engine is LiquidEnginePart liquidEngine)
            {
                burned = DrainFeedTanks(liquidEngine, burned, out flameout);
            }
            else
            {
                thrust *= BurnSolid((SolidBoosterPart)engine, ref burned, out flameout);
            }

            Vector2 force = new Vector2(0f, 1f).Rotated(-Mathf.DegToRad(engine.GimbalAngleDeg)) * thrust;
            Vector2 r = engine.GetLocalPosition() - com;
            result.ThrustForceVector += force;
            result.TorqueZ += FlightMath.Cross(r, force);
            result.MassBurnedKg += burned;
            partLoads?.Add(new PartLoad { Part = engine, Force = force });

            if (flameout)
            {
                // Only exception to "physics never writes commands": an empty engine stops (guide B 4.3).
                engine.IsIgnited = false;
                events?.Add(new FlightEvent { Type = FlightEventType.EngineFlameout, Severity = 1f, SourceId = engine.ModelId });
            }
        }
        return result;
    }

    /// <summary>Throttle 0 gives 0; otherwise thrust fraction = Throttle, but never below MinThrottle.</summary>
    private static float LiquidThrottleFactor(LiquidEnginePart engine)
    {
        if (engine.Throttle <= 0f) return 0f;
        float min = engine.LiquidDefinition?.MinThrottle ?? 0f;
        return Mathf.Clamp(Mathf.Max(engine.Throttle, min), 0f, 1f);
    }

    /// <summary>Takes kg evenly from the feed tanks that still have fuel. Returns kg actually drained.</summary>
    private float DrainFeedTanks(LiquidEnginePart engine, float kg, out bool flameout)
    {
        _tanksWithFuel.Clear();
        foreach (FuelTankPart tank in engine.FeedTanks)
        {
            if (tank.CurrentFuel > 0f && tank.State != ComponentState.Broken) _tanksWithFuel.Add(tank);
        }
        float drained = 0f;
        float remaining = kg;
        // Two passes so a nearly empty tank's share moves to the others.
        for (int pass = 0; pass < 2 && remaining > 1e-6f && _tanksWithFuel.Count > 0; pass++)
        {
            float share = remaining / _tanksWithFuel.Count;
            foreach (FuelTankPart tank in _tanksWithFuel)
            {
                float take = Mathf.Min(share, tank.CurrentFuel);
                tank.CurrentFuel -= take;
                drained += take;
            }
            remaining = kg - drained;
            _tanksWithFuel.RemoveAll(t => t.CurrentFuel <= 0f);
        }
        flameout = remaining > 1e-4f;
        return drained;
    }

    /// <summary>Advances a solid booster burn. Returns the BurnCurve thrust multiplier; adjusts burned to the propellant left.</summary>
    private static float BurnSolid(SolidBoosterPart booster, ref float burned, out bool flameout)
    {
        float propellantMass = booster.BoosterDefinition?.PropellantMass ?? 0f;
        burned = Mathf.Min(burned, booster.RemainingPropellant);
        booster.RemainingPropellant -= burned;
        if (propellantMass > 0f) booster.BurnProgress = Mathf.Clamp(booster.BurnProgress + burned / propellantMass, 0f, 1f);
        flameout = booster.RemainingPropellant <= 0f;
        Curve curve = booster.BoosterDefinition?.BurnCurve;
        return curve != null ? curve.Sample(booster.BurnProgress) : 1f;
    }
}
