namespace RocketGame.Physics;

/// <summary>Power balance of one tick or one cruise day.</summary>
public struct PowerStepResult
{
    /// <summary>Power produced (W).</summary>
    public float SupplyW;
    /// <summary>Power consumed (W).</summary>
    public float DemandW;
    /// <summary>Battery state of charge (0..1).</summary>
    public float BatteryFraction;
    /// <summary>Battery empty and supply below demand.</summary>
    public bool IsBrownout;
}
