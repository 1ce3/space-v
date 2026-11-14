using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>What a meteor hit did (B12).</summary>
public enum MeteorImpactOutcome { None, Scratch, Leak, HullBreach, EngineBroken, PartBroken }

/// <summary>Result of MeteorImpact.Resolve. Event is set (Type, Severity, SourceId) for Leak, HullBreach and EngineBroken.</summary>
public readonly record struct MeteorImpactResult(MeteorImpactOutcome Outcome, FlightEvent Event)
{
    /// <summary>True when the hit did more than scratch the part.</summary>
    public bool IsDamage => Outcome >= MeteorImpactOutcome.Leak;
}

/// <summary>
/// Phase 2 (PHASE2_PLAN.md 5.6, owner B, task B12): consequences of a meteor hitting a part.
/// Threshold = the part's effective impact tolerance (material included, B9):
/// below it a scratch; above it a tank leaks (CryoLeak), the pod gets a HullBreach, an engine breaks;
/// above MeteorBreakFactor (3) times it the part breaks like a collision. A working FuelSensor slows a tank leak,
/// a working PressureSensor slows module failures in a hull breach (LeakReduction).
/// C11 detects the hit and calls PhysicsCoordinator.ResolveMeteorHit (or Resolve with settings and the detector).
/// </summary>
public static class MeteorImpact
{
    /// <summary>C9 contract: default settings, no break handler (a broken part is only marked Broken). True when damaged.</summary>
    public static bool Resolve(RocketPart part, float speedMps) => Resolve(part, speedMps, null, null).IsDamage;

    /// <summary>Applies the hit. failures: used for PartBroken so the part is cut and BreakTriggered fires; null = mark Broken only.</summary>
    public static MeteorImpactResult Resolve(RocketPart part, float speedMps, PhysicsSettings settings, FailureDetector failures)
    {
        settings ??= new PhysicsSettings();
        if (part == null || !part.IsAttached || speedMps <= 0f) return new MeteorImpactResult(MeteorImpactOutcome.None, default);

        float tolerance = Mathf.Max(0.01f, part.GetEffectiveImpactToleranceMps());
        if (speedMps <= tolerance) return new MeteorImpactResult(MeteorImpactOutcome.Scratch, default);

        if (speedMps > tolerance * settings.MeteorBreakFactor)
        {
            if (failures != null) failures.TriggerBreak(part, FailureCause.Collision);
            else part.State = ComponentState.Broken;
            return new MeteorImpactResult(MeteorImpactOutcome.PartBroken,
                new FlightEvent { Type = FlightEventType.HullBreach, Severity = 1f, SourceId = part.ModelId });
        }

        CommandPodPart pod = FindPod(part);
        switch (part)
        {
            case FuelTankPart tank:
                float rate = settings.MeteorLeakRateKgPerSec * (1f - SensorReduction(pod, InstrumentKind.FuelSensor));
                tank.LeakRateKgPerSec = Mathf.Max(tank.LeakRateKgPerSec, rate);
                return new MeteorImpactResult(MeteorImpactOutcome.Leak,
                    new FlightEvent { Type = FlightEventType.CryoLeak, Severity = 0.5f, SourceId = tank.ModelId });
            case CommandPodPart hull:
                if (!hull.IsHullBreached) hull.HullBreachTimer = HullBreachInterval(hull, settings.HullBreachFailSeconds);
                hull.IsHullBreached = true;
                return new MeteorImpactResult(MeteorImpactOutcome.HullBreach,
                    new FlightEvent { Type = FlightEventType.HullBreach, Severity = 0.7f, SourceId = hull.ModelId });
            case EnginePart engine:
                engine.State = ComponentState.Broken;
                engine.IsIgnited = false;
                return new MeteorImpactResult(MeteorImpactOutcome.EngineBroken,
                    new FlightEvent { Type = FlightEventType.EngineFlameout, Severity = 1f, SourceId = engine.ModelId });
            default:
                return new MeteorImpactResult(MeteorImpactOutcome.Scratch, default);
        }
    }

    /// <summary>Largest LeakReduction (0..1) of the working instruments of this kind in the pod.</summary>
    public static float SensorReduction(CommandPodPart pod, InstrumentKind kind)
    {
        if (pod == null) return 0f;
        float best = 0f;
        foreach (InstrumentModule sensor in pod.GetModules<InstrumentModule>())
        {
            InstrumentModuleDefinition def = sensor.InstrumentDefinition;
            if (def != null && def.Kind == kind && sensor.CanOperate()) best = Mathf.Max(best, def.LeakReduction);
        }
        return Mathf.Clamp(best, 0f, 0.95f);
    }

    /// <summary>Time between module failures of a hull breach: baseInterval made longer by a PressureSensor.</summary>
    public static float HullBreachInterval(CommandPodPart pod, float baseInterval) =>
        baseInterval / (1f - SensorReduction(pod, InstrumentKind.PressureSensor));

    /// <summary>
    /// Advances a hull breach by elapsed (seconds in flight, days in cruise; baseInterval in the same unit).
    /// Every interval a random working module breaks. Returns its ModelId, or "" when nothing broke.
    /// </summary>
    public static string TickHullBreach(CommandPodPart pod, float elapsed, float baseInterval, RandomNumberGenerator rng)
    {
        if (pod == null || !pod.IsHullBreached || elapsed <= 0f || baseInterval <= 0f) return "";
        pod.HullBreachTimer -= elapsed;
        if (pod.HullBreachTimer > 0f) return "";
        pod.HullBreachTimer += HullBreachInterval(pod, baseInterval);

        var working = pod.Modules.FindAll(m => m.State != ComponentState.Broken);
        if (working.Count == 0) return "";
        PodModule module = working[rng != null ? rng.RandiRange(0, working.Count - 1) : 0];
        module.State = ComponentState.Broken;
        module.IsPowered = false;
        return module.ModelId;
    }

    private static CommandPodPart FindPod(RocketPart part)
    {
        if (part is CommandPodPart self) return self;
        return PowerSolver.FindPod(part?.Tree);
    }
}
