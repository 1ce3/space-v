using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Result of RocketAssembler: the rocket Control will fly. RefCounted so it can travel through signals.</summary>
public partial class AssembledRocket : RefCounted
{
    public PartTree PartTree { get; set; }
    /// <summary>Stage 0 = launch, then one per separation depth.</summary>
    public List<StageGroup> Stages { get; set; } = new();
    /// <summary>Mass properties at launch (vehicle-local frame).</summary>
    public MassProperties MassProperties { get; set; }

    public AssembledRocket() { }

    public AssembledRocket(PartTree partTree, List<StageGroup> stages, MassProperties massProperties)
    {
        PartTree = partTree;
        Stages = stages ?? new List<StageGroup>();
        MassProperties = massProperties;
    }
}
