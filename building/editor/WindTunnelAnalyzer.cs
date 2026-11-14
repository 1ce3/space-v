using System.Collections.Generic;
using Godot;
using RocketGame.Core;
using RocketGame.Physics;

namespace RocketGame.Building;

/// <summary>
/// Build-screen aero preview (A9, P2): runs B's AerodynamicSolver on the parts in Earth air at the test altitude.
/// Stability is read from the aero torque at an angle of attack (at least StabilityProbeDeg): restoring torque = stable.
/// </summary>
public class WindTunnelAnalyzer
{
    /// <summary>Settings used when none are passed.</summary>
    public const string DefaultSettingsPath = "res://data/settings/physics_settings.tres";
    /// <summary>Smallest angle used to judge stability (deg).</summary>
    public const float StabilityProbeDeg = 5f;

    /// <summary>Physics tuning (drag model, Mach curve). Loaded from DefaultSettingsPath when null.</summary>
    public PhysicsSettings Settings { get; set; }

    /// <summary>
    /// Aero forces and stability for the given flow. centerOfMass (vehicle-local m) defaults to the mass center of parts.
    /// The parts are put in a temporary PartTree for the solver; their own Tree is restored afterwards.
    /// </summary>
    public AeroReport Analyze(IReadOnlyList<RocketPart> parts, TestConditions conditions, Vector2? centerOfMass = null)
    {
        if (parts == null || parts.Count == 0) return default;
        Settings ??= ResourceLoader.Exists(DefaultSettingsPath) ? GD.Load<PhysicsSettings>(DefaultSettingsPath) : new PhysicsSettings();

        Vector2 com = centerOfMass ?? MassCenterCalculator.Calculate(parts).CenterOfMass;
        var report = new AeroReport { CenterOfMass = com, CenterOfPressure = com };
        float speed = Mathf.Max(0f, conditions.SpeedMps);
        if (speed <= 0f) return report;

        var tree = new PartTree();
        var oldTrees = new Dictionary<RocketPart, PartTree>();
        foreach (RocketPart part in parts)
        {
            if (part == null) continue;
            oldTrees[part] = part.Tree;
            tree.Add(part);
        }

        try
        {
            AtmosphereSample air = new USSA76AtmosphereModel().Sample(conditions.AltitudeM);
            var solver = new AerodynamicSolver(Settings);

            float aoa = Mathf.DegToRad(conditions.AngleOfAttackDeg);
            var velDir = new Vector2(Mathf.Sin(aoa), Mathf.Cos(aoa)); // where the vehicle goes, nose = +Y
            AeroResult result = solver.Solve(tree, -velDir * speed, air, com);
            report.DragN = -result.Force.Dot(velDir);
            report.LiftN = result.Force.Dot(new Vector2(velDir.Y, -velDir.X));

            // Flow from the +X side: a stable rocket turns its nose into it, i.e. clockwise = negative torque.
            float probe = Mathf.DegToRad(Mathf.Max(Mathf.Abs(conditions.AngleOfAttackDeg), StabilityProbeDeg));
            var probeDir = new Vector2(Mathf.Sin(probe), Mathf.Cos(probe));
            AeroResult tilted = solver.Solve(tree, -probeDir * speed, air, com);
            report.IsStable = tilted.TorqueZ < 0f;
            // Height where the side force acts (torque about the CoM = -(cp.y - com.y) * side force), so the margin agrees with IsStable.
            float side = tilted.Force.X;
            if (Mathf.Abs(side) > 1e-3f)
            {
                float cpY = com.Y - tilted.TorqueZ / side;
                report.CenterOfPressure = new Vector2(com.X, cpY);
                report.StabilityMarginM = com.Y - cpY;
            }
        }
        finally
        {
            foreach ((RocketPart part, PartTree old) in oldTrees) part.Tree = old;
        }
        return report;
    }
}
