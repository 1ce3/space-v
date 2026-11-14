using RocketGame.Building;

namespace RocketGame.Physics;

/// <summary>Load carried by every joint, checked against joint strength and part shear limits (guide B 4.4).</summary>
public class StructuralStressEvaluator
{
    private readonly StressReport _report = new();

    /// <summary>Computes joint loads from the tick result. Call before CheckYieldLimits.</summary>
    public void EvaluateRecursiveLoad(PhysicsStepInput input, PhysicsStepOutput output)
    {
        _report.Violations.Clear();
        _report.MaxStressRatio = 0f;
        // TODO(B3): per joint, Ft = m_subtree * a - F_ext; JointOverload and Shear checks.
    }

    /// <summary>Report of the last EvaluateRecursiveLoad. The object is reused every tick.</summary>
    public StressReport CheckYieldLimits() => _report;
}
