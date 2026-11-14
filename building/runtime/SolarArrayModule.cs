using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Deployable solar array; breaks if deployed in dense air.</summary>
public class SolarArrayModule : PowerModule
{
    /// <summary>Flux at which NominalOutputW is rated (W/m^2, Earth orbit).</summary>
    public const float ReferenceSolarFluxWm2 = 1361f;

    public bool IsDeployed { get; private set; }
    /// <summary>Set by physics when deployed above a q limit.</summary>
    public bool IsBrokenByAero { get; set; }

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        IsDeployed = false;
        IsBrokenByAero = false;
    }

    /// <summary>Deploys or stows (flight_deploy_toggle).</summary>
    public void SetDeployed(bool deployed)
    {
        IsDeployed = deployed;
    }

    public override float GetOutputW(float dynamicPressure, float solarFluxWm2)
    {
        if (!IsDeployed || IsBrokenByAero || !IsHealthy) return 0f;
        return (PowerDefinition?.NominalOutputW ?? 0f) * solarFluxWm2 / ReferenceSolarFluxWm2;
    }
}
