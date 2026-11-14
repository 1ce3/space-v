using System.Collections.Generic;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Connection between a parent and a child part in the tree, with the decouplers on its edges.</summary>
public class Joint
{
    public RocketPart ParentPart { get; set; }
    public RocketPart ChildPart { get; set; }
    /// <summary>Every touching edge between the two parts.</summary>
    public List<Edge> ContactEdges { get; } = new();
    /// <summary>Decouplers sitting on ContactEdges.</summary>
    public List<DecouplerPart> Decouplers { get; } = new();
    /// <summary>Fairing or parachute joint: cut by the part itself, not by staging.</summary>
    public bool IsSelfSeparating { get; set; }
    /// <summary>Depth of ChildPart from the pod (pod = 0).</summary>
    public int Depth { get; set; }
    /// <summary>Max load before JointOverload (N): sum of decoupler AttachmentStrength, or the fairing/parachute strength.</summary>
    public float Strength { get; set; }
    /// <summary>True once separated (each joint is cut only once).</summary>
    public bool IsSeparated { get; set; }

    /// <summary>A joint with no way to separate.</summary>
    public bool IsPermanent => !CanJettison();

    /// <summary>True when this joint can be cut by staging or by its own part.</summary>
    public bool CanJettison() => !IsSeparated && (Decouplers.Count > 0 || IsSelfSeparating);
}
