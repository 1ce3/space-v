using System;
using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using Side = RocketGame.Core.Side;

namespace RocketGame.Physics;

/// <summary>
/// Load carried by every joint, checked against joint strength and part shear limits (guide B 4.4).
/// For the joint above child c with subtree S: Ft = m_S * a - F_ext(S), where a = (NetForce - Gravity) / TotalMass.
/// P1: Bending = |lateral| * distance(joint, CoM of S) / MaxBending; the pod breaks (JointOverload) above MaxGForce * PodBreakGFactor.
/// </summary>
public class StructuralStressEvaluator
{
    private readonly StressReport _report = new();
    private readonly Dictionary<RocketPart, Vector2> _externalLoad = new();
    private readonly Dictionary<RocketPart, float> _subtreeMass = new();
    private readonly Dictionary<RocketPart, Vector2> _subtreeLoad = new();
    private readonly Dictionary<RocketPart, Vector2> _subtreeMoment = new();

    /// <summary>
    /// Share of a compressive joint load counted against joint strength (PhysicsSettings.JointCompressionFactor).
    /// A joint pushed together (lower stage thrusting into the upper one) is far stronger than one pulled apart;
    /// 1 counts compression like tension (old behaviour).
    /// </summary>
    public float JointCompressionFactor { get; set; } = 0.25f;

    /// <summary>The pod breaks above its MaxGForce times this (PhysicsSettings.PodBreakGFactor). 0 turns the check off.</summary>
    public float PodBreakGFactor { get; set; } = 1.5f;
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
        _subtreeMoment.Clear();
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
            if (separable && joint.Strength > 0f) Check(child, FailureCause.JointOverload, JointLoad(transmitted, input.BodyRotationRad, joint, child) / joint.Strength);
            if (child.GetEffectiveMaxShear() > 0f) Check(child, FailureCause.Shear, lateral / child.GetEffectiveMaxShear());
            if (child.GetEffectiveMaxBending() > 0f && _subtreeMass[child] > 0f)
            {
                Vector2 subtreeCom = _subtreeMoment[child] / _subtreeMass[child];
                float arm = JointPosition(joint, child).DistanceTo(subtreeCom);
                Check(child, FailureCause.Bending, lateral * arm / child.GetEffectiveMaxBending());
            }
        }

        // P1: the pod itself is crushed above MaxGForce * PodBreakGFactor.
        if (PodBreakGFactor > 0f && tree.RootPart is CommandPodPart pod && pod.PodDefinition?.MaxGForce > 0f)
        {
            float g = accel.Length() / FlightMath.G0;
            Check(pod, FailureCause.JointOverload, g / (pod.PodDefinition.MaxGForce * PodBreakGFactor));
        }
        if (_report.Violations.Count > 1) _report.Violations.Sort(WorstFirst);
    }

    /// <summary>
    /// Joint load (N): tension and the sideways part count fully, compression only by JointCompressionFactor.
    /// transmitted is the force the parent puts on the child subtree; pointing from parent to child means pushing.
    /// </summary>
    private float JointLoad(Vector2 transmitted, float bodyRotationRad, Joint joint, RocketPart child)
    {
        Vector2 local = transmitted.Rotated(-bodyRotationRad);
        Vector2 axis = child.GetLocalPosition() - (joint.ParentPart?.GetLocalPosition() ?? Vector2.Zero);
        if (axis.LengthSquared() < 1e-6f) return local.Length();
        axis = axis.Normalized();
        float along = local.Dot(axis);
        float sideways = (local - axis * along).Length();
        float axial = along > 0f ? along * Mathf.Clamp(JointCompressionFactor, 0f, 1f) : -along;
        return Mathf.Sqrt(axial * axial + sideways * sideways);
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
        Vector2 moment = part.GetLocalPosition() * mass;
        _externalLoad.TryGetValue(part, out Vector2 load);
        foreach (RocketPart child in part.Children)
        {
            Accumulate(child);
            mass += _subtreeMass[child];
            load += _subtreeLoad[child];
            moment += _subtreeMoment[child];
        }
        _subtreeMass[part] = mass;
        _subtreeLoad[part] = load;
        _subtreeMoment[part] = moment;
    }

    /// <summary>Mean of the contact edge midpoints (m, vehicle frame); falls back to halfway between the two parts.</summary>
    private static Vector2 JointPosition(Joint joint, RocketPart child)
    {
        float cs = RocketPart.GetCellSize();
        if (joint.ContactEdges.Count == 0 || joint.ParentPart == null)
            return ((joint.ParentPart?.GetLocalPosition() ?? child.GetLocalPosition()) + child.GetLocalPosition()) * 0.5f;

        Vector2 sum = Vector2.Zero;
        foreach (Edge edge in joint.ContactEdges)
        {
            Vector2 cell = new(edge.Cell.X, edge.Cell.Y);
            Vector2 mid = edge.Side switch
            {
                Side.Top => cell + new Vector2(0.5f, 1f),
                Side.Bottom => cell + new Vector2(0.5f, 0f),
                Side.Left => cell + new Vector2(0f, 0.5f),
                _ => cell + new Vector2(1f, 0.5f),
            };
            sum += mid * cs;
        }
        return sum / joint.ContactEdges.Count;
    }
}
