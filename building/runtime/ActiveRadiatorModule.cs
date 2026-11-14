using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Deployable radiator panel.</summary>
public class ActiveRadiatorModule : ThermalModule
{
    public bool IsPanelExtended { get; private set; }
    /// <summary>0..1 pump setting.</summary>
    public float CoolantPumpRate { get; set; } = 1f;

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        IsPanelExtended = false;
    }

    public override float GetHeatRejectionW()
    {
        // TODO(A1): MaxHeatRejectionW * CoolantPumpRate when extended and operating.
        return 0f;
    }

    /// <summary>Extends or stows the panel (flight_deploy_toggle).</summary>
    public void SetExtended(bool extended)
    {
        IsPanelExtended = extended;
    }
}
