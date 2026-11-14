using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Physics;
using RocketGame.Research;

namespace RocketGame.Tests.B;

/// <summary>
/// Sandbox for task B5. Builds the Mars stack with TestRocketFactory.CreateMarsStack() (A4, needs the PartCatalog autoload),
/// puts it in its state after ascent (boosters, first stage and fairing gone, engine_vac1 armed, part of tank_h1 used),
/// then runs CruiseSimulator for the whole trip in four setups:
/// 1. as designed: delta-v above RequiredDeltaVMps, the transfer burn works, nothing freezes, no brownout;
/// 2. without thermal modules: a module freezes (expected in the second half of the trip);
/// 3. with insulation_i1 + rhu_h1: nothing freezes;
/// 4. without solar or RTG: brownout in the first days.
/// </summary>
public partial class CruiseTest : Node
{
    /// <summary>Cruise segment (seg_cruise.tres): RequiredDeltaVMps and DurationDays.</summary>
    [Export] public MissionSegmentDefinition Segment { get; set; }
    /// <summary>Origin body (earth_scaled.tres), for its solar flux.</summary>
    [Export] public CelestialBodyDefinition Origin { get; set; }
    /// <summary>Destination body (mars_scaled.tres), for its solar flux.</summary>
    [Export] public CelestialBodyDefinition Destination { get; set; }
    /// <summary>Physics settings (physics_settings.tres).</summary>
    [Export] public PhysicsSettings Settings { get; set; }
    /// <summary>
    /// Fuel left in the upper stage tanks after reaching orbit (kg). The guide A estimate is about 1 250 m/s left, about 1 100 kg.
    /// Negative keeps the tanks full. Replace with a real ascent once the flight scene runs (M2).
    /// </summary>
    [Export] public float UpperStageFuelAfterAscentKg { get; set; } = 1100f;
    /// <summary>Ignitions of engine_vac1 used to reach orbit.</summary>
    [Export] public int IgnitionsUsedInAscent { get; set; } = 1;
    /// <summary>Stages fired before the cruise (0 = launch, 1 = boosters, 2 = first stage and engine_vac1).</summary>
    [Export] public int LastStageBeforeCruise { get; set; } = 2;
    /// <summary>Heater module for setup 3 (rhu_h1.tres). Empty = PartCatalog / guide defaults.</summary>
    [Export] public ModuleDefinition Heater { get; set; }
    /// <summary>Print the state every this many days.</summary>
    [Export] public int LogEveryDays { get; set; } = 30;
    /// <summary>Quit after the test so the log is flushed.</summary>
    [Export] public bool QuitWhenDone { get; set; } = true;

