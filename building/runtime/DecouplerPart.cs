using System.Collections.Generic;
using Godot;
using RocketGame.Core;
using Side = RocketGame.Core.Side;

namespace RocketGame.Building;

/// <summary>Decoupler on a cell edge. TriggerSeparation cuts its joint once.</summary>
public partial class DecouplerPart : StructuralPart
{
    /// <summary>Edge it sits on (normalized to Top or Right).</summary>
    public Edge Edge { get; set; }
    /// <summary>Joint it can separate (set by RocketAssembler).</summary>
    public Joint Joint { get; set; }

    public DecouplerPartDefinition DecouplerDefinition => Definition as DecouplerPartDefinition;

    /// <summary>Midpoint of the edge, in cells.</summary>
    public override Vector2 GetGridPosition() => Edge.Side switch
    {
        Side.Top => new Vector2(Edge.Cell.X + 0.5f, Edge.Cell.Y + 1f),
        Side.Bottom => new Vector2(Edge.Cell.X + 0.5f, Edge.Cell.Y),
        Side.Left => new Vector2(Edge.Cell.X, Edge.Cell.Y + 0.5f),
        _ => new Vector2(Edge.Cell.X + 1f, Edge.Cell.Y + 0.5f),
    };

    /// <summary>Cuts Joint.ChildPart with the summed EjectionImpulse of the joint's decouplers.</summary>
    public void TriggerSeparation()
    {
        // TODO(A1): if Joint.CanJettison(): Tree.SeverSubtree(Joint.ChildPart, SeverCause.Jettison, sum of EjectionImpulse).
    }
}
