using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Tests;

/// <summary>
/// A1/A2 check: run tests/a/part_runtime_test.tscn (F6). Prints one line per check and a summary.
/// Builds rockets with TestRocketFactory, so it works before any .tres exists.
/// </summary>
public partial class PartRuntimeTest : Node
{
    [Export] public bool QuitWhenDone { get; set; }

    private int _passed;
    private int _failed;

    public override void _Ready()
    {
        GD.Print("[A test] ---- part runtime test ----");
        TestMinimalRocket();
        TestEngineRules();
        TestTank();
        TestThermalRules();
        TestHeatShield();
        TestSeverSubtree();
        TestModules();
        GD.Print($"[A test] ---- {_passed} passed, {_failed} failed ----");
        if (_failed > 0) GD.PushError($"[A test] {_failed} check(s) failed");
        if (QuitWhenDone) GetTree().Quit(_failed > 0 ? 1 : 0);
    }

    private void TestMinimalRocket()
    {
        AssembledRocket rocket = TestRocketFactory.CreateMinimal();
        Check("CreateMinimal returns a rocket", rocket != null);
        if (rocket == null) return;

        PartTree tree = rocket.PartTree;
        Check("3 active parts", tree.ActiveParts.Count == 3, $"{tree.ActiveParts.Count}");
        Check("root is CommandPodPart", tree.RootPart is CommandPodPart);
        Check("1 tank, 1 liquid engine", tree.GetParts<FuelTankPart>().Count == 1 && tree.GetParts<LiquidEnginePart>().Count == 1);

        var pod = (CommandPodPart)tree.RootPart;
        Check("pod has IMU and battery", pod.GetModules<GyroscopeImuModule>().Count == 1 && pod.GetModules<BatteryModule>().Count == 1);
        Check("battery full (500 Wh)", Near(pod.GetModules<BatteryModule>()[0].StoredEnergyWh, 500f));

        // 600 + 300 payload + 15 + 20 (pod) + 150 + 1000 (tank) + 300 (engine) = 2385 kg
        MassProperties mp = rocket.MassProperties;
        Check("total mass 2385 kg", Near(mp.TotalMass, 2385f), $"{mp.TotalMass}");
        // y: engine 1.0 m, tank 4.0 m, pod 7.0 m (cell 2 m) -> (300*1 + 1150*4 + 935*7) / 2385
        float expectedY = (300f * 1f + 1150f * 4f + 935f * 7f) / 2385f;
        Check("CoM x = 9 m (column 4)", Near(mp.CenterOfMass.X, 9f), $"{mp.CenterOfMass}");
        Check("CoM y as expected", Near(mp.CenterOfMass.Y, expectedY, 0.01f), $"{mp.CenterOfMass.Y} vs {expectedY}");
        Check("inertia > 0", mp.MomentOfInertia > 0f, $"{mp.MomentOfInertia}");

        var engine = tree.GetParts<LiquidEnginePart>()[0];
        Check("stage 0 arms the engine", rocket.Stages.Count == 1 && rocket.Stages[0].EnginesToArm.Contains(engine));
        Check("engine feeds from the tank", engine.FeedTanks.Count == 1 && Near(engine.GetAvailableFuel(), 1000f));
        Check("engine parent is tank", engine.ParentPart is FuelTankPart && engine.ParentJoint != null);
        FreeTree(tree);
    }

    private void TestEngineRules()
    {
        AssembledRocket rocket = TestRocketFactory.CreateMinimal();
        var engine = rocket.PartTree.GetParts<LiquidEnginePart>()[0];

        engine.Ignite();
        Check("no ignition before Arm", !engine.IsIgnited);
        engine.Arm();
        Check("Arm sets IsArmed", engine.IsArmed && !engine.IsIgnited);

        engine.SetThrottle(1.5f);
        Check("SetThrottle clamps to 1", Near(engine.Throttle, 1f));
        engine.Ignite();
        Check("Ignite after Arm", engine.IsIgnited && engine.RemainingIgnitions == 0);
        Check("vacuum thrust 140 kN at full throttle", Near(engine.ComputeThrust(0f), 140_000f, 1f), $"{engine.ComputeThrust(0f)}");
        Check("sea-level thrust 126 kN", Near(engine.ComputeThrust(101325f), 126_000f, 1f), $"{engine.ComputeThrust(101325f)}");

        engine.SetThrottle(0f);
        Check("throttle 0 gives 0 thrust", Near(engine.ComputeThrust(0f), 0f));
        engine.SetThrottle(0.5f);
        Check("throttle 0.5 -> factor 0.75", Near(engine.ComputeThrust(0f), 105_000f, 1f), $"{engine.ComputeThrust(0f)}");

        engine.Shutdown();
        engine.Ignite();
        Check("no relight with 0 ignitions left", !engine.IsIgnited);

        engine.SetGimbal(9f);
        Check("gimbal clamps to 5 deg", Near(engine.GimbalAngleDeg, 5f));
        engine.SetGimbal(-9f);
        Check("gimbal clamps to -5 deg", Near(engine.GimbalAngleDeg, -5f));
        FreeTree(rocket.PartTree);
    }

