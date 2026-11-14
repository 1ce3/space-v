using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Throttleable, restartable liquid engine fed by FeedTanks.</summary>
public partial class LiquidEnginePart : EnginePart
{
    /// <summary>Throttle command 0..1 (Control writes it).</summary>
    public float Throttle { get; private set; }
    /// <summary>Ignitions left.</summary>
    public int RemainingIgnitions { get; set; }
    /// <summary>Tanks this engine drains (set by RocketAssembler.LinkFeedTanks).</summary>
    public List<FuelTankPart> FeedTanks { get; } = new();

    public LiquidEnginePartDefinition LiquidDefinition => Definition as LiquidEnginePartDefinition;

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        Throttle = 0f;
        RemainingIgnitions = LiquidDefinition?.MaxIgnitions ?? 0;
    }

    /// <summary>Sets throttle, clamped 0..1. Does not ignite (ThrottleController does).</summary>
    public void SetThrottle(float value)
    {
        Throttle = Mathf.Clamp(value, 0f, 1f);
    }

    /// <summary>Stops burning; the engine can relight while RemainingIgnitions > 0.</summary>
    public override void Shutdown()
    {
        IsIgnited = false;
    }

    /// <summary>Total propellant left in the attached feed tanks (kg).</summary>
    public float GetAvailableFuel()
    {
        float sum = 0f;
        foreach (FuelTankPart tank in FeedTanks)
            if (tank.IsAttached) sum += tank.CurrentFuel;
        return sum;
    }

    /// <summary>True when it could still burn: armed, not broken, ignitions and fuel left.</summary>
    public bool CanStillFire() =>
        IsArmed && State != ComponentState.Broken && (IsIgnited || RemainingIgnitions > 0) && GetAvailableFuel() > 0f;

    protected override bool TryConsumeIgnition()
    {
        if (RemainingIgnitions <= 0 || GetAvailableFuel() <= 0f) return false;
        RemainingIgnitions--;
        return true;
    }

    /// <summary>Lerp(MinThrottle, 1, Throttle) when Throttle > 0, else 0 (B_PHYSICS_RESEARCH.md 4.3.1).</summary>
    protected override float GetThrottleFactor()
    {
        if (Throttle <= 0f) return 0f;
        return Mathf.Lerp(LiquidDefinition?.MinThrottle ?? 0f, 1f, Throttle);
    }
}
