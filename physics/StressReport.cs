using System.Collections.Generic;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>One structural limit exceeded at the joint above Part.</summary>
public struct StressViolation
{
    /// <summary>Child part of the overloaded joint.</summary>
    public RocketPart Part;
    /// <summary>Shear, Bending or JointOverload.</summary>
    public FailureCause Cause;
    /// <summary>Load / limit (above 1 means broken).</summary>
    public float Ratio;
}

/// <summary>Result of StructuralStressEvaluator.CheckYieldLimits for one tick.</summary>
public class StressReport
{
    /// <summary>Largest load / limit ratio over all joints.</summary>
    public float MaxStressRatio { get; set; }
    /// <summary>Joints over their limit, worst first.</summary>
    public List<StressViolation> Violations { get; } = new();
}
