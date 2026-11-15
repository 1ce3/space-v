using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Physics;

namespace RocketGame.Tests.B;

/// <summary>
/// Test for task B9 (PHASE2_PLAN.md 3.2): physics reads part stats through the material.
/// 1. No material: every effective stat equals the definition value, launch mass unchanged;
/// 2. Steel-like material on a tank: dry mass x1.4, MaxSafeTempK +300 K, impact x1.5, shear and bending x1.3,
///    and the rocket mass grows by exactly the extra dry mass;
/// 3. FailureDetector.MonitorImpact and ThermalStateRules use the effective limits;
/// 4. RocketAssembler joint strength follows the decoupler material.
/// </summary>
public partial class MaterialTest : Node
{
    /// <summary>Quit after the test so the log is flushed.</summary>
    [Export] public bool QuitWhenDone { get; set; } = true;

    private readonly List<RocketPart> _parts = new();
    private int _ok, _fail;

    public override void _Ready() => CallDeferred(nameof(RunAll));

    private void RunAll()
    {
        AssembledRocket rocket = TestRocketFactory.CreateMarsStack();
        if (rocket?.PartTree == null)
        {
            GD.PushError("[B9] TestRocketFactory.CreateMarsStack() returned nothing (is the PartCatalog autoload loaded?)");
            return;
        }
        _parts.AddRange(rocket.PartTree.ActiveParts);

        TestNoMaterial(rocket.PartTree);
        TestSteel(rocket.PartTree);
        TestLimits(rocket.PartTree);
        TestJoint(rocket.PartTree);

        foreach (RocketPart part in _parts)
            if (IsInstanceValid(part)) part.Free();
        GD.Print($"[B9] Done: {_ok} OK, {_fail} FAIL.");
        if (QuitWhenDone) GetTree().Quit();
    }

    private static MaterialDefinition Steel() => new()
    {
        MaterialId = "mat_test_steel",
        MassMultiplier = 1.4f,
        MaxTempBonusK = 300f,
        ImpactToleranceMultiplier = 1.5f,
        StrengthMultiplier = 1.3f,
    };

    private FuelTankPart FirstTank(PartTree tree) => tree.GetParts<FuelTankPart>().Find(t => t.Definition != null);

    private void TestNoMaterial(PartTree tree)
    {
        bool allSame = true;
        foreach (RocketPart p in tree.ActiveParts)
        {
            PartDefinition d = p.Definition;
            if (d == null) continue;
            allSame &= Mathf.IsEqualApprox(p.GetEffectiveDryMass(), d.DryMass)
                       && Mathf.IsEqualApprox(p.GetEffectiveMaxSafeTempK(), d.MaxSafeTempK)
                       && Mathf.IsEqualApprox(p.GetEffectiveImpactToleranceMps(), d.ImpactToleranceMps)
                       && Mathf.IsEqualApprox(p.GetEffectiveMaxShear(), d.MaxShear)
                       && Mathf.IsEqualApprox(p.GetEffectiveMaxBending(), d.MaxBending);
        }
        Check("no material: effective stats = definition", allSame);
    }

    private void TestSteel(PartTree tree)
    {
        FuelTankPart tank = FirstTank(tree);
        if (tank == null) { Check("Mars stack has a tank", false); return; }
        PartDefinition d = tank.Definition;
        float massBefore = MassCenterCalculator.Calculate(tree.ActiveParts).TotalMass;

        tank.Material = Steel();
        Check($"steel dry mass x1.4 ({d.DryMass:0} -> {tank.GetEffectiveDryMass():0} kg)", Mathf.IsEqualApprox(tank.GetEffectiveDryMass(), d.DryMass * 1.4f));
        Check("steel MaxSafeTempK +300 K", Mathf.IsEqualApprox(tank.GetEffectiveMaxSafeTempK(), d.MaxSafeTempK + 300f));
        Check("steel impact tolerance x1.5", Mathf.IsEqualApprox(tank.GetEffectiveImpactToleranceMps(), d.ImpactToleranceMps * 1.5f));
        Check("steel shear and bending x1.3",
            Mathf.IsEqualApprox(tank.GetEffectiveMaxShear(), d.MaxShear * 1.3f) &&
            Mathf.IsEqualApprox(tank.GetEffectiveMaxBending(), d.MaxBending * 1.3f));
        Check("heat capacity follows the material mass", Mathf.IsEqualApprox(tank.HeatCapacityJPerK, d.DryMass * 1.4f * ThermalStateRules.SpecificHeat * ThermalStateRules.SkinMassFraction));

        float massAfter = MassCenterCalculator.Calculate(tree.ActiveParts).TotalMass;
        float extra = d.DryMass * 0.4f;
        Check($"rocket mass +{extra:0} kg ({massBefore:0} -> {massAfter:0})", Mathf.Abs(massAfter - massBefore - extra) < 0.5f);
        tank.Material = null;
    }

    private void TestLimits(PartTree tree)
    {
        FuelTankPart tank = FirstTank(tree);
        if (tank == null) return;
        PartDefinition d = tank.Definition;
        var detector = new FailureDetector(null);

        // Between the aluminium limit and the steel limit: only the plain tank breaks.
        float speed = d.ImpactToleranceMps * 1.2f;
        tank.Material = Steel();
        detector.BeginTick();
        Check("steel tank survives an impact at 1.2 x base tolerance", !detector.MonitorImpact(tank, speed));

        // Temperature between the base and the steel limit: plain = Overheated, steel = still fine.
        tank.State = ComponentState.Active;
        tank.TempK = d.MaxSafeTempK * 1.05f;
        ThermalStateRules.Evaluate(tank);
        Check("steel tank not Overheated at 1.05 x base limit", tank.State == ComponentState.Active);

        tank.Material = null;
        tank.State = ComponentState.Active;
        ThermalStateRules.Evaluate(tank);
        Check("plain tank Overheated at 1.05 x base limit", tank.State == ComponentState.Overheated);
        tank.State = ComponentState.Active;
        tank.TempK = 288f;
    }

    private void TestJoint(PartTree tree)
    {
        DecouplerPart decoupler = tree.GetParts<DecouplerPart>().Find(p => p.DecouplerDefinition != null);
        if (decoupler == null) { Check("Mars stack has a decoupler", false); return; }
        float plain = decoupler.GetEffectiveAttachmentStrength();
        decoupler.Material = Steel();
        float steel = decoupler.GetEffectiveAttachmentStrength();
        decoupler.Material = null;
        Check($"decoupler strength x1.3 with steel ({plain:0} -> {steel:0} N)", Mathf.IsEqualApprox(steel, plain * 1.3f) && plain > 0f);
    }

    private void Check(string name, bool ok)
    {
        if (ok) _ok++; else _fail++;
        GD.Print($"[B9] {(ok ? "OK  " : "FAIL")} {name}");
    }
}