    private void TestTank()
    {
        AssembledRocket rocket = TestRocketFactory.CreateMinimal();
        var tank = rocket.PartTree.GetParts<FuelTankPart>()[0];
        Check("DrainFuel(300) returns 300", Near(tank.DrainFuel(300f), 300f));
        Check("700 kg left", Near(tank.CurrentFuel, 700f));
        Check("DrainFuel(5000) returns the rest", Near(tank.DrainFuel(5000f), 700f) && Near(tank.CurrentFuel, 0f));
        Check("tank mass = dry when empty", Near(tank.GetCurrentMass(), 150f));

        var engine = rocket.PartTree.GetParts<LiquidEnginePart>()[0];
        engine.Arm();
        engine.SetThrottle(1f);
        engine.Ignite();
        Check("no ignition with empty tanks", !engine.IsIgnited);
        FreeTree(rocket.PartTree);
    }

    private void TestThermalRules()
    {
        AssembledRocket rocket = TestRocketFactory.CreateMinimal();
        var engine = rocket.PartTree.GetParts<LiquidEnginePart>()[0];
        float capacity = engine.HeatCapacityJPerK; // 300 kg * 900

        engine.ApplyThermalLoad((1250f - engine.TempK) * capacity);
        Check("> MaxSafeTempK -> Overheated", engine.State == ComponentState.Overheated, $"{engine.State} at {engine.TempK:0} K");
        engine.ApplyThermalLoad((1150f - engine.TempK) * capacity);
        Check("between 0.9x and 1x stays Overheated", engine.State == ComponentState.Overheated, $"{engine.State}");
        engine.ApplyThermalLoad((1000f - engine.TempK) * capacity);
        Check("< 0.9x MaxSafeTempK -> Active", engine.State == ComponentState.Active, $"{engine.State}");
        engine.ApplyThermalLoad((150f - engine.TempK) * capacity);
        Check("< MinSafeTempK -> Frozen", engine.State == ComponentState.Frozen, $"{engine.State}");
        Check("frozen cannot operate", !engine.CanOperate());
        engine.ApplyThermalLoad((215f - engine.TempK) * capacity);
        Check("> Min + 10 K -> Active", engine.State == ComponentState.Active, $"{engine.State}");
        engine.ApplyThermalLoad((1600f - engine.TempK) * capacity);
        Check("> 1.25x MaxSafeTempK -> Broken", engine.State == ComponentState.Broken, $"{engine.State}");
        engine.ApplyThermalLoad((300f - engine.TempK) * capacity);
        Check("Broken stays Broken", engine.State == ComponentState.Broken);
        FreeTree(rocket.PartTree);
    }

    private void TestHeatShield()
    {
        var def = new HeatShieldPartDefinition { ModelId = "test_shield", DryMass = 200f, MaxAblatorKg = 80f, HeatOfAblationJPerKg = 150_000f };
        var shield = (HeatShieldPart)RocketPart.Create(def);
        Check("factory makes HeatShieldPart", shield != null);
        if (shield == null) return;
        Check("ablator starts full", Near(shield.AblatorRemainingKg, 80f));
        Check("Ablate(3 MJ) absorbs all", Near(shield.Ablate(3_000_000f), 0f) && Near(shield.AblatorRemainingKg, 60f, 0.01f));
        float leftover = shield.Ablate(100_000_000f);
        Check("Ablate past capacity lets the rest through", Near(leftover, 91_000_000f, 16f) && Near(shield.AblatorRemainingKg, 0f), $"{leftover}");
        shield.Free();
    }

