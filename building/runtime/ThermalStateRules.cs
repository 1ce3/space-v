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

    /// <summary>
    /// Share of a rocket part's dry mass that heats up in flight (its outer skin). 1 = the whole part; physics sets it
    /// from PhysicsSettings.ThermalSkinFraction. With the whole mass, aero heating raised a pod only a few kelvin.
    /// </summary>
    public static float SkinMassFraction { get; set; } = 1f;

    /// <summary>MaxSafeTempK with the hull material of a part (B9); modules use the definition value.</summary>
    public static float MaxSafeTemp(IFlightComponent c) =>
        c is RocketPart part ? part.GetEffectiveMaxSafeTempK() : c?.BaseDefinition?.MaxSafeTempK ?? 0f;

    /// <summary>Updates c.State from c.TempK.</summary>
    public static void Evaluate(IFlightComponent c)
    {
        ComponentDefinition def = c?.BaseDefinition;
        if (def == null || c.State == ComponentState.Broken) return;

        float t = c.TempK;
        float max = MaxSafeTemp(c);
        float min = def.MinSafeTempK;

        if (t > BreakFactor * max) c.State = ComponentState.Broken;
        else if (t > max) c.State = ComponentState.Overheated;
        else if (c.State == ComponentState.Overheated) { if (t < RecoverFactor * max) c.State = ComponentState.Active; }
        else if (t < min) c.State = ComponentState.Frozen;
        else if (c.State == ComponentState.Frozen && t > min + FrozenRecoverMarginK) c.State = ComponentState.Active;
    }
}
