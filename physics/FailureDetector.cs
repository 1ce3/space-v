using System;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>Decides when a part breaks (stress, heat, impact) and raises BreakTriggered before cutting it (guide B 4.4).</summary>
public class FailureDetector
{
    /// <summary>Raised before the part is cut. FlightDirector (C) ends the run.</summary>
    public event Action<RocketPart, FailureCause> BreakTriggered;

    private readonly StructuralBreakHandler _breakHandler;
    private bool _brokeThisTick;

    public FailureDetector(StructuralBreakHandler breakHandler) => _breakHandler = breakHandler;

    /// <summary>Allows one new Break. PhysicsCoordinator calls it at the start of every tick.</summary>
    public void BeginTick() => _brokeThisTick = false;

    /// <summary>First violation of the report becomes a Break.</summary>
    public void MonitorStressThresholds(StressReport report)
    {
        if (report == null || report.Violations.Count == 0) return;
        StressViolation worst = report.Violations[0];
        TriggerBreak(worst.Part, worst.Cause);
    }

    /// <summary>A grid part that became Broken from heat becomes a Break (Thermal). Broken modules do not.</summary>
    public void MonitorThermalLimits(ThermalStepResult thermal)
    {
        if (thermal.BrokenParts == null || thermal.BrokenParts.Count == 0) return;
        TriggerBreak(thermal.BrokenParts[0], FailureCause.Thermal);
    }

    /// <summary>Breaks part when contactSpeed is above its ImpactToleranceMps. Returns true when it broke.</summary>
    public bool MonitorImpact(RocketPart part, float contactSpeed)
    {
        if (part == null || contactSpeed <= part.GetEffectiveImpactToleranceMps()) return false;
        TriggerBreak(part, FailureCause.Collision);
        return true;
    }

    /// <summary>Raises BreakTriggered, then asks StructuralBreakHandler to cut the part. At most one Break per tick.</summary>
    public void TriggerBreak(RocketPart part, FailureCause cause)
    {
        if (part == null || _brokeThisTick) return;
        _brokeThisTick = true;
        BreakTriggered?.Invoke(part, cause);
        _breakHandler.HandleBreak(part, cause);
    }
}
