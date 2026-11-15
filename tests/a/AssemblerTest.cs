using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using Side = RocketGame.Core.Side;

namespace RocketGame.Tests;

/// <summary>
/// A4 check: run tests/a/assembler_test.tscn (F6). Assembles the Mars stack (A_BUILDING.md 5.4) with the real
/// RocketAssembler and checks tree, joints, the 5 stages, feed tanks, shielding, mass, staging and validation errors.
/// </summary>
public partial class AssemblerTest : Node
{
    [Export] public bool QuitWhenDone { get; set; }

    private int _passed;
    private int _failed;

    public override void _Ready()
    {
        GD.Print("[A test] ---- assembler test ----");
        if (PartCatalog.Instance == null)
        {
            GD.PushError("[A test] PartCatalog autoload missing");
            return;
        }
        TestMarsStack();
        TestStagingRun();
        TestValidation();
        GD.Print($"[A test] ---- {_passed} passed, {_failed} failed ----");
        if (_failed > 0) GD.PushError($"[A test] {_failed} check(s) failed");
        if (QuitWhenDone) GetTree().Quit(_failed > 0 ? 1 : 0);
    }

    private void TestMarsStack()
    {
        AssembledRocket rocket = TestRocketFactory.CreateMarsStack();
        Check("CreateMarsStack returns a rocket", rocket != null);
        if (rocket == null) return;
        PartTree tree = rocket.PartTree;

        Check("19 active parts (14 + 5 decouplers)", tree.ActiveParts.Count == 19, $"{tree.ActiveParts.Count}");
        Check("root is pod_mk2", tree.RootPart?.ModelId == "pod_mk2");
        var pod = (CommandPodPart)tree.RootPart;
        Check("pod has 5 modules", pod.Modules.Count == 5, $"{pod.Modules.Count}");
        Check("payload 300 kg", Near(pod.PayloadMassKg, 300f), $"{pod.PayloadMassKg}");
        Check("every part has Tree set", tree.ActiveParts.TrueForAll(p => p.Tree == tree));

        RocketPart fairing = Find(tree, "fairing_n1");
        RocketPart chute = Find(tree, "chute_p1");
        RocketPart landEngine = Find(tree, "engine_land1");
        RocketPart shield = Find(tree, "heatshield_h1");
        RocketPart tankH = Find(tree, "tank_h1");
        RocketPart vacEngine = Find(tree, "engine_vac1");
        RocketPart tankM = Find(tree, "tank_m1");
        RocketPart mainEngine = Find(tree, "engine_lv2");
        List<SolidBoosterPart> boosters = tree.GetParts<SolidBoosterPart>();
        List<AeroSurfacePart> fixedFins = tree.GetParts<AeroSurfacePart>();
        List<ControlFinPart> fins = tree.GetParts<ControlFinPart>();

        Check("chain: fairing -> chute -> pod", fairing?.ParentPart == chute && chute?.ParentPart == pod);
        Check("chain: shield -> engine_land1, tank_h1 -> shield", shield?.ParentPart == landEngine && tankH?.ParentPart == shield);
        Check("chain: tank_m1 -> engine_vac1, engine_lv2 -> tank_m1", tankM?.ParentPart == vacEngine && mainEngine?.ParentPart == tankM);
        Check("2 boosters, parent tank_m1", boosters.Count == 2 && boosters.TrueForAll(b => b.ParentPart == tankM));
        Check("2 control fins (fin_f1), parent = a booster",
            fins.Count == 2 && fins.TrueForAll(f => f.ParentPart is SolidBoosterPart), $"{fins.Count} control, {fixedFins.Count} aero");

        Check("fairing joint self-separating, strength 100 000", fairing?.ParentJoint is { IsSelfSeparating: true } j1 && Near(j1.Strength, 100_000f));
        Check("chute joint self-separating, strength 300 000 (B8)", chute?.ParentJoint is { IsSelfSeparating: true } j2 && Near(j2.Strength, 300_000f));
        Check("booster joints: 1 decoupler, 3 contact edges, strength 300 000",
            boosters.TrueForAll(b => b.ParentJoint.Decouplers.Count == 1 && b.ParentJoint.ContactEdges.Count == 3 && Near(b.ParentJoint.Strength, 300_000f)));
        Check("tank_m1 joint depth 6 with a decoupler", tankM?.ParentJoint is { Depth: 6 } j3 && j3.Decouplers.Count == 1);
        Check("engine_lv2 joint is permanent", mainEngine?.ParentJoint is { IsPermanent: true });
        Check("every decoupler knows its joint", tree.GetParts<DecouplerPart>().TrueForAll(d => d.Joint != null && d.Joint.Decouplers.Contains(d)));

        // Stages (A_BUILDING.md 5.4).
        List<StageGroup> s = rocket.Stages;
        Check("5 stages", s.Count == 5, $"{s.Count}");
        if (s.Count == 5)
        {
            Check("stage 0 arms engine_lv2 + 2 boosters",
                s[0].Joints.Count == 0 && s[0].EnginesToArm.Count == 3 && s[0].EnginesToArm.Contains((EnginePart)mainEngine)
                && boosters.TrueForAll(b => s[0].EnginesToArm.Contains(b)), Ids(s[0].EnginesToArm));
            Check("stage 1 = 2 booster joints, depth 7, arms nothing",
                s[1].Depth == 7 && s[1].Joints.Count == 2 && s[1].EnginesToArm.Count == 0, $"depth {s[1].Depth}, {s[1].Joints.Count} joints");
            Check("stage 1 drops 6 parts (2 x booster, fin, decoupler)", s[1].Parts.Count == 6, $"{s[1].Parts.Count}");
            Check("stage 2 = tank_m1 (depth 6), arms engine_vac1",
                s[2].Depth == 6 && s[2].Joints.Count == 1 && s[2].Joints[0].ChildPart == tankM
                && s[2].EnginesToArm.Count == 1 && s[2].EnginesToArm[0] == vacEngine, Ids(s[2].EnginesToArm));
            Check("stage 3 = tank_h1 (depth 4), arms nothing",
                s[3].Depth == 4 && s[3].Joints.Count == 1 && s[3].Joints[0].ChildPart == tankH && s[3].EnginesToArm.Count == 0);
            Check("stage 4 = heatshield_h1 (depth 3), arms engine_land1",
                s[4].Depth == 3 && s[4].Joints.Count == 1 && s[4].Joints[0].ChildPart == shield
                && s[4].EnginesToArm.Count == 1 && s[4].EnginesToArm[0] == landEngine, Ids(s[4].EnginesToArm));
        }

        // Feed tanks.
        Check("engine_lv2 feeds from tank_m1 only", FeedIs(mainEngine, tankM));
        Check("engine_vac1 feeds from tank_h1 only", FeedIs(vacEngine, tankH));
        Check("engine_land1 feeds from tank_lm1 only", FeedIs(landEngine, Find(tree, "tank_lm1")));

        // Shielding.
        Check("fairing shields chute + pod", fairing is FairingPart fp && fp.ShieldedParts.Count == 2 && fp.ShieldedParts.Contains(pod));
        Check("heat shield shields engine_land1, tank_lm1, pod",
            shield is HeatShieldPart hs && hs.ShieldedParts.Count == 3 && hs.ShieldedParts[0] == landEngine && hs.ShieldedParts[2] == pod);

        // Mass: 21 990 (parts) + 125 (modules) + 300 (payload) + 150 (5 decouplers).
        MassProperties mp = rocket.MassProperties;
        Check("total mass 26 665 kg (B7/B8 balance)", Near(mp.TotalMass, 26_665f, 0.5f), $"{mp.TotalMass}");
        Check("CoM x = 9 m (symmetric)", Near(mp.CenterOfMass.X, 9f, 0.01f), $"{mp.CenterOfMass}");
        Check("parts positioned in metres (pod at y = 21 m)", Near(pod.Position.Y, 21f) && Near(pod.Position.X, 9f), $"{pod.Position}");
        GD.Print($"[A test] Mars stack: {mp.TotalMass:0} kg, CoM {mp.CenterOfMass}, I {mp.MomentOfInertia:0} kg*m2");

        FreeTree(tree);
    }