    private readonly List<RocketPart> _allParts = new();

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Segment, nameof(Segment))) return;
        CallDeferred(nameof(RunAll));
    }

    private void RunAll()
    {
        GD.Print($"[Cruise] Segment {Segment.SegmentId}: need {Segment.RequiredDeltaVMps:0} m/s, {Segment.DurationDays:0} days, " +
                 $"flux {Origin?.SolarFluxWm2 ?? 0:0} -> {Destination?.SolarFluxWm2 ?? 0:0} W/m^2");

        RunCase("1. Mars stack as designed", pod => { }, expectFrozen: false, expectBrownout: false, doBurn: true);
        RunCase("2. No thermal module", pod => pod.Modules.RemoveAll(m => m is ThermalModule), expectFrozen: true, expectBrownout: null, doBurn: false);
        RunCase("3. insulation_i1 + rhu_h1", AddHeater, expectFrozen: false, expectBrownout: false, doBurn: false);
        RunCase("4. No solar or RTG", pod => pod.Modules.RemoveAll(m => m is SolarArrayModule || m is RtgModule), expectFrozen: null, expectBrownout: true, doBurn: false);

        foreach (RocketPart part in _allParts)
            if (IsInstanceValid(part)) part.Free();
        _allParts.Clear();
        GD.Print("[Cruise] Done.");
        if (QuitWhenDone) GetTree().Quit();
    }

    private void RunCase(string name, System.Action<CommandPodPart> setup, bool? expectFrozen, bool? expectBrownout, bool doBurn)
    {
        GD.Print($"[Cruise] --- {name}");
        AssembledRocket rocket = TestRocketFactory.CreateMarsStack();
        if (rocket?.PartTree == null)
        {
            GD.PushError("[Cruise] TestRocketFactory.CreateMarsStack() returned nothing (is the PartCatalog autoload loaded?)");
            return;
        }
        _allParts.AddRange(rocket.PartTree.ActiveParts);
        PartTree tree = rocket.PartTree;
        CommandPodPart pod = PowerSolver.FindPod(tree);
        if (pod == null)
        {
            GD.PushError("[Cruise] Mars stack has no command pod");
            return;
        }

        PrepareAfterAscent(rocket);
        setup(pod);
        foreach (SolarArrayModule solar in pod.GetModules<SolarArrayModule>()) solar.SetDeployed(true);
        GD.Print($"[Cruise] Modules: {ModuleList(pod)}; parts left {tree.ActiveParts.Count}");

        // Balance test: no random cruise events (B11) or transfer-burn incidents.
        var segment = (MissionSegmentDefinition)Segment.Duplicate();
        segment.EnabledHazards &= ~(CruiseEventDirector.EventMask | FlightEventMask.EngineFlameout | FlightEventMask.CryoLeak);
        var sim = new CruiseSimulator { Settings = Settings };
        sim.Begin(tree, segment, Origin, Destination);

        if (doBurn)
        {
            float before = sim.GetRemainingDeltaV();
            Check($"delta-v after ascent {before:0} m/s > {Segment.RequiredDeltaVMps:0}", before > Segment.RequiredDeltaVMps);
            bool burned = sim.ExecuteTransferBurn();
            Check("transfer burn", burned);
            GD.Print($"[Cruise] Delta-v after the burn {sim.GetRemainingDeltaV():0} m/s, engine_vac1 ignitions left {VacIgnitions(tree)}");
        }

        int days = Mathf.CeilToInt(Segment.DurationDays);
        int firstFrozen = -1, firstBrownout = -1;
        float lowest = float.MaxValue;
        CruiseStepResult step = default;
        for (int d = 0; d < days; d++)
        {
            step = sim.StepDay();
            lowest = Mathf.Min(lowest, step.LowestTempRatio);
            if (firstFrozen < 0 && step.FrozenComponents.Count > 0)
            {
                firstFrozen = step.Day;
                GD.Print($"[Cruise] Day {step.Day}: frozen {string.Join(", ", step.FrozenComponents)} at {pod.TempK:0} K");
            }
            if (firstBrownout < 0 && step.IsBrownout)
            {
                firstBrownout = step.Day;
                GD.Print($"[Cruise] Day {step.Day}: brownout, balance {step.PowerBalanceW:0} W");
            }
            if (step.BrokenComponents.Count > 0) GD.Print($"[Cruise] Day {step.Day}: broken {string.Join(", ", step.BrokenComponents)}");
            if (LogEveryDays > 0 && step.Day % LogEveryDays == 0)
                GD.Print($"[Cruise] Day {step.Day,3}: flux {sim.CurrentSolarFluxWm2,4:0} W/m^2, pod {pod.TempK,3:0} K, " +
                         $"power {step.PowerBalanceW,4:0} W, battery {step.BatteryFraction:P0}, lowest temp ratio {step.LowestTempRatio:0.00}");
        }

        GD.Print($"[Cruise] End: day {step.Day}, lowest temp ratio {lowest:0.00}, first freeze {DayText(firstFrozen)}, first brownout {DayText(firstBrownout)}");
        if (expectFrozen.HasValue)
            Check(expectFrozen.Value ? "a module froze" : "nothing froze", (firstFrozen > 0) == expectFrozen.Value);
        if (expectFrozen == true && firstFrozen > 0)
            GD.Print($"[Cruise] Balance goal: freeze in the second half (day > {days / 2}) -> {(firstFrozen > days / 2 ? "yes" : "no, B6 tuning")}");
        if (expectBrownout.HasValue)
            Check(expectBrownout.Value ? "brownout happened" : "no brownout", (firstBrownout > 0) == expectBrownout.Value);
        if (expectBrownout == true && firstBrownout > 0)
            GD.Print($"[Cruise] Balance goal: brownout in the first days -> {(firstBrownout <= 5 ? "yes" : "no, B6 tuning")}");
    }

    /// <summary>Fires stages 1..LastStageBeforeCruise, arms their engines, drops the fairing and uses part of the upper stage fuel.</summary>
    private void PrepareAfterAscent(AssembledRocket rocket)
    {
        PartTree tree = rocket.PartTree;
        int last = Mathf.Min(LastStageBeforeCruise, rocket.Stages.Count - 1);
        for (int s = 1; s <= last; s++)
            foreach (Joint joint in rocket.Stages[s].Joints)
                if (joint.ChildPart != null && joint.ChildPart.IsAttached) tree.SeverSubtree(joint.ChildPart, SeverCause.Jettison);
        foreach (FairingPart fairing in tree.GetParts<FairingPart>()) tree.SeverSubtree(fairing, SeverCause.Jettison);

        for (int s = 0; s <= last; s++)
            foreach (EnginePart engine in rocket.Stages[s].EnginesToArm)
                if (engine.IsAttached) engine.Arm();

        foreach (LiquidEnginePart engine in tree.GetParts<LiquidEnginePart>())
        {
            if (!engine.IsArmed) continue;
            engine.RemainingIgnitions = Mathf.Max(0, engine.RemainingIgnitions - IgnitionsUsedInAscent);
            if (UpperStageFuelAfterAscentKg < 0f) continue;
            float left = UpperStageFuelAfterAscentKg;
            foreach (FuelTankPart tank in engine.FeedTanks)
            {
                tank.CurrentFuel = Mathf.Min(tank.CurrentFuel, left);
                left -= tank.CurrentFuel;
            }
        }
    }

    private void AddHeater(CommandPodPart pod)
    {
        ModuleDefinition def = Heater ?? TestRocketFactory.GetModule("rhu_h1");
        PodModule heater = def != null ? PodModule.Create(def) : null;
        if (heater == null)
        {
            GD.PushError("[Cruise] No rhu_h1 definition");
            return;
        }
        pod.AddModule(heater); // Test only: pod_mk2 has one thermal slot, the build screen would refuse this.
    }

    private static string ModuleList(CommandPodPart pod)
    {
        var ids = new List<string>();
        foreach (PodModule module in pod.Modules) ids.Add(module.ModelId);
        return ids.Count > 0 ? string.Join(", ", ids) : "none";
    }

    private static int VacIgnitions(PartTree tree)
    {
        foreach (LiquidEnginePart engine in tree.GetParts<LiquidEnginePart>())
            if (engine.IsArmed) return engine.RemainingIgnitions;
        return 0;
    }

    private static string DayText(int day) => day > 0 ? $"day {day}" : "never";

    private static void Check(string label, bool ok) => GD.Print($"[Cruise] {(ok ? "OK  " : "FAIL")} {label}");
}
