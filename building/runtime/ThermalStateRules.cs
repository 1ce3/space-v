using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Temperature -> ComponentState rules (decision #12). Physics calls this after every TempK change.</summary>
public static class ThermalStateRules
{
    /// <summary>Above MaxSafeTempK times this, the component breaks.</summary>
    public const float BreakFactor = 1.25f;
    /// <summary>An overheated component recovers below MaxSafeTempK times this.</summary>
    public const float RecoverFactor = 0.9f;
    /// <summary>A frozen component recovers above MinSafeTempK plus this (K).</summary>
    public const float FrozenRecoverMarginK = 10f;

    /// <summary>J/(kg*K) used for HeatCapacityJPerK = DryMass * this.</summary>
    public const float SpecificHeat = 900f;

    /// <summary>Updates c.State from c.TempK.</summary>
    public static void Evaluate(IFlightComponent c)
    {
        // TODO(A1): Broken stays Broken; > BreakFactor*Max -> Broken; > Max -> Overheated;
        //           Overheated and < RecoverFactor*Max -> Active; < Min -> Frozen; Frozen and > Min + margin -> Active.
    }
}
