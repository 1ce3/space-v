using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Parachute.</summary>
public partial class ParachutePart : RocketPart
{
    /// <summary>Stowed, Deployed or Cut. Physics may set Broken in State when opened too fast.</summary>
    public ParachuteState ChuteState { get; set; }

    public ParachutePartDefinition ChuteDefinition => Definition as ParachutePartDefinition;

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        ChuteState = ParachuteState.Stowed;
    }

    /// <summary>Opens if Stowed and no fairing is left in the tree.</summary>
    public bool Deploy()
    {
        // TODO(A1)
        return false;
    }

    /// <summary>Cuts the chute: Cut state, then severs itself (Jettison).</summary>
    public void Cut()
    {
        // TODO(A1)
    }
}
