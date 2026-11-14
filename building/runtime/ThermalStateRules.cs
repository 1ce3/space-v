using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Thermal state machine shared by parts and modules. Physics changes TempK, then calls Evaluate.</summary>
public static class ThermalStateRules
{
    /// <summary>Above this multiple of MaxSafeTempK the component is destroyed (Broken).</summary>
    public const float BrokenTempFactor = 1.25f;

    /// <summary>An Overheated component recovers once it cools below this multiple of MaxSafeTempK.</summary>
    public const float OverheatRecoverFactor = 0.9f;

    /// <summary>A Frozen component recovers once it warms this far above MinSafeTempK, in K.</summary>
    public const float FrozenRecoverMarginK = 10f;

    /// <summary>Updates the component State from its TempK. Broken is permanent.</summary>
    public static void Evaluate(IFlightComponent c)
    {
        if (c == null || c.BaseDefinition == null || c.State == ComponentState.Broken) return;

        float maxSafe = c.BaseDefinition.MaxSafeTempK;
        float minSafe = c.BaseDefinition.MinSafeTempK;
        float temp = c.TempK;

        if (temp > BrokenTempFactor * maxSafe)
        {
            c.State = ComponentState.Broken;
            return;
        }

        if (temp > maxSafe)
        {
            c.State = ComponentState.Overheated;
            return;
        }

        if (c.State == ComponentState.Overheated && temp < OverheatRecoverFactor * maxSafe)
        {
            c.State = ComponentState.Active;
        }

        if (temp < minSafe)
        {
            c.State = ComponentState.Frozen;
            return;
        }

        if (c.State == ComponentState.Frozen && temp > minSafe + FrozenRecoverMarginK)
        {
            c.State = ComponentState.Active;
        }
    }
}
