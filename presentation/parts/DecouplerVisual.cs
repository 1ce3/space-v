using Godot;
using RocketGame.Building;
using RocketGame.Core;
using Side = RocketGame.Core.Side;

namespace RocketGame.Presentation;

/// <summary>Decoupler ring; flash on separation.</summary>
public partial class DecouplerVisual : PartVisual
{
    /// <summary>Lies flat on Top edges; stands up on Left/Right edges (side boosters).</summary>
    public override void Bind(RocketPart part)
    {
        base.Bind(part);
        if (MeshRoot != null && part is DecouplerPart decoupler && (decoupler.Edge.Side == Side.Right || decoupler.Edge.Side == Side.Left))
            MeshRoot.RotationDegrees = new Vector3(0f, 0f, 90f);
    }

    public void PlaySeparation()
    {
        // TODO(A8)
    }
}
