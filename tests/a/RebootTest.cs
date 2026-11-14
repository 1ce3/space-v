using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Tests;

/// <summary>
/// Test for task A13/A14 (PHASE2_PLAN.md 5.7): PodModule.TryReboot with a fixed seed, module data of table 2.4
/// loaded from the catalog (grid size, radiation resistance, reboot chance) and the module tooltip.
/// </summary>
public partial class RebootTest : Node
{
    [Export] public bool QuitWhenDone { get; set; } = true;

    private int _passed, _failed;

    public override void _Ready() => CallDeferred(nameof(Run));

    private void Run()
    {
        GD.Print("[A test] ---- reboot test ----");

        PodModule sure = Make(1f);
        Check("working module: no reboot", !sure.TryReboot(new RandomNumberGenerator { Seed = 1 }) && sure.State == ComponentState.Idle);

        sure.State = ComponentState.Broken;
        sure.IsPowered = false;
        Check("chance 1: broken module comes back powered", sure.TryReboot(new RandomNumberGenerator { Seed = 1 })
            && sure.State == ComponentState.Idle && sure.IsPowered);

        PodModule never = Make(0f);
        never.State = ComponentState.Broken;
        never.IsPowered = false;
        Check("chance 0: stays broken", !never.TryReboot(new RandomNumberGenerator { Seed = 1 })
            && never.State == ComponentState.Broken && !never.IsPowered);

        var rng = new RandomNumberGenerator { Seed = 12345 };
        PodModule half = Make(0.5f);
        int ok = 0;
        const int tries = 2000;
        for (int i = 0; i < tries; i++)
        {
            half.State = ComponentState.Broken;
            if (half.TryReboot(rng)) ok++;
        }
        float rate = ok / (float)tries;
        Check($"chance 0.5 over {tries} tries ({rate:P1})", Mathf.Abs(rate - 0.5f) < 0.05f);

        var a = new RandomNumberGenerator { Seed = 99 };
        var b = new RandomNumberGenerator { Seed = 99 };
        bool same = true;
        for (int i = 0; i < 50; i++)
        {
            PodModule x = Make(0.5f), y = Make(0.5f);
            x.State = ComponentState.Broken;
            y.State = ComponentState.Broken;
            same &= x.TryReboot(a) == y.TryReboot(b);
        }
        Check("same seed, same results", same);

        CheckCatalog();

        GD.Print($"[A test] reboot test: {_passed} passed, {_failed} failed");
        if (QuitWhenDone) GetTree().Quit();
    }

    /// <summary>Table 2.4 values in the .tres files, read through the catalog.</summary>
    private void CheckCatalog()
    {
        PartCatalog catalog = PartCatalog.Instance;
        if (catalog == null)
        {
            Check("PartCatalog autoload", false);
            return;
        }
        ModuleDefinition imu = catalog.GetDefinition("imu_g1") as ModuleDefinition;
        ModuleDefinition rtg = catalog.GetDefinition("rtg_t1") as ModuleDefinition;
        Check("imu_g1: 1x1, resistance 0, reboot 0.3", imu != null && imu.GridSize == Vector2I.One
            && Mathf.IsZeroApprox(imu.RadiationResistance) && Mathf.IsEqualApprox(imu.RebootSuccessChance, 0.3f));
        Check("rtg_t1: 2x2, resistance 0.8, reboot 0.9", rtg != null && rtg.GridSize == new Vector2I(2, 2)
            && Mathf.IsEqualApprox(rtg.RadiationResistance, 0.8f) && Mathf.IsEqualApprox(rtg.RebootSuccessChance, 0.9f));
        string tip = imu?.GetFormattedTooltip() ?? "";
        Check("module tooltip shows size, resistance and reboot", tip.Contains("Size: 1 x 1") && tip.Contains("Radiation") && tip.Contains("Reboot"));
    }

    private static PodModule Make(float chance) => PodModule.Create(new InstrumentModuleDefinition
    {
        ModelId = "reboot_test", Kind = InstrumentKind.Comms, RebootSuccessChance = chance, DryMass = 1f,
        SlotType = ModuleSlotType.Comms,
    });

    private void Check(string name, bool ok)
    {
        if (ok) _passed++;
        else _failed++;
        GD.Print($"[A test] {(ok ? "PASS" : "FAIL")} {name}");
    }
}
