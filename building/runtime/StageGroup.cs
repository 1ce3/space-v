using System.Collections.Generic;

namespace RocketGame.Building;

/// <summary>One step of the staging sequence: what separates and which engines get armed.</summary>
public class StageGroup
{
    /// <summary>Position in the staging sequence (0 = first press of Stage).</summary>
    public int StageIndex { get; set; }

    /// <summary>Depth of the joints separated in this stage, measured from the pod (pod = 0).</summary>
    public int Depth { get; set; }

    /// <summary>Joints separated in this stage.</summary>
    public List<Joint> Joints { get; } = new();

    /// <summary>Parts that leave the rocket in this stage.</summary>
    public List<RocketPart> Parts { get; } = new();

    /// <summary>Engines armed in this stage.</summary>
    public List<EnginePart> EnginesToArm { get; } = new();
}