    private void TestStagingRun()
    {
        AssembledRocket rocket = TestRocketFactory.CreateMarsStack();
        if (rocket == null) return;
        PartTree tree = rocket.PartTree;
        var removed = new List<RocketPart>();
        int[] expectedCounts = { 19, 13, 10, 7, 5 };

        for (int i = 1; i < rocket.Stages.Count; i++)
        {
            foreach (Joint joint in rocket.Stages[i].Joints)
            {
                foreach (RocketPart p in tree.GetSubtree(joint.ChildPart)) removed.Add(p);
                joint.Decouplers[0].TriggerSeparation();
            }
            Check($"after stage {i}: {expectedCounts[i]} parts attached",
                i < expectedCounts.Length && tree.ActiveParts.Count == expectedCounts[i], $"{tree.ActiveParts.Count}");
        }
        Check("lander left: pod, chute, fairing, tank_lm1, engine_land1",
            tree.ActiveParts.Exists(p => p.ModelId == "engine_land1") && tree.ActiveParts.Exists(p => p.ModelId == "tank_lm1"));
        foreach (RocketPart p in removed) p.Free();
        FreeTree(tree);
    }

    private void TestValidation()
    {
        var assembler = new RocketAssembler();
        List<string> errors = assembler.ValidateConnections(SampleDesigns.CreateMarsStack(), RocketAssembler.NoWeightLimit);
        Check("Mars stack is valid", errors.Count == 0, string.Join(" | ", errors));
        Check("Mars stack over weight 20 -> 1 error",
            assembler.ValidateConnections(SampleDesigns.CreateMarsStack(), 20).Count == 1);

        var noPod = new RocketDesign();
        noPod.AddPart("tank_s1", new Vector2I(4, 1));
        noPod.AddPart("engine_lv1", new Vector2I(4, 0));
        ExpectError(assembler, noPod, "no pod", "command pod");

        var noEngine = new RocketDesign();
        noEngine.AddPart("pod_mk1", new Vector2I(4, 2));
        noEngine.AddPart("tank_s1", new Vector2I(4, 0));
        ExpectError(assembler, noEngine, "no engine", "engine");

        var wrongFuel = new RocketDesign();
        wrongFuel.AddPart("pod_mk1", new Vector2I(4, 3));
        wrongFuel.AddPart("tank_s1", new Vector2I(4, 1));
        wrongFuel.AddPart("engine_vac1", new Vector2I(4, 0));
        ExpectError(assembler, wrongFuel, "Hydrolox engine under a Kerolox tank", "Hydrolox");

        var loose = MinimalDesign();
        loose.AddPart("tank_s1", new Vector2I(0, 10));
        ExpectError(assembler, loose, "part not connected", "not connected");

        var badDecoupler = MinimalDesign();
        badDecoupler.AddDecoupler("decoupler_d1", new Edge(new Vector2I(4, 3), Side.Top));
        ExpectError(assembler, badDecoupler, "decoupler on an empty edge", "Decoupler");

        var badModule = MinimalDesign();
        badModule.AddModule("battery_b1", new Vector2I(5, 5));
        ExpectError(assembler, badModule, "module outside the pod grid", "does not fit");

        var stacked = MinimalDesign();
        stacked.AddModule("imu_g1", new Vector2I(0, 0));
        stacked.AddModule("battery_b1", new Vector2I(0, 0));
        ExpectError(assembler, stacked, "two modules on one cell", "overlaps");

        var legacy = MinimalDesign();
        legacy.AddModule("imu_g1", PodInventory.Unplaced);
        legacy.AddModule("battery_b1", PodInventory.Unplaced);
        Check("unplaced (version 1) modules are valid", assembler.ValidateConnections(legacy, RocketAssembler.NoWeightLimit).Count == 0,
            string.Join(" | ", assembler.ValidateConnections(legacy, RocketAssembler.NoWeightLimit)));

        var overlap = MinimalDesign();
        overlap.AddPart("fin_f1", new Vector2I(4, 1));
        ExpectError(assembler, overlap, "overlapping parts", "misplaced");

        Check("minimal design is valid", assembler.ValidateConnections(MinimalDesign(), RocketAssembler.NoWeightLimit).Count == 0,
            string.Join(" | ", assembler.ValidateConnections(MinimalDesign(), RocketAssembler.NoWeightLimit)));

        AssembledRocket minimal = assembler.AssembleRocket(MinimalDesign());
        Check("minimal design assembles: 3 parts, 1 stage arming the engine",
            minimal.PartTree.ActiveParts.Count == 3 && minimal.Stages.Count == 1 && minimal.Stages[0].EnginesToArm.Count == 1);
        FreeTree(minimal.PartTree);
    }

