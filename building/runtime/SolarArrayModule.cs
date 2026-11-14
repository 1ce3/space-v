using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Deployable solar array; breaks if deployed in dense air.</summary>
public class SolarArrayModule : PowerModule
{
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
        // TODO(A1): NominalOutputW * solarFluxWm2 / 1361 when deployed, not broken, operating.
        return 0f;
    }
}
