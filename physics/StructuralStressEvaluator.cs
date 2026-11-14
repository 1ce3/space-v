using System;
using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>
/// Load carried by every joint, checked against joint strength and part shear limits (guide B 4.4).
/// For the joint above child c with subtree S: Ft = m_S * a - F_ext(S), where a = (NetForce - Gravity) / TotalMass.
/// </summary>
public class StructuralStressEvaluator
{
    private readonly StressReport _report = new();
    private readonly Dictionary<RocketPart, Vector2> _externalLoad = new();
    private readonly Dictionary<RocketPart, float> _subtreeMass = new();
    private readonly Dictionary<RocketPart, Vector2> _subtreeLoad = new();
    private static readonly Comparison<StressViolation> WorstFirst = (x, y) => y.Ratio.CompareTo(x.Ratio);

    /// <summary>Computes joint loads from the tick result. Call after ExecutePhysicsStep and before CheckYieldLimits.</summary>
    public void EvaluateRecursiveLoad(PhysicsStepInput input, PhysicsStepOutput output)
    {
        _report.Violations.Clear();
        _report.MaxStressRatio = 0f;
        PartTree tree = input.Parts;
        if (tree?.RootPart == null || input.Mass.TotalMass <= 0f) return;

        Vector2 accel = (output.NetForce - output.GravityForce) / input.Mass.TotalMass;
        _externalLoad.Clear();
        _subtreeMass.Clear();
        _subtreeLoad.Clear();
        if (output.PartLoads != null)
        {
            foreach (PartLoad load in output.PartLoads)
            {
                _externalLoad.TryGetValue(load.Part, out Vector2 sum);
                _externalLoad[load.Part] = sum + load.Force;
            }
        }
        Accumulate(tree.RootPart);

        foreach (RocketPart child in tree.ActiveParts)
        {
            Joint joint = child.ParentJoint;
            if (joint == null || joint.IsSeparated || child.ParentPart == null || !_subtreeMass.ContainsKey(child)) continue;

            Vector2 transmitted = _subtreeMass[child] * accel - _subtreeLoad[child];
            float lateral = Mathf.Abs(transmitted.Rotated(-input.BodyRotationRad).X);

            bool separable = joint.Decouplers.Count > 0 || joint.IsSelfSeparating;
            if (separable && joint.Strength > 0f) Check(child, FailureCause.JointOverload, transmitted.Length() / joint.Strength);
            if (child.MaxShear > 0f) Check(child, FailureCause.Shear, lateral / child.MaxShear);
            // P1: Bending = lateral * distance(joint, subtree CoM) / MaxBending.
        }
        if (_report.Violations.Count > 1) _report.Violations.Sort(WorstFirst);
    }

    /// <summary>Report of the last EvaluateRecursiveLoad. The object is reused every tick.</summary>
    public StressReport CheckYieldLimits() => _report;

    private void Check(RocketPart part, FailureCause cause, float ratio)
    {
        if (ratio > _report.MaxStressRatio) _report.MaxStressRatio = ratio;
        if (ratio > 1f) _report.Violations.Add(new StressViolation { Part = part, Cause = cause, Ratio = ratio });
    }

    /// <summary>Post-order walk: mass and external force of each subtree.</summary>
    private void Accumulate(RocketPart part)
    {
        float mass = part.GetCurrentMass();
        _externalLoad.TryGetValue(part, out Vector2 load);
        foreach (RocketPart child in part.Children)
        {
            Accumulate(child);
            mass += _subtreeMass[child];
            load += _subtreeLoad[child];
        }
        _subtreeMass[part] = mass;
        _subtreeLoad[part] = load;
    }
}
