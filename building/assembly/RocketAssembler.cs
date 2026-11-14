using System.Collections.Generic;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Turns a RocketDesign into the runtime rocket (A_BUILDING.md 5.3). Always creates fresh parts.</summary>
public class RocketAssembler
{
    /// <summary>Player-facing errors (Vietnamese). Empty = valid.</summary>
    public List<string> ValidateConnections(RocketDesign design)
    {
        // TODO(A4): rules of A_BUILDING.md 5.2.
        return new List<string>();
    }

    /// <summary>Creates parts, BFS from the pod, joints with decouplers. Sets Cell, Tree, ParentPart, ParentJoint.</summary>
    public PartTree AssembleRuntimeTree(RocketDesign design)
    {
        // TODO(A4)
        return new PartTree();
    }

    /// <summary>Creates PodModules from the design and installs them in the pod.</summary>
    public void InstallModules(RocketDesign design, CommandPodPart pod)
    {
        // TODO(A4)
    }

    /// <summary>Fills FeedTanks of each liquid engine (same propellant, not across decoupler joints).</summary>
    public void LinkFeedTanks(PartTree tree)
    {
        // TODO(A4)
    }

    /// <summary>Instances each part's visual scene (or the group placeholder) and binds it.</summary>
    public void AttachVisuals(PartTree tree)
    {
        // TODO(A5)
    }

    /// <summary>Stage 0 plus one stage per decoupler depth (deepest first), with EnginesToArm.</summary>
    public List<StageGroup> DeriveStages(PartTree tree)
    {
        // TODO(A4)
        return new List<StageGroup>();
    }

    /// <summary>Full pipeline: tree, modules, feed tanks, stages, shielding, visuals, mass properties.</summary>
    public AssembledRocket AssembleRocket(RocketDesign design)
    {
        // TODO(A4)
        PartTree tree = AssembleRuntimeTree(design);
        return new AssembledRocket(tree, DeriveStages(tree), MassCenterCalculator.Calculate(tree.ActiveParts));
    }
}
