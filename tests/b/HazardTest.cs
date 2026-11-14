using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Physics;
using RocketGame.Research;

namespace RocketGame.Tests.B;

/// <summary>
/// Sandbox for task B6 (guide B 4.6 and B6). Uses the Mars stack (A4) and a copy of the physics settings where the
/// random hazards always fire, then checks:
/// 1. HazardMonitor: enable mask, once until Acknowledge, MaxQ, G limit, flameout pass-through, overheat, cryo leak and its drain,
///    debris warning (not dodged = hit, dodged = no hit);
/// 2. PowerSolver.Step: brownout switches avionics off, power back switches them on;
/// 3. StructuralStressEvaluator: Bending under a side load, pod crushed above MaxGForce * PodBreakGFactor;
/// 4. CruiseSimulator: daily DebrisWarning / SeuGlitch, once until Acknowledge.
/// </summary>
public partial class HazardTest : Node
{
    /// <summary>physics_settings.tres; the test works on a copy.</summary>
    [Export] public PhysicsSettings Settings { get; set; }
    /// <summary>seg_cruise.tres (duration and bodies for the cruise part).</summary>
    [Export] public MissionSegmentDefinition CruiseSegment { get; set; }
    /// <summary>earth_scaled.tres.</summary>
    [Export] public CelestialBodyDefinition Earth { get; set; }
    /// <summary>mars_scaled.tres.</summary>
    [Export] public CelestialBodyDefinition Mars { get; set; }
    /// <summary>Quit after the test so the log is flushed.</summary>
    [Export] public bool QuitWhenDone { get; set; } = true;

