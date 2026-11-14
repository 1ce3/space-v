using System.Collections.Generic;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Connection between a parent part and one child part in the part tree.</summary>
public class Joint
{
    /// <summary>Part closer to the pod.</summary>
    public RocketPart ParentPart { get; set; }

    /// <summary>Part farther from the pod.</summary>
    public RocketPart ChildPart { get; set; }

    /// <summary>Every cell edge where the two parts touch.</summary>
    public List<Edge> ContactEdges { get; } = new();

    /// <summary>Decouplers placed on the contact edges.</summary>
    public List<DecouplerPart> Decouplers { get; } = new();

    /// <summary>True for fairing and parachute joints, which separate on their own command.</summary>
    public bool IsSelfSeparating { get; set; }

    /// <summary>Force the joint carries before it breaks, in N.</summary>
    public float Strength { get; set; }

    /// <summary>True when the joint can never be separated on command (no decoupler and not self-separating).</summary>
    public bool IsPermanent => !IsSelfSeparating && Decouplers.Count == 0;

    /// <summary>True when the joint can be separated on command.</summary>
    public bool CanJettison() => !IsPermanent;
}
