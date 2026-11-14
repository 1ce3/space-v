using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Physics;
using RocketGame.Research;

namespace RocketGame.Tests.B;

/// <summary>
/// Test for task B11 (PHASE2_PLAN.md 5.2-5.5), fixed seeds:
/// 1. CruiseEventDirector.Plan: 2..4 events, at least CruiseEventMinGapDays apart, inside the cruise, same seed = same plan;
/// 2. solar flare formula and damage (hull shielding, module resistance, powered down);
/// 3. instrument failure breaks exactly one module;
/// 4. a radiation sensor warns FlareWarningDays early, the flare day reports DamagedModules;
/// 5. PowerSolver.TryDrawEnergy;
/// 6. transfer-burn incidents: flameout costs extra delta-v, a leak drains fuel until SealLeak.
/// </summary>
public partial class CruiseEventTest : Node
{
    /// <summary>physics_settings.tres; the test works on copies.</summary>
    [Export] public PhysicsSettings Settings { get; set; }
    /// <summary>seg_cruise.tres.</summary>
    [Export] public MissionSegmentDefinition CruiseSegment { get; set; }
    [Export] public CelestialBodyDefinition Earth { get; set; }
    [Export] public CelestialBodyDefinition Mars { get; set; }
    /// <summary>Quit after the test so the log is flushed.</summary>
    [Export] public bool QuitWhenDone { get; set; } = true;