    private void TestSeverSubtree()
    {
        AssembledRocket rocket = TestRocketFactory.CreateMinimal();
        PartTree tree = rocket.PartTree;
        var tank = tree.GetParts<FuelTankPart>()[0];
        var engine = tree.GetParts<LiquidEnginePart>()[0];

        RocketPart severedPart = null;
        SeverCause severedCause = SeverCause.Break;
        float severedImpulse = -1f;
        int calls = 0;
        tree.PartSevered += (part, cause, impulse) => { severedPart = part; severedCause = cause; severedImpulse = impulse; calls++; };

        Check("subtree of tank = tank + engine", tree.GetSubtree(tank).Count == 2);
        Check("subtree mass of tank = 1450 kg", Near(tree.GetSubtreeMass(tank), 1450f), $"{tree.GetSubtreeMass(tank)}");

        tree.SeverSubtree(engine, SeverCause.Jettison, 1234f);
        Check("PartSevered emitted once with args", calls == 1 && severedPart == engine && severedCause == SeverCause.Jettison && Near(severedImpulse, 1234f));
        Check("engine removed from ActiveParts", !tree.ActiveParts.Contains(engine) && tree.ActiveParts.Count == 2);
        Check("tank has no children", tank.Children.Count == 0);
        Check("engine keeps ParentJoint and ParentPart", engine.ParentJoint != null && engine.ParentPart == tank);
        Check("joint marked separated", engine.ParentJoint.IsSeparated);
        Check("feed tank of a cut engine still listed", engine.FeedTanks.Count == 1);

        tree.SeverSubtree(engine, SeverCause.Jettison);
        Check("second cut of the same part does nothing", calls == 1);

        tree.SeverSubtree(tank, SeverCause.Break);
        Check("Break of tank emitted", calls == 2 && severedCause == SeverCause.Break && tree.ActiveParts.Count == 1);
        Check("tank no longer attached", tank.IsAttached == false);
        FreeTree(tree, engine, tank);
    }

    private void TestModules()
    {
        AssembledRocket rocket = TestRocketFactory.CreateMinimal();
        var pod = (CommandPodPart)rocket.PartTree.RootPart;
        var imu = pod.GetModules<GyroscopeImuModule>()[0];
        var battery = pod.GetModules<BatteryModule>()[0];

        Check("IMU allows StabilityAssist only", imu.EnabledSasModes.Has(SasMode.StabilityAssist) && !imu.EnabledSasModes.Has(SasMode.Prograde));
        Check("IMU noise 0.5 deg", Near(imu.CurrentNoiseDeg, 0.5f));
        imu.AccumulatedDriftDeg = 2f;
        Check("IMU reads true + drift", Near(imu.ReadPitch(90f), 92f));
        imu.CalibrateWithStarTracker();
        Check("calibration clears drift", Near(imu.AccumulatedDriftDeg, 0f));

        Check("battery Draw(100) = 100", Near(battery.Draw(100f), 100f) && Near(battery.StoredEnergyWh, 400f));
        Check("battery Draw(1000) = 400", Near(battery.Draw(1000f), 400f) && Near(battery.StoredEnergyWh, 0f));
        Check("battery charge capped at 500", Near(battery.Draw(-800f), -500f) && Near(battery.StoredEnergyWh, 500f));

        imu.IsPowered = false;
        Check("unpowered module cannot operate", !imu.CanOperate());
        Check("pod mass includes modules and payload", Near(pod.GetCurrentMass(), 935f));
        FreeTree(rocket.PartTree);
    }

    private void Check(string name, bool ok, string detail = "")
    {
        if (ok) _passed++; else _failed++;
        string suffix = string.IsNullOrEmpty(detail) ? "" : $" ({detail})";
        GD.Print($"[A test] {(ok ? "PASS" : "FAIL")} {name}{suffix}");
    }

    private static bool Near(float a, float b, float eps = 0.001f) => Mathf.Abs(a - b) <= eps;

    /// <summary>Parts are orphan nodes here; free them so Godot does not report leaks.</summary>
    private static void FreeTree(PartTree tree, params RocketPart[] extra)
    {
        foreach (RocketPart part in tree.ActiveParts) part.Free();
        foreach (RocketPart part in extra) if (IsInstanceValid(part)) part.Free();
    }
}
