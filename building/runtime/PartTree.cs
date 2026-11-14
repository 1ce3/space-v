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

    private readonly HashSet<RocketPart> _removeSet = new();
    private readonly List<RocketPart> _scratch = new();

    /// <summary>Adds a part to the tree (assembler / test factories). Sets part.Tree.</summary>
    public void Add(RocketPart part)
    {
        if (part == null || ActiveParts.Contains(part)) return;
        part.Tree = this;
        ActiveParts.Add(part);
    }

    /// <summary>Makes child a child of parent through joint (fills ParentPart, Children, ParentJoint, joint ends).</summary>
    public static void Link(RocketPart parent, RocketPart child, Joint joint)
    {
        child.ParentPart = parent;
        child.ParentJoint = joint;
        joint.ParentPart = parent;
        joint.ChildPart = child;
        if (!parent.Children.Contains(child)) parent.Children.Add(child);
    }

    /// <summary>Attached parts of type T.</summary>
    public List<T> GetParts<T>() where T : RocketPart
    {
        var result = new List<T>();
        foreach (RocketPart part in ActiveParts)
            if (part is T typed) result.Add(typed);
        return result;
    }

    /// <summary>
    /// part, every descendant, and the decouplers on part's ParentJoint and on every joint below it:
    /// exactly what leaves the rocket if part is cut.
    /// </summary>
    public List<RocketPart> GetSubtree(RocketPart part)
    {
        var result = new List<RocketPart>();
        GetSubtree(part, result);
        return result;
    }

    /// <summary>Allocation-free version: clears result and fills it.</summary>
    public void GetSubtree(RocketPart part, List<RocketPart> result)
    {
        result.Clear();
        if (part == null) return;
        CollectSubtree(part, result);
    }

    /// <summary>Current mass of GetSubtree(part) (kg).</summary>
    public float GetSubtreeMass(RocketPart part)
    {
        if (part == null) return 0f;
        float mass = part.GetCurrentMass();
        if (part.ParentJoint != null)
            foreach (DecouplerPart decoupler in part.ParentJoint.Decouplers) mass += decoupler.GetCurrentMass();
        foreach (RocketPart child in part.Children) mass += GetSubtreeMass(child);
        return mass;
    }

    /// <summary>
    /// Detaches part and its subtree: removes it from its parent's Children (ParentPart and ParentJoint are kept),
    /// marks the joint separated, removes GetSubtree(part) from ActiveParts, then emits PartSevered.
    /// Does nothing if part is not attached.
    /// </summary>
    public void SeverSubtree(RocketPart part, SeverCause cause, float impulseNs = 0f)
    {
        if (part == null || !ActiveParts.Contains(part)) return;

        GetSubtree(part, _scratch);
        part.ParentPart?.Children.Remove(part);
        if (part.ParentJoint != null) part.ParentJoint.IsSeparated = true;

        _removeSet.Clear();
        foreach (RocketPart p in _scratch) _removeSet.Add(p);
        ActiveParts.RemoveAll(p => _removeSet.Contains(p));
        _removeSet.Clear();

        EmitSignal(SignalName.PartSevered, part, (int)cause, impulseNs);
    }

    private static void CollectSubtree(RocketPart part, List<RocketPart> result)
    {
        result.Add(part);
        if (part.ParentJoint != null)
            foreach (DecouplerPart decoupler in part.ParentJoint.Decouplers)
                if (!result.Contains(decoupler)) result.Add(decoupler);
        foreach (RocketPart child in part.Children) CollectSubtree(child, result);
    }
}
