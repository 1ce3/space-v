using System.Threading.Tasks;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Presentation;

namespace RocketGame.Tests;

/// <summary>
/// A8 check: run tests/a/part_effects_test.tscn (F6). Builds the Mars stack, then drives part state by code and checks
/// each visual reacts: engine plume follows throttle, gimbal turns, boosters burn, fins deflect, tank vents, solar
/// wings open, fairing halves spread, chute opens, heat shield chars, decoupler flashes. The window stays open.
/// </summary>
public partial class PartEffectsTest : Node3D
{
    [Export] public Node3D Vehicle { get; set; }
    [Export] public bool QuitWhenDone { get; set; }

    private int _passed;
    private int _failed;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Vehicle, nameof(Vehicle))) return;
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        GD.Print("[A test] ---- part effects test ----");
        AssembledRocket rocket = TestRocketFactory.CreateMarsStack();
        Check("Mars stack assembled", rocket != null);
        if (rocket == null) return;
        PartTree tree = rocket.PartTree;
        foreach (RocketPart part in tree.ActiveParts) Vehicle.AddChild(part);
        await Frames(2);

        // Engines: liquid at 0, then full, then gimbal.
        LiquidEnginePart main = tree.GetParts<LiquidEnginePart>().Find(e => e.ModelId == "engine_lv2");
        var mainVisual = VisualOf<EngineVisual>(main);
        Check("engine_lv2 has EngineVisual", mainVisual != null);
        Check("plume hidden before ignition", mainVisual != null && !mainVisual.PlumeVisible);
        main?.Arm();
        main?.SetThrottle(0f);
        main?.Ignite();
        Check("engine_lv2 ignites (armed, fuel, ignitions)", main != null && main.IsIgnited);
        main?.SetThrottle(0.2f);
        await Frames(2);
        float low = mainVisual?.PlumeFraction ?? 0f;
        main?.SetThrottle(1f);
        await Frames(2);
        float high = mainVisual?.PlumeFraction ?? 0f;
        Check("plume visible while burning", mainVisual != null && mainVisual.PlumeVisible);
        Check("plume grows with throttle", high > low && Mathf.IsEqualApprox(high, 1f), $"{low:0.00} -> {high:0.00}");
        main?.SetGimbal(5f);
        await Seconds(0.5f);
        var gimbal = mainVisual?.FindChild("GimbalPivot", true, false) as Node3D;
        Check("GimbalPivot turns with the gimbal", gimbal != null && Mathf.Abs(gimbal.RotationDegrees.Z) > 5f, $"{gimbal?.RotationDegrees.Z:0.0}");

        foreach (SolidBoosterPart booster in tree.GetParts<SolidBoosterPart>()) booster.Arm();
        await Frames(2);
        SolidBoosterPart sb = tree.GetParts<SolidBoosterPart>()[0];
        Check("solid booster plume visible after Arm", VisualOf<EngineVisual>(sb)?.PlumeVisible == true);

        // Fins, tank leak, solar wings.
        ControlFinPart fin = tree.GetParts<ControlFinPart>().Count > 0 ? tree.GetParts<ControlFinPart>()[0] : null;
        if (fin != null)
        {
            fin.SetDeflection(10f);
            await Seconds(0.5f);
            var hinge = VisualOf<FinVisual>(fin)?.FindChild("FinHinge", true, false) as Node3D;
            Check("control fin hinge rotates", hinge != null && Mathf.Abs(hinge.RotationDegrees.Z) > 5f, $"{hinge?.RotationDegrees.Z:0.0}");
        }
        else GD.Print("[A test] (no control fin in the stack, fin check skipped)");

        FuelTankPart tank = tree.GetParts<FuelTankPart>()[0];
        tank.LeakRateKgPerSec = 10f;
        await Frames(2);
        Check("leaking tank vents", VisualOf<TankVisual>(tank)?.IsVenting == true);
        tank.LeakRateKgPerSec = 0f;
        await Frames(2);
        Check("vent stops when the leak is sealed", VisualOf<TankVisual>(tank)?.IsVenting == false);

        var pod = (CommandPodPart)tree.RootPart;
        foreach (SolarArrayModule solar in pod.GetModules<SolarArrayModule>()) solar.SetDeployed(true);
        await Frames(2);
        Check("solar wings shown when deployed", VisualOf<PodVisual>(pod)?.SolarVisible == true);

        // Fairing: jettison arrives as OnSevered(Jettison) from debris spawning; call it the same way (by name).
        FairingPart fairing = tree.GetParts<FairingPart>()[0];
        var fairingVisual = VisualOf<FairingVisual>(fairing);
        tree.SeverSubtree(fairing, SeverCause.Jettison);
        fairingVisual?.Call("OnSevered", (int)SeverCause.Jettison);
        await Seconds(0.5f);
        var half = fairingVisual?.FindChild("HalfLeft", true, false) as Node3D;
        Check("fairing halves spread on jettison", fairingVisual?.IsJettisoned == true && half != null && half.Position.X < -0.2f, $"{half?.Position.X:0.00}");

        // Chute (needs the fairing gone).
        ParachutePart chute = tree.GetParts<ParachutePart>()[0];
        var chuteVisual = VisualOf<ParachuteVisual>(chute);
        Check("canopy hidden while stowed", chuteVisual != null && !chuteVisual.CanopyVisible);
        Check("chute deploys", chute.Deploy());
        await Seconds(1.5f);
        Check("canopy shown and fully open", chuteVisual?.CanopyVisible == true && chuteVisual.OpenFraction >= 0.99f, $"{chuteVisual?.OpenFraction:0.00}");

        // Heat shield.
        HeatShieldPart shield = tree.GetParts<HeatShieldPart>()[0];
        var shieldVisual = VisualOf<HeatShieldVisual>(shield);
        float full = shield.AblatorRemainingKg;
        shield.AblatorRemainingKg = full * 0.7f;
        await Frames(2);
        float partial = shieldVisual?.CharAmount ?? 0f;
        shield.AblatorRemainingKg = 0f;
        await Frames(2);
        Check("heat shield chars gradually", partial > 0.2f && partial < 1f && Mathf.IsEqualApprox(shieldVisual.CharAmount, 1f), $"{partial:0.00}");

        // Decoupler.
        DecouplerPart decoupler = tree.GetParts<DecouplerPart>()[0];
        var decouplerVisual = VisualOf<DecouplerVisual>(decoupler);
        Check("decoupler not flashed yet", decouplerVisual != null && !decouplerVisual.HasSeparated);
        if (decoupler.Joint != null) decoupler.Joint.IsSeparated = true;
        await Frames(2);
        Check("decoupler flashes when its joint separates", decouplerVisual?.HasSeparated == true);

        // Shutdown.
        main?.Shutdown();
        await Frames(2);
        Check("plume hidden after shutdown", mainVisual != null && !mainVisual.PlumeVisible);

        GD.Print($"[A test] ---- {_passed} passed, {_failed} failed ---- (window shows the effects)");
        if (_failed > 0) GD.PushError($"[A test] {_failed} check(s) failed");
        main?.SetThrottle(1f);
        main?.Ignite();
        if (QuitWhenDone) GetTree().Quit(_failed > 0 ? 1 : 0);
    }

    private static T VisualOf<T>(RocketPart part) where T : PartVisual => part?.GetNodeOrNull<T>("Visual");

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Seconds(float seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private void Check(string name, bool ok, string detail = "")
    {
        if (ok) _passed++;
        else _failed++;
        GD.Print($"[A test] {(ok ? "PASS" : "FAIL")} {name}{(ok || detail == "" ? "" : $" ({detail})")}");
    }
}
