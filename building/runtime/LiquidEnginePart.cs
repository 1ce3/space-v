using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Throttleable liquid engine fed by FeedTanks.</summary>
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

    /// <summary>Total propellant left in the feed tanks (kg).</summary>
    public float GetAvailableFuel()
    {
        float sum = 0f;
        foreach (FuelTankPart tank in FeedTanks) sum += tank.CurrentFuel;
        return sum;
    }
}