    private const FlightEventMask AllHazards = (FlightEventMask)0x3FF;
    private readonly List<RocketPart> _parts = new();
    private int _ok, _fail;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Settings, nameof(Settings))) return;
        CallDeferred(nameof(RunAll));
    }

    private void RunAll()
    {
        var forced = (PhysicsSettings)Settings.Duplicate();
        forced.CryoLeakChancePerSec = 1f;
        forced.DebrisChancePerSec = 1f;
        forced.DebrisChancePerDay = 1f;
        forced.SeuChancePerDay = 1f;

        TestMonitor(forced);
        TestPower();
        TestStress(forced);
        TestCruise(forced);

        foreach (RocketPart part in _parts)
            if (IsInstanceValid(part)) part.Free();
        GD.Print($"[B6] Done: {_ok} OK, {_fail} FAIL.");
        if (QuitWhenDone) GetTree().Quit();
    }

    // ------------------------------------------------------------------ 1. HazardMonitor

    private void TestMonitor(PhysicsSettings forced)
    {
        GD.Print("[B6] --- HazardMonitor");
        PartTree tree = NewMarsStack();
        if (tree == null) return;
        PhysicsStepInput input = MakeInput(tree);
        var monitor = new HazardMonitor(forced, 1);

        // Mask: everything except GLimitExceeded and the random ones, so the first checks are deterministic.
        monitor.Configure(new MissionSegmentDefinition { EnabledHazards = FlightEventMask.MaxQExceeded | FlightEventMask.ComponentOverheat | FlightEventMask.EngineFlameout });
        var output = new PhysicsStepOutput { DynamicPressure = 50000f, CurrentGForce = 20f, AltitudeMeters = 1000f };
        List<FlightEvent> events = monitor.Evaluate(input, output);
        Check("MaxQExceeded fires above MaxQLimit", Has(events, FlightEventType.MaxQExceeded));
        Check("GLimitExceeded stays quiet when the segment disables it", !Has(events, FlightEventType.GLimitExceeded));
        Check("MaxQExceeded fires only once", !Has(monitor.Evaluate(input, output), FlightEventType.MaxQExceeded));
        monitor.Acknowledge(FlightEventType.MaxQExceeded);
        Check("MaxQExceeded fires again after Acknowledge", Has(monitor.Evaluate(input, output), FlightEventType.MaxQExceeded));

        var flameout = new List<FlightEvent> { new() { Type = FlightEventType.EngineFlameout, Severity = 1f, SourceId = "engine_lv2" } };
        output.Thermal = new ThermalStepResult { OverheatedComponents = new List<string> { "engine_lv2" } };
        events = monitor.Evaluate(input, output, flameout);
        Check("EngineFlameout from the solver passes through", Has(events, FlightEventType.EngineFlameout));
        Check("ComponentOverheat fires", Has(events, FlightEventType.ComponentOverheat));

        // All hazards on, random ones forced.
        monitor.Configure(new MissionSegmentDefinition { EnabledHazards = AllHazards });
        output = new PhysicsStepOutput { CurrentGForce = 9f, AltitudeMeters = 1000f };
        events = monitor.Evaluate(input, output);
        Check("GLimitExceeded fires above the pod MaxGForce (8)", Has(events, FlightEventType.GLimitExceeded));
        FlightEvent leak = Find(events, FlightEventType.CryoLeak);
        Check($"CryoLeak fires on a cryo tank ({leak.SourceId})", leak.SourceId == "tank_h1" || leak.SourceId == "tank_lm1");
        Check("no debris warning below 80 km", !Has(events, FlightEventType.DebrisWarning));
        float lost = monitor.ApplyLeaks(tree, 1f);
        Check($"leak drains {lost:0.0} kg in 1 s (expect {forced.CryoLeakRateKgPerSec:0.0})", Mathf.IsEqualApprox(lost, forced.CryoLeakRateKgPerSec));
        foreach (FuelTankPart tank in tree.GetParts<FuelTankPart>()) tank.SealLeak();
        Check("SealLeak stops the drain", monitor.ApplyLeaks(tree, 1f) == 0f);

        // Debris, not dodged.
        output.AltitudeMeters = 100000f;
        input.LinearVelocity = new Vector2(2000f, 0f);
        Check("DebrisWarning fires above 80 km", Has(monitor.Evaluate(input, output), FlightEventType.DebrisWarning));
        bool hit = RunDebris(monitor, input, output, 0f);
        Check($"no dodge: debris hits after {forced.DebrisWarningSeconds:0} s", hit);

        // Debris, dodged by changing horizontal speed by 10 m/s.
        monitor.Acknowledge(FlightEventType.DebrisWarning);
        Check("second DebrisWarning after Acknowledge", Has(monitor.Evaluate(input, output), FlightEventType.DebrisWarning));
        hit = RunDebris(monitor, input, output, 10f);
        Check("dodged: no hit", !hit);
    }

    private bool RunDebris(HazardMonitor monitor, PhysicsStepInput input, PhysicsStepOutput output, float dodgeMps)
    {
        input.DeltaTime = 0.1f;
        input.LinearVelocity = new Vector2(2000f + dodgeMps, 0f);
        for (int i = 0; i < 120; i++)
        {
            monitor.Evaluate(input, output);
            if (monitor.ConsumeDebrisHit()) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ 2. Power in flight

    private void TestPower()
    {
        GD.Print("[B6] --- PowerSolver.Step");
        PartTree tree = NewMarsStack();
        CommandPodPart pod = PowerSolver.FindPod(tree);
        if (pod == null) return;
        var solver = new PowerSolver(Settings);

        PowerStepResult r = solver.Step(tree, 0f, 1361f, 48f * 3600f); // 48 h with the solar array folded.
        GD.Print($"[B6] Folded array: supply {r.SupplyW:0} W, demand {r.DemandW:0} W, battery {r.BatteryFraction:P0}");
        Check("brownout when the battery runs out", r.IsBrownout);
        Check("avionics switched off", pod.Modules.TrueForAll(m => m.Definition?.SlotType != ModuleSlotType.Avionics || !m.IsPowered));

        foreach (SolarArrayModule solar in pod.GetModules<SolarArrayModule>()) solar.SetDeployed(true);
        r = solver.Step(tree, 0f, 1361f, 3600f);
        GD.Print($"[B6] Deployed array: supply {r.SupplyW:0} W, demand {r.DemandW:0} W, battery {r.BatteryFraction:P0}");
        Check("power back: no brownout", !r.IsBrownout);
        Check("avionics switched on again", pod.Modules.TrueForAll(m => m.Definition?.SlotType != ModuleSlotType.Avionics || m.IsPowered));
    }

    // ------------------------------------------------------------------ 3. Bending and pod G

    private void TestStress(PhysicsSettings settings)
    {
        GD.Print("[B6] --- Bending and pod G limit");
        PartTree tree = NewMarsStack();
        if (tree == null) return;
        PhysicsStepInput input = MakeInput(tree);
        float mass = input.Mass.TotalMass;
        var evaluator = new StructuralStressEvaluator { PodBreakGFactor = settings.PodBreakGFactor };
        GD.Print($"[B6] Mars stack {mass:0} kg");

        // 11 G sideways: below the pod limit (12 G), enough to bend the long stack.
        evaluator.EvaluateRecursiveLoad(input, new PhysicsStepOutput { NetForce = new Vector2(mass * FlightMath.G0 * 11f, 0f) });
        StressReport report = evaluator.CheckYieldLimits();
        StressViolation bend = report.Violations.Find(v => v.Cause == FailureCause.Bending);
        Check($"Bending under 11 G side load (worst {bend.Part?.ModelId}, ratio {bend.Ratio:0.00})", bend.Part != null);
        Check("pod holds at 11 G", report.Violations.Find(v => v.Part is CommandPodPart).Part == null);

        evaluator.EvaluateRecursiveLoad(input, new PhysicsStepOutput { NetForce = new Vector2(0f, mass * FlightMath.G0 * 13f) });
        StressViolation crush = evaluator.CheckYieldLimits().Violations.Find(v => v.Part is CommandPodPart);
        Check($"pod breaks at 13 G (limit {8f * settings.PodBreakGFactor:0} G), cause {crush.Cause}", crush.Part != null && crush.Cause == FailureCause.JointOverload);
    }

    // ------------------------------------------------------------------ 4. Cruise daily hazards

    private void TestCruise(PhysicsSettings forced)
    {
        GD.Print("[B6] --- Cruise daily hazards");
        PartTree tree = NewMarsStack();
        if (tree == null || CruiseSegment == null) return;
        var segment = (MissionSegmentDefinition)CruiseSegment.Duplicate();
        segment.EnabledHazards = AllHazards;
        var sim = new CruiseSimulator { Settings = forced };
        sim.Begin(tree, segment, Earth, Mars);

        CruiseStepResult day = sim.StepDay();
        Check("day 1: DebrisWarning", Has(day.Events, FlightEventType.DebrisWarning));
        FlightEvent seu = Find(day.Events, FlightEventType.SeuGlitch);
        Check($"day 1: SeuGlitch on {seu.SourceId}", seu.Type == FlightEventType.SeuGlitch);
        day = sim.StepDay();
        Check("day 2: nothing new before Acknowledge", !Has(day.Events, FlightEventType.DebrisWarning) && !Has(day.Events, FlightEventType.SeuGlitch));
        sim.Acknowledge(FlightEventType.DebrisWarning);
        Check("day 3: DebrisWarning again after Acknowledge", Has(sim.StepDay().Events, FlightEventType.DebrisWarning));

        segment.EnabledHazards = FlightEventMask.None;
        var quiet = new CruiseSimulator { Settings = forced };
        quiet.Begin(NewMarsStack(), segment, Earth, Mars);
        Check("segment without hazards: no events", quiet.StepDay().Events.Count == 0);
    }

    // ------------------------------------------------------------------ helpers

    private PartTree NewMarsStack()
    {
        AssembledRocket rocket = TestRocketFactory.CreateMarsStack();
        if (rocket?.PartTree == null)
        {
            GD.PushError("[B6] TestRocketFactory.CreateMarsStack() returned nothing (is the PartCatalog autoload loaded?)");
            return null;
        }
        _parts.AddRange(rocket.PartTree.ActiveParts);
        return rocket.PartTree;
    }

    private PhysicsStepInput MakeInput(PartTree tree) => new()
    {
        Parts = tree,
        Mass = MassCenterCalculator.Calculate(tree.ActiveParts),
        Position = new Vector2(0f, (Earth?.Radius ?? 600000f) + 1000f),
        DeltaTime = 1f / 60f,
    };

    private static bool Has(List<FlightEvent> events, FlightEventType type) => events != null && events.Exists(e => e.Type == type);

    private static FlightEvent Find(List<FlightEvent> events, FlightEventType type) =>
        events?.Find(e => e.Type == type) ?? new FlightEvent { Type = (FlightEventType)(-1), SourceId = "" };

    private void Check(string label, bool ok)
    {
        if (ok) _ok++;
        else _fail++;
        GD.Print($"[B6] {(ok ? "OK  " : "FAIL")} {label}");
    }
}
