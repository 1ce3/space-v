using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Nose fairing.</summary>
public partial class FairingPart : RocketPart
{
    /// <summary>Parts it protects when nose-first (chain of parents up to the pod).</summary>
    public List<RocketPart> ShieldedParts { get; } = new();

    public FairingPartDefinition FairingDefinition => Definition as FairingPartDefinition;

    /// <summary>Unshields ShieldedParts and severs itself (Jettison).</summary>
    public void Jettison()
    {
        if (!IsAttached) return;
        foreach (RocketPart part in ShieldedParts) part.IsShielded = false;
        Tree.SeverSubtree(this, SeverCause.Jettison);
    }
}
