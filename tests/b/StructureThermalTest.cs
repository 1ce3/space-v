using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Physics;
using RocketGame.Research;
using Side = RocketGame.Core.Side;

namespace RocketGame.Tests.B;

/// <summary>
/// Sandbox for task B3 (guide B section 3, "Xong khi"):
/// 1. a booster joint with AttachmentStrength 1 000 N breaks (JointOverload), with the normal 300 000 N it holds;
/// 2. a heat shield entering Mars base first loses ablator;
/// 3. with MaxAblatorKg 5 the ablator runs out, the shield burns through and BreakTriggered fires with Thermal.
/// Rockets are built here because the Mars stack (A4) is not ready; numbers come from guide A section 7.
/// </summary>
public partial class StructureThermalTest : Node
{
    /// <summary>Physics node under test.</summary>
    [Export] public PhysicsCoordinator Coordinator { get; set; }
    /// <summary>seg_earth_ascent.tres.</summary>
    [Export] public MissionSegmentDefinition EarthSegment { get; set; }
    /// <summary>seg_mars_edl.tres.</summary>
    [Export] public MissionSegmentDefinition MarsSegment { get; set; }
    /// <summary>Integration step (s).</summary>
    [Export] public float StepSeconds { get; set; } = 1f / 60f;
    /// <summary>Weak decoupler strength for the break test (N).</summary>
    [Export] public float WeakAttachmentStrength { get; set; } = 1000f;
    /// <summary>Normal decoupler strength (decoupler_d1, N).</summary>
    [Export] public float NormalAttachmentStrength { get; set; } = 300000f;
    /// <summary>Ablator of the second entry run (kg).</summary>
    [Export] public float SmallAblatorKg { get; set; } = 5f;
    /// <summary>Quit after the test so the log is flushed.</summary>
    [Export] public bool QuitWhenDone { get; set; } = true;

