using System.Collections.Generic;

using Godot;
using RocketGame.Core;

namespace RocketGame.Building;


/// <summary>
/// View over the runtime part tree (root = command pod). Cutting happens only through SeverSubtree,
/// called by StagingManager (Jettison) or StructuralBreakHandler (Break). Never reparents nodes.
/// </summary>
public partial class PartTree : RefCounted
{
    /// <summary>Emitted after a cut. part keeps its ParentJoint so listeners can find the parent.</summary>
    [Signal] public delegate void PartSeveredEventHandler(RocketPart part, SeverCause cause, float impulseNs);

    /// <summary>The command pod.</summary>
    public RocketPart RootPart { get; set; }

    /// <summary>Every part still attached, including decouplers.</summary>
    public List<RocketPart> ActiveParts { get; } = new();

    /// <summary>Attached parts of type T.</summary>
    public List<T> GetParts<T>() where T : RocketPart
    {
        var result = new List<T>();
        foreach (RocketPart part in ActiveParts)
            if (part is T typed) result.Add(typed);
        return result;
    }

    /// <summary>part and every descendant (no decouplers).</summary>
    public List<RocketPart> GetSubtree(RocketPart part)
    {
        // TODO(A1): depth-first over Children.
        return new List<RocketPart> { part };
    }

    /// <summary>Current mass of part and its descendants (kg).</summary>
    public float GetSubtreeMass(RocketPart part)
    {
        float mass = 0f;
        foreach (RocketPart p in GetSubtree(part)) mass += p.GetCurrentMass();
        return mass;
    }

    /// <summary>Detaches part and its subtree, removes them and the joint's decouplers from ActiveParts, emits PartSevered.</summary>
    public void SeverSubtree(RocketPart part, SeverCause cause, float impulseNs = 0f)
    {
        // TODO(A1): remove from parent's Children (keep part.ParentJoint), mark joint separated,
        //           remove subtree and joint decouplers from ActiveParts, then emit.
        EmitSignal(SignalName.PartSevered, part, (int)cause, impulseNs);
    }

}
