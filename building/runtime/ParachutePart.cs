using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Parachute.</summary>
public partial class ParachutePart : RocketPart
{
    /// <summary>Stowed, Deployed or Cut. Physics may set Broken in State when opened too fast.</summary>
    public ParachuteState ChuteState { get; set; }
    /// <summary>0..1 how far the deployed canopy has opened (physics advances it over InflationTimeS).</summary>
    public float InflationFraction { get; set; }

    public ParachutePartDefinition ChuteDefinition => Definition as ParachutePartDefinition;

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        ChuteState = ParachuteState.Stowed;
        InflationFraction = 0f;
    }

    /// <summary>Opens if Stowed and no fairing is left in the tree.</summary>
    public bool Deploy()
    {
        if (ChuteState != ParachuteState.Stowed || State == ComponentState.Broken) return false;
        if (Tree != null && Tree.GetParts<FairingPart>().Count > 0) return false;
        ChuteState = ParachuteState.Deployed;
        if (State == ComponentState.Idle) State = ComponentState.Active;
        return true;
    }

    /// <summary>Cuts the chute: Cut state, then severs itself (Jettison).</summary>
    public void Cut()
    {
        if (ChuteState == ParachuteState.Cut) return;
        ChuteState = ParachuteState.Cut;
        if (IsAttached) Tree.SeverSubtree(this, SeverCause.Jettison);
    }
}