    private readonly List<Node> _created = new();
    private string _lastBreak = "";

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Coordinator, nameof(Coordinator))) return;
        if (!NodeGuard.Require(this, EarthSegment, nameof(EarthSegment))) return;
        if (!NodeGuard.Require(this, MarsSegment, nameof(MarsSegment))) return;
        CallDeferred(nameof(RunAll));
    }

    private void RunAll()
    {
        Coordinator.Failures.BreakTriggered += OnBreakTriggered;
        Coordinator.Initialize(EarthSegment);
        RunBoosterJoint(WeakAttachmentStrength, expectBreak: true);
        RunBoosterJoint(NormalAttachmentStrength, expectBreak: false);

        Coordinator.Initialize(MarsSegment);
        RunEntry(80f, expectBurnThrough: false);
        RunEntry(SmallAblatorKg, expectBurnThrough: true);

        Coordinator.Failures.BreakTriggered -= OnBreakTriggered;
        foreach (Node node in _created) node.Free();
        if (QuitWhenDone) GetTree().Quit();
    }

    private void OnBreakTriggered(RocketPart part, FailureCause cause) => _lastBreak = $"{cause} at {part.ModelId}";

    // ---------- 1. Booster joint ----------

    private void RunBoosterJoint(float strength, bool expectBreak)
    {
        _lastBreak = "";
        PartTree tree = BuildBoosterRocket(strength, out LiquidEnginePart engine, out SolidBoosterPart booster);
        engine.Arm();
        engine.SetThrottle(1f);
        engine.Ignite();
        booster.Arm(); // solids light when armed

        var state = new FlightState { Position = new Vector2(0f, 10f), Rotation = 0f };
        float maxRatio = 0f;
        for (float t = 0f; t < 10f && _lastBreak == ""; t += StepSeconds)
        {
            PhysicsStepInput input = MakeInput(tree, state);
            PhysicsStepOutput output = Coordinator.ExecutePhysicsStep(input);
            Coordinator.StressEvaluator.EvaluateRecursiveLoad(input, output);
            StressReport report = Coordinator.StressEvaluator.CheckYieldLimits();
            maxRatio = Mathf.Max(maxRatio, report.MaxStressRatio);
            Coordinator.Failures.MonitorStressThresholds(report);
            Integrate(ref state, output, input.Mass.TotalMass);
        }
        bool broke = _lastBreak != "";
        bool booster_cut = !tree.ActiveParts.Contains(booster);
        string mark = broke == expectBreak && booster_cut == expectBreak ? "OK  " : "FAIL";
        GD.Print($"[Physics] {mark} booster joint {strength:F0} N: max stress ratio {maxRatio:F2}, " +
                 $"break = {(broke ? _lastBreak : "none")}, booster cut = {booster_cut} (expect {expectBreak})");
    }

    private PartTree BuildBoosterRocket(float strength, out LiquidEnginePart engine, out SolidBoosterPart booster)
    {
        var tree = new PartTree();
        var pod = Make<CommandPodPart>(tree, TestRocketFactory.GetPart("pod_mk1"), new Vector2I(1, 3));
        var tank = Make<FuelTankPart>(tree, TestRocketFactory.GetPart("tank_s1"), new Vector2I(1, 1));
        engine = Make<LiquidEnginePart>(tree, TestRocketFactory.GetPart("engine_lv1"), new Vector2I(1, 0));
        booster = Make<SolidBoosterPart>(tree, new SolidBoosterPartDefinition
        {
            ModelId = "booster_s1", PartGroup = PartGroup.SolidBooster, DryMass = 300f, Footprint = new Vector2I(1, 3),
            MaxThrustVac = 130000f, IspVacuum = 250f, IspSeaLevel = 230f, PropellantMass = 1500f, SideArea = 12f,
        }, new Vector2I(2, 0));
        tree.RootPart = pod;

        PartTree.Link(pod, tank, new Joint { Depth = 1 });
        PartTree.Link(tank, engine, new Joint { Depth = 2 });

        var decoupler = Make<DecouplerPart>(tree, new DecouplerPartDefinition
        {
            ModelId = "decoupler_d1", DryMass = 30f, AttachmentStrength = strength, EjectionImpulse = 3000f, FrontalArea = 0.5f,
        }, new Vector2I(1, 1));
        decoupler.Edge = new Edge(new Vector2I(1, 1), Side.Right);
        var joint = new Joint { Depth = 2, Strength = strength };
        joint.Decouplers.Add(decoupler);
        decoupler.Joint = joint;
        PartTree.Link(tank, booster, joint);

        engine.FeedTanks.Add(tank);
        return tree;
    }

    // ---------- 2-3. Mars entry ----------

    private void RunEntry(float ablatorKg, bool expectBurnThrough)
    {
        _lastBreak = "";
        var tree = new PartTree();
        var pod = Make<CommandPodPart>(tree, TestRocketFactory.GetPart("pod_mk1"), new Vector2I(1, 1));
        pod.PayloadMassKg = 300f;
        var shield = Make<HeatShieldPart>(tree, new HeatShieldPartDefinition
        {
            ModelId = "heatshield_h1", PartGroup = PartGroup.HeatShield, DryMass = 200f, MaxAblatorKg = ablatorKg,
            HeatOfAblationJPerKg = 150000f, MaxSafeTempK = 2400f, DragCoefficient = 1.5f, FrontalArea = 6f, SideArea = 4f,
        }, new Vector2I(1, 0));
        tree.RootPart = pod;
        PartTree.Link(pod, shield, new Joint { Depth = 1 });
        shield.ShieldedParts.Add(pod);

        float angle = Mathf.DegToRad(MarsSegment.EntryAngleDeg);
        var state = new FlightState
        {
            Position = new Vector2(0f, MarsSegment.EntryAltitudeM),
            Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * MarsSegment.EntrySpeedMps,
        };
        float startAblator = shield.AblatorRemainingKg;
        float peakFlux = 0f;
        float peakTemp = shield.TempK;
        float t = 0f;
        GD.Print($"[Physics] === Mars entry, base first, ablator {ablatorKg:F0} kg ===");
        for (; t < 600f && _lastBreak == ""; t += StepSeconds)
        {
            // Hold base first: body +Y points against the velocity.
            state.Rotation = (-state.Velocity).Angle() - Mathf.Pi / 2f;
            PhysicsStepInput input = MakeInput(tree, state);
            PhysicsStepOutput output = Coordinator.ExecutePhysicsStep(input);
            Coordinator.Failures.MonitorThermalLimits(output.Thermal);
            peakFlux = Mathf.Max(peakFlux, output.HeatFlux);
            peakTemp = Mathf.Max(peakTemp, shield.TempK);
            Integrate(ref state, output, input.Mass.TotalMass);
            if (output.AltitudeMeters < 0f) break;
        }
        bool burned = _lastBreak.StartsWith("Thermal");
        bool ablated = shield.AblatorRemainingKg < startAblator;
        bool ok = ablated && burned == expectBurnThrough;
        GD.Print($"[Physics] {(ok ? "OK  " : "FAIL")} ablator {startAblator:F1} -> {shield.AblatorRemainingKg:F1} kg, " +
                 $"peak flux {peakFlux:F0} W/m2, peak shield temp {peakTemp:F0} K, t = {t:F0} s, " +
                 $"break = {(burned ? _lastBreak : "none")} (expect burn-through {expectBurnThrough})");
    }

    // ---------- helpers ----------

    private struct FlightState
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Rotation;
    }

    private PhysicsStepInput MakeInput(PartTree tree, FlightState state) => new()
    {
        Position = state.Position, LinearVelocity = state.Velocity, BodyRotationRad = state.Rotation,
        Mass = ComputeMass(tree), Parts = tree, DeltaTime = StepSeconds,
    };

    private void Integrate(ref FlightState state, PhysicsStepOutput output, float mass)
    {
        state.Velocity += output.NetForce / mass * StepSeconds;
        state.Position += state.Velocity * StepSeconds;
    }

    private T Make<T>(PartTree tree, PartDefinition def, Vector2I cell) where T : RocketPart, new()
    {
        var part = new T();
        part.Initialize(def);
        part.Cell = cell;
        tree.Add(part);
        _created.Add(part);
        return part;
    }

    private static MassProperties ComputeMass(PartTree tree)
    {
        float total = 0f;
        Vector2 weighted = Vector2.Zero;
        foreach (RocketPart part in tree.ActiveParts)
        {
            float m = part.GetCurrentMass();
            total += m;
            weighted += part.GetLocalPosition() * m;
        }
        return new MassProperties { TotalMass = total, CenterOfMass = total > 0f ? weighted / total : Vector2.Zero };
    }
}
