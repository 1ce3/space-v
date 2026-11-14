using System.Collections.Generic;

namespace RocketGame.Building;

/// <summary>One press of the stage key: joints to separate and engines to arm.</summary>
public class StageGroup
{
    /// <summary>0 = launch (no joint).</summary>
    public int StageIndex { get; set; }
    /// <summary>Depth of the joints in this stage (0 for stage 0).</summary>
    public int Depth { get; set; }
    public List<Joint> Joints { get; } = new();
    /// <summary>Parts that leave the rocket when this stage fires.</summary>
    public List<RocketPart> Parts { get; } = new();
    public List<EnginePart> EnginesToArm { get; } = new();
}
