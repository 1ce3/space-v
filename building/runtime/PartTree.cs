using System.Collections.Generic;
using System.Linq;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>The tree of parts that make up the rocket. Emits PartSevered when a subtree is cut off.</summary>
public partial class PartTree : RefCounted
{
    /// <summary>Emitted after a subtree has been removed from ActiveParts. The part keeps its ParentJoint.</summary>
    [Signal] public delegate void PartSeveredEventHandler(RocketPart part, SeverCause cause, float impulseNs);

    /// <summary>The command pod.</summary>
    public RocketPart RootPart { get; set; }

    /// <summary>Every part still attached, including decouplers on intact joints.</summary>
    public List<RocketPart> ActiveParts { get; } = new();

    /// <summary>All active parts of one runtime type.</summary>
    public List<T> GetParts<T>() where T : RocketPart => ActiveParts.OfType<T>().ToList();

    /// <summary>Part and all its descendants in the tree (decouplers are not tree nodes, so they are not included).</summary>
    public List<RocketPart> GetSubtree(RocketPart part)
    {
        var result = new List<RocketPart>();
        if (part != null) CollectSubtree(part, result);
        return result;
    }

    /// <summary>Total current mass of the part and its descendants, in kg.</summary>
    public float GetSubtreeMass(RocketPart part)
    {
        float mass = 0f;
        foreach (RocketPart p in GetSubtree(part)) mass += p.GetCurrentMass();
        return mass;
    }

    /// <summary>
    /// Cuts the part and everything below it off the tree. Removes them, and the decouplers of their joints,
    /// from ActiveParts, then emits PartSevered. Does not reparent nodes: VehicleRoot does that on the signal.
    /// </summary>
    public void SeverSubtree(RocketPart part, SeverCause cause, float impulseNs = 0f)
    {
        if (part == null)
        {
            GD.PushError("[PartTree] SeverSubtree was called with a null part.");
            return;
        }

        if (part == RootPart)
        {
            GD.PushError("[PartTree] The root part cannot be severed.");
            return;
        }

        if (!ActiveParts.Contains(part)) return; // Already severed.

        List<RocketPart> subtree = GetSubtree(part);

        part.ParentPart?.Children.Remove(part);

        foreach (RocketPart p in subtree)
        {
            ActiveParts.Remove(p);
            if (p.ParentJoint == null) continue;
            foreach (DecouplerPart decoupler in p.ParentJoint.Decouplers) ActiveParts.Remove(decoupler);
        }

        EmitSignal(SignalName.PartSevered, part, (int)cause, impulseNs);
    }

    private static void CollectSubtree(RocketPart part, List<RocketPart> result)
    {
        result.Add(part);
        foreach (RocketPart child in part.Children) CollectSubtree(child, result);
    }
}