    private static RocketDesign MinimalDesign()
    {
        var d = new RocketDesign();
        d.AddPart("pod_mk1", new Vector2I(4, 3));
        d.AddPart("tank_s1", new Vector2I(4, 1));
        d.AddPart("engine_lv1", new Vector2I(4, 0));
        return d;
    }

    private void ExpectError(RocketAssembler assembler, RocketDesign design, string name, string fragment)
    {
        List<string> errors = assembler.ValidateConnections(design, RocketAssembler.NoWeightLimit);
        bool ok = errors.Exists(e => e.Contains(fragment));
        Check($"invalid: {name}", ok, string.Join(" | ", errors));
        if (ok) GD.Print($"[A test]      -> {errors.Find(e => e.Contains(fragment))}");
    }

    private static bool FeedIs(RocketPart engine, RocketPart tank) =>
        engine is LiquidEnginePart e && tank is FuelTankPart t && e.FeedTanks.Count == 1 && e.FeedTanks[0] == t;

    private static RocketPart Find(PartTree tree, string modelId) => tree.ActiveParts.Find(p => p.ModelId == modelId);

    private static string Ids(List<EnginePart> engines)
    {
        var ids = new List<string>();
        foreach (EnginePart e in engines) ids.Add(e.ModelId);
        return string.Join(", ", ids);
    }

    private static void FreeTree(PartTree tree)
    {
        foreach (RocketPart part in new List<RocketPart>(tree.ActiveParts)) part.Free();
    }

    private void Check(string name, bool ok, string detail = "")
    {
        if (ok) _passed++;
        else _failed++;
        GD.Print($"[A test] {(ok ? "PASS" : "FAIL")} {name}{(ok || detail == "" ? "" : $" ({detail})")}");
    }

    private static bool Near(float a, float b, float tolerance = 0.001f) => Mathf.Abs(a - b) <= tolerance;
}
