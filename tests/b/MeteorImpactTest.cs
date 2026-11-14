using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Physics;
using RocketGame.Research;

namespace RocketGame.Tests.B;

/// <summary>
/// Test for task B12 (PHASE2_PLAN.md 5.6): MeteorImpact.Resolve on a tank, the pod and an engine at
/// below / above / 3 times the impact tolerance, steel raising the threshold, fuel and pressure sensors,
/// hull breach ticking in flight and in the cruise, sealing with SealLeak.
/// </summary>
public partial class MeteorImpactTest : Node
{
    [Export] public PhysicsSettings Settings { get; set; }
    [Export] public MissionSegmentDefinition CruiseSegment { get; set; }
    [Export] public CelestialBodyDefinition Earth { get; set; }
    [Export] public CelestialBodyDefinition Mars { get; set; }
    [Export] public bool QuitWhenDone { get; set; } = true;

    private readonly List<RocketPart> _parts = new();
    private int _ok, _fail;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Settings, nameof(Settings))) return;
        CallDeferred(nameof(RunAll));
    }

    private void RunAll()
    {
        TestTank();
        TestPod();
        TestEngineAndBreak();
        TestCruise();

        foreach (RocketPart part in _parts)
            if (IsInstanceValid(part)) part.Free();
        GD.Print($"[B12] Done: {_ok} OK, {_fail} FAIL.");
        if (QuitWhenDone) GetTree().Quit();
    }

    private void TestTank()
    {
        GD.Print("[B12] --- Tank");
        PartTree tree = NewMarsStack();
        FuelTankPart tank = tree?.GetParts<FuelTankPart>().Find(t => t.Definition != null);
        if (tank == null) { Check("Mars stack has a tank", false); return; }
        float tol = tank.GetEffectiveImpactToleranceMps();

        MeteorImpactResult r = MeteorImpact.Resolve(tank, tol * 0.9f, Settings, null);
        Check($"below tolerance ({tol:0} m/s): scratch, no leak", r.Outcome == MeteorImpactOutcome.Scratch && tank.LeakRateKgPerSec == 0f);

        r = MeteorImpact.Resolve(tank, tol * 1.5f, Settings, null);
        Check($"above tolerance: leak at {Settings.MeteorLeakRateKgPerSec} kg/s, CryoLeak event",
            r.Outcome == MeteorImpactOutcome.Leak && Mathf.IsEqualApprox(tank.LeakRateKgPerSec, Settings.MeteorLeakRateKgPerSec)
            && r.Event.Type == FlightEventType.CryoLeak && r.Event.SourceId == tank.ModelId);
        tank.SealLeak();

        tank.Material = new MaterialDefinition { MaterialId = "mat_test_steel", ImpactToleranceMultiplier = 1.5f };
        r = MeteorImpact.Resolve(tank, tol * 1.2f, Settings, null);
        Check("steel (x1.5) tank only scratched at 1.2 x base tolerance", r.Outcome == MeteorImpactOutcome.Scratch && tank.LeakRateKgPerSec == 0f);
        tank.Material = null;

        CommandPodPart pod = PowerSolver.FindPod(tree);
        pod.AddModule(Sensor(InstrumentKind.FuelSensor, 0.5f));
        MeteorImpact.Resolve(tank, tol * 1.5f, Settings, null);
        Check("fuel sensor (0.5) halves the leak rate", Mathf.IsEqualApprox(tank.LeakRateKgPerSec, Settings.MeteorLeakRateKgPerSec * 0.5f));
        Check("C9 contract Resolve(part, speed) reports damage", MeteorImpact.Resolve(tank, tol * 1.5f));
    }

    private void TestPod()
    {
        GD.Print("[B12] --- Pod hull breach");
        PartTree tree = NewMarsStack();
        CommandPodPart pod = PowerSolver.FindPod(tree);
        if (pod == null) { Check("Mars stack has a pod", false); return; }
        float tol = pod.GetEffectiveImpactToleranceMps();

        MeteorImpactResult r = MeteorImpact.Resolve(pod, tol * 1.5f, Settings, null);
        Check("above tolerance: HullBreach", r.Outcome == MeteorImpactOutcome.HullBreach && pod.IsHullBreached
            && Mathf.IsEqualApprox(pod.HullBreachTimer, Settings.HullBreachFailSeconds));

        var rng = new RandomNumberGenerator { Seed = 4 };
        float interval = Settings.HullBreachFailSeconds;
        Check("no module lost before the interval", MeteorImpact.TickHullBreach(pod, interval * 0.5f, interval, rng).Length == 0);
        string lost = MeteorImpact.TickHullBreach(pod, interval * 0.6f, interval, rng);
        Check($"one module lost after the interval ({lost})", lost.Length > 0 && pod.Modules.FindAll(m => m.State == ComponentState.Broken).Count == 1);
        pod.SealHullBreach();
        Check("sealed: nothing more breaks", MeteorImpact.TickHullBreach(pod, interval * 5f, interval, rng).Length == 0 && !pod.IsHullBreached);

        tree = NewMarsStack();
        pod = PowerSolver.FindPod(tree);
        pod.AddModule(Sensor(InstrumentKind.PressureSensor, 0.5f));
        MeteorImpact.Resolve(pod, tol * 1.5f, Settings, null);
        Check("pressure sensor (0.5) doubles the time between failures", Mathf.IsEqualApprox(pod.HullBreachTimer, Settings.HullBreachFailSeconds * 2f));
    }

    private void TestEngineAndBreak()
    {
        GD.Print("[B12] --- Engine and 3x");
        PartTree tree = NewMarsStack();
        LiquidEnginePart engine = tree?.GetParts<LiquidEnginePart>().Find(e => e.Definition != null);
        FuelTankPart tank = tree?.GetParts<FuelTankPart>().Find(t => t.Definition != null);
        if (engine == null || tank == null) { Check("Mars stack has an engine and a tank", false); return; }

        float tol = engine.GetEffectiveImpactToleranceMps();
        MeteorImpactResult r = MeteorImpact.Resolve(engine, tol * 1.5f, Settings, null);
        Check("engine above tolerance: broken", r.Outcome == MeteorImpactOutcome.EngineBroken && engine.State == ComponentState.Broken);

        float tankTol = tank.GetEffectiveImpactToleranceMps();
        r = MeteorImpact.Resolve(tank, tankTol * (Settings.MeteorBreakFactor + 0.1f), Settings, null);
        Check($"above {Settings.MeteorBreakFactor} x tolerance: part broken", r.Outcome == MeteorImpactOutcome.PartBroken && tank.State == ComponentState.Broken);
    }

    private void TestCruise()
    {
        GD.Print("[B12] --- Hull breach in the cruise");
        if (CruiseSegment == null) return;
        PartTree tree = NewMarsStack();
        CommandPodPart pod = PowerSolver.FindPod(tree);
        if (pod == null) return;
        foreach (SolarArrayModule solar in pod.GetModules<SolarArrayModule>()) solar.SetDeployed(true);
        var segment = (MissionSegmentDefinition)CruiseSegment.Duplicate();
        segment.EnabledHazards = FlightEventMask.None;

        var sim = new CruiseSimulator { Settings = Settings, HazardSeed = 9 };
        sim.Begin(tree, segment, Earth, Mars);
        MeteorImpact.Resolve(pod, pod.GetEffectiveImpactToleranceMps() * 1.5f, Settings, null);
        pod.HullBreachTimer = Settings.CruiseHullBreachFailDays; // cruise unit: days
        Check("HasLeak covers the hull breach", sim.HasLeak);

        int days = Mathf.CeilToInt(Settings.CruiseHullBreachFailDays);
        int damaged = 0;
        for (int d = 0; d < days; d++) damaged += sim.StepDay().DamagedModules.Count;
        Check($"a module breaks after {days} days", damaged == 1);
        sim.SealLeak();
        int after = 0;
        for (int d = 0; d < days * 2; d++) after += sim.StepDay().DamagedModules.Count;
        Check("SealLeak stops the hull breach", after == 0 && !sim.HasLeak);
    }

    private static PodModule Sensor(InstrumentKind kind, float reduction) => PodModule.Create(new InstrumentModuleDefinition
    {
        ModelId = $"sensor_test_{kind}", Kind = kind, LeakReduction = reduction, DryMass = 1f, SlotType = ModuleSlotType.Sensor,
    });

    private PartTree NewMarsStack()
    {
        AssembledRocket rocket = TestRocketFactory.CreateMarsStack();
        if (rocket?.PartTree == null)
        {
            GD.PushError("[B12] TestRocketFactory.CreateMarsStack() returned nothing (is the PartCatalog autoload loaded?)");
            return null;
        }
        _parts.AddRange(rocket.PartTree.ActiveParts);
        return rocket.PartTree;
    }

    private void Check(string label, bool ok)
    {
        if (ok) _ok++;
        else _fail++;
        GD.Print($"[B12] {(ok ? "OK  " : "FAIL")} {label}");
    }
}