    private readonly List<RocketPart> _parts = new();
    private int _ok, _fail;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Settings, nameof(Settings))) return;
        if (!NodeGuard.Require(this, CruiseSegment, nameof(CruiseSegment))) return;
        CallDeferred(nameof(RunAll));
    }

    private void RunAll()
    {
        TestPlan();
        TestFlare();
        TestInstrumentFailure();
        TestWarning();
        TestDrawEnergy();
        TestTransferBurn();

        foreach (RocketPart part in _parts)
            if (IsInstanceValid(part)) part.Free();
        GD.Print($"[B11] Done: {_ok} OK, {_fail} FAIL.");
        if (QuitWhenDone) GetTree().Quit();
    }

    // ------------------------------------------------------------------ 1. plan

    private void TestPlan()
    {
        GD.Print("[B11] --- Plan");
        MissionSegmentDefinition segment = Segment(CruiseEventDirector.EventMask);
        int duration = Mathf.FloorToInt(segment.DurationDays);
        bool countOk = true, gapOk = true, rangeOk = true;
        var seen = new HashSet<FlightEventType>();
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var director = new CruiseEventDirector(Settings, seed);
            director.Plan(null, segment);
            IReadOnlyList<CruiseEventDirector.PlannedEvent> plan = director.Planned;
            countOk &= plan.Count >= Settings.CruiseEventsMin && plan.Count <= Settings.CruiseEventsMax;
            for (int i = 0; i < plan.Count; i++)
            {
                seen.Add(plan[i].Type);
                rangeOk &= plan[i].Day >= Settings.CruiseEventFirstDay && plan[i].Day <= duration - Settings.CruiseEventLastDayMargin;
                if (i > 0) gapOk &= plan[i].Day - plan[i - 1].Day >= Settings.CruiseEventMinGapDays;
            }
        }
        Check($"50 seeds: {Settings.CruiseEventsMin}..{Settings.CruiseEventsMax} events each", countOk);
        Check($"events at least {Settings.CruiseEventMinGapDays} days apart", gapOk);
        Check($"events between day {Settings.CruiseEventFirstDay} and {duration - Settings.CruiseEventLastDayMargin}", rangeOk);
        Check($"all three types appear ({string.Join(", ", seen)})", seen.Count == 3);

        var a = new CruiseEventDirector(Settings, 7);
        var b = new CruiseEventDirector(Settings, 7);
        a.Plan(null, segment);
        b.Plan(null, segment);
        bool same = a.Planned.Count == b.Planned.Count;
        for (int i = 0; same && i < a.Planned.Count; i++) same = a.Planned[i] == b.Planned[i];
        Check($"same seed, same plan ({PlanText(a)})", same);

        var none = new CruiseEventDirector(Settings, 7);
        none.Plan(null, Segment(FlightEventMask.None));
        Check("segment without event types: nothing planned", none.Planned.Count == 0);
    }

    // ------------------------------------------------------------------ 2. flare

    private void TestFlare()
    {
        GD.Print("[B11] --- Solar flare");
        var director = new CruiseEventDirector(Settings, 3);
        float p = director.FlareDamageChance(0.2f, 0.3f, false);
        float expected = Settings.FlareBaseDamageChance * 0.8f * 0.7f;
        Check($"flare chance = base x (1 - shielding) x (1 - resistance) ({p:0.000})", Mathf.IsEqualApprox(p, expected));
        Check("powered down: x FlarePoweredDownFactor",
            Mathf.IsEqualApprox(director.FlareDamageChance(0.2f, 0.3f, true), expected * Settings.FlarePoweredDownFactor));

        var sure = (PhysicsSettings)Settings.Duplicate();
        sure.FlareBaseDamageChance = 1f;
        PartTree tree = NewMarsStack();
        CommandPodPart pod = PowerSolver.FindPod(tree);
        if (pod == null) { Check("Mars stack has a pod", false); return; }
        foreach (PodModule m in pod.Modules) SetResistance(m, 0f);

        List<string> broken = new CruiseEventDirector(sure, 3).ApplySolarFlare(tree, null);
        Check($"base 1, no protection: every module breaks ({broken.Count}/{pod.Modules.Count})",
            broken.Count == pod.Modules.Count && pod.Modules.TrueForAll(m => m.State == ComponentState.Broken && !m.IsPowered));

        tree = NewMarsStack();
        pod = PowerSolver.FindPod(tree);
        pod.Material = new MaterialDefinition { MaterialId = "mat_test_lead", RadiationShielding = 1f };
        Check("hull shielding 1: nothing breaks", new CruiseEventDirector(sure, 3).ApplySolarFlare(tree, null).Count == 0);

        tree = NewMarsStack();
        pod = PowerSolver.FindPod(tree);
        foreach (PodModule m in pod.Modules) SetResistance(m, 1f);
        Check("module resistance 1: nothing breaks", new CruiseEventDirector(sure, 3).ApplySolarFlare(tree, null).Count == 0);

        var off = (PhysicsSettings)sure.Duplicate();
        off.FlarePoweredDownFactor = 0f;
        tree = NewMarsStack();
        pod = PowerSolver.FindPod(tree);
        foreach (PodModule m in pod.Modules) SetResistance(m, 0f);
        Check("powered down with factor 0: nothing breaks",
            new CruiseEventDirector(off, 3).ApplySolarFlare(tree, new HashSet<PodModule>(pod.Modules)).Count == 0);
    }

    // ------------------------------------------------------------------ 3. instrument failure

    private void TestInstrumentFailure()
    {
        GD.Print("[B11] --- Instrument failure");
        PartTree tree = NewMarsStack();
        CommandPodPart pod = PowerSolver.FindPod(tree);
        if (pod == null) return;
        string id = new CruiseEventDirector(Settings, 5).ApplyInstrumentFailure(tree);
        int broken = pod.Modules.FindAll(m => m.State == ComponentState.Broken).Count;
        Check($"one module breaks ({id})", broken == 1 && id.Length > 0);
    }

    // ------------------------------------------------------------------ 4. sensor warning through the simulator

    private void TestWarning()
    {
        GD.Print("[B11] --- Flare warning");
        var flareOnly = (PhysicsSettings)Settings.Duplicate();
        flareOnly.CruiseEventsMin = 1;
        flareOnly.CruiseEventsMax = 1;
        flareOnly.MeteorFieldWeight = 0f;
        flareOnly.InstrumentFailureWeight = 0f;
        flareOnly.FlareBaseDamageChance = 1f;

        PartTree tree = NewMarsStack();
        CommandPodPart pod = PowerSolver.FindPod(tree);
        if (pod == null) return;
        foreach (PodModule m in pod.Modules) SetResistance(m, 0f);
        foreach (SolarArrayModule solar in pod.GetModules<SolarArrayModule>()) solar.SetDeployed(true);
        var sensorDef = new InstrumentModuleDefinition
        {
            ModelId = "sensor_test_rad", Kind = InstrumentKind.RadiationSensor, FlareWarningDays = 3f, DryMass = 1f,
            RadiationResistance = 1f, SlotType = ModuleSlotType.Sensor,
        };
        PodModule sensor = PodModule.Create(sensorDef);
        pod.AddModule(sensor);

        var sim = new CruiseSimulator { Settings = flareOnly, HazardSeed = 11 };
        sim.Begin(tree, Segment(FlightEventMask.SolarFlare), Earth, Mars);
        if (sim.PlannedEvents.Count != 1) { Check("one flare planned", false); return; }
        int flareDay = sim.PlannedEvents[0].Day;

        int warnDay = -1;
        CruiseStepResult flare = default;
        for (int d = 1; d <= flareDay; d++)
        {
            CruiseStepResult step = sim.StepDay();
            foreach (FlightEvent e in step.Events)
            {
                if (e.Type != FlightEventType.SolarFlare) continue;
                if (e.Severity < 1f && warnDay < 0) warnDay = step.Day;
                if (e.Severity >= 1f) flare = step;
            }
        }
        Check($"sensor warns 3 days early (warning day {warnDay}, flare day {flareDay})", warnDay == flareDay - 3);
        Check($"flare day reports DamagedModules ({string.Join(", ", flare.DamagedModules ?? new List<string>())})",
            flare.Day == flareDay && flare.DamagedModules != null && flare.DamagedModules.Count == pod.Modules.Count - 1);
        Check("the hardened sensor survives", sensor.State != ComponentState.Broken);

        // Without the sensor: no warning.
        tree = NewMarsStack();
        var quiet = new CruiseSimulator { Settings = flareOnly, HazardSeed = 11 };
        quiet.Begin(tree, Segment(FlightEventMask.SolarFlare), Earth, Mars);
        bool warned = false;
        for (int d = 1; d < quiet.PlannedEvents[0].Day; d++)
            foreach (FlightEvent e in quiet.StepDay().Events) warned |= e.Type == FlightEventType.SolarFlare;
        Check("no sensor: no warning before the flare", !warned);
    }

    // ------------------------------------------------------------------ 5. energy

    private void TestDrawEnergy()
    {
        GD.Print("[B11] --- TryDrawEnergy");
        PartTree tree = NewMarsStack();
        CommandPodPart pod = PowerSolver.FindPod(tree);
        if (pod == null) return;
        float stored = 0f;
        foreach (BatteryModule b in pod.GetModules<BatteryModule>()) stored += b.StoredEnergyWh;
        if (stored <= 0f) { Check("Mars stack has charged batteries", false); return; }

        bool ok = PowerSolver.TryDrawEnergy(pod, stored * 0.5f);
        float after = 0f;
        foreach (BatteryModule b in pod.GetModules<BatteryModule>()) after += b.StoredEnergyWh;
        Check($"draw half ({stored:0} -> {after:0} Wh)", ok && Mathf.Abs(after - stored * 0.5f) < 0.01f);
        Check("drawing more than stored fails and takes nothing", !PowerSolver.TryDrawEnergy(pod, after + 1f) && Mathf.IsEqualApprox(Stored(pod), after));
    }

    private static float Stored(CommandPodPart pod)
    {
        float s = 0f;
        foreach (BatteryModule b in pod.GetModules<BatteryModule>()) s += b.StoredEnergyWh;
        return s;
    }

    // ------------------------------------------------------------------ 6. transfer burn

    private void TestTransferBurn()
    {
        GD.Print("[B11] --- Transfer burn incidents");
        var always = (PhysicsSettings)Settings.Duplicate();
        always.TransferBurnEventChance = 1f;

        CruiseSimulator calm = NewCruiseReady(Settings, FlightEventMask.None);
        CruiseSimulator flame = NewCruiseReady(always, FlightEventMask.EngineFlameout);
        if (calm == null || flame == null) return;
        bool calmBurn = calm.ExecuteTransferBurn();
        bool flameBurn = flame.ExecuteTransferBurn();
        float calmLeft = calm.GetRemainingDeltaV(), flameLeft = flame.GetRemainingDeltaV();
        Check("both transfer burns succeed", calmBurn && flameBurn);
        Check("flameout reported", flame.LastBurnEvents.Count == 1 && flame.LastBurnEvents[0].Type == FlightEventType.EngineFlameout);
        Check($"relight costs extra delta-v ({calmLeft:0} vs {flameLeft:0} m/s)", flameLeft < calmLeft - 1f);
        Check("flameout also comes with the next day", flame.StepDay().Events.Exists(e => e.Type == FlightEventType.EngineFlameout));

        CruiseSimulator leak = NewCruiseReady(always, FlightEventMask.CryoLeak);
        if (leak == null) return;
        leak.ExecuteTransferBurn();
        Check($"leak started ({(leak.LastBurnEvents.Count > 0 ? leak.LastBurnEvents[0].SourceId : "-")})", leak.HasLeak);
        float dv0 = leak.GetRemainingDeltaV();
        leak.StepDay();
        float dv1 = leak.GetRemainingDeltaV();
        leak.SealLeak();
        leak.StepDay();
        float dv2 = leak.GetRemainingDeltaV();
        Check($"leak drains fuel, sealing stops it ({dv0:0} -> {dv1:0} -> {dv2:0} m/s)", dv1 < dv0 && Mathf.IsEqualApprox(dv1, dv2) && !leak.HasLeak);
    }

    /// <summary>Mars stack after ascent: boosters and the core stage dropped, engines up to the transfer stage armed.</summary>
    private CruiseSimulator NewCruiseReady(PhysicsSettings settings, FlightEventMask mask)
    {
        AssembledRocket rocket = TestRocketFactory.CreateMarsStack();
        if (rocket?.PartTree == null) { Check("Mars stack", false); return null; }
        _parts.AddRange(rocket.PartTree.ActiveParts);
        PartTree tree = rocket.PartTree;
        int last = Mathf.Min(2, rocket.Stages.Count - 1);
        for (int s = 1; s <= last; s++)
            foreach (Joint joint in rocket.Stages[s].Joints)
                if (joint.ChildPart != null && joint.ChildPart.IsAttached) tree.SeverSubtree(joint.ChildPart, SeverCause.Jettison);
        for (int s = 0; s <= last; s++)
            foreach (EnginePart engine in rocket.Stages[s].EnginesToArm)
                if (engine.IsAttached) engine.Arm();

        var sim = new CruiseSimulator { Settings = settings, HazardSeed = 21 };
        sim.Begin(tree, Segment(mask), Earth, Mars);
        return sim;
    }

    // ------------------------------------------------------------------ helpers

    private MissionSegmentDefinition Segment(FlightEventMask mask)
    {
        var segment = (MissionSegmentDefinition)CruiseSegment.Duplicate();
        segment.EnabledHazards = mask;
        return segment;
    }

    private static void SetResistance(PodModule module, float value)
    {
        if (module.Definition == null) return;
        var copy = (ModuleDefinition)module.Definition.Duplicate();
        copy.RadiationResistance = value;
        module.Initialize(copy);
    }

    private PartTree NewMarsStack()
    {
        AssembledRocket rocket = TestRocketFactory.CreateMarsStack();
        if (rocket?.PartTree == null)
        {
            GD.PushError("[B11] TestRocketFactory.CreateMarsStack() returned nothing (is the PartCatalog autoload loaded?)");
            return null;
        }
        _parts.AddRange(rocket.PartTree.ActiveParts);
        return rocket.PartTree;
    }

    private static string PlanText(CruiseEventDirector director)
    {
        var parts = new List<string>();
        foreach (CruiseEventDirector.PlannedEvent e in director.Planned) parts.Add($"day {e.Day} {e.Type}");
        return string.Join(", ", parts);
    }

    private void Check(string label, bool ok)
    {
        if (ok) _ok++;
        else _fail++;
        GD.Print($"[B11] {(ok ? "OK  " : "FAIL")} {label}");
    }
}
