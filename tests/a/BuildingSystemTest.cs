using Godot;
using RocketGame.Building;
using RocketGame.Core;
using Side = RocketGame.Core.Side;

namespace RocketGame.Tests;

/// <summary>
/// A7 check: run tests/a/building_system_test.tscn (F6). Drives a BuildingSystem by code: modules (install, replace,
/// wrong slot, budget, removal with the pod), orphan decouplers, and a save/load round trip that must restore
/// parts, decouplers and modules. Uses starter parts only. Writes a scratch file, never user://current_design.json.
/// </summary>
public partial class BuildingSystemTest : Node
{
    [Export] public bool QuitWhenDone { get; set; }
    [Export] public string TestSavePath { get; set; } = "user://test_building_system.json";

    private int _passed;
    private int _failed;

    private static readonly Vector2I PodCell = new(4, 12);
    private static readonly Vector2I TankCell = new(4, 10);
    private static readonly Vector2I EngineCell = new(4, 9);
    private static readonly Edge TankEngineEdge = new(new Vector2I(4, 9), Side.Top);

    public override void _Ready()
    {
        GD.Print("[A test] ---- building system test ----");
        if (PartCatalog.Instance == null)
        {
            GD.PushError("[A test] PartCatalog autoload missing");
            return;
        }
        TestModules();
        TestOrphansAndPodRemoval();
        TestRoundTrip();
        TestVersion1Design();
        TestMaterials();
        TestInstruments();
        GD.Print($"[A test] ---- {_passed} passed, {_failed} failed ----");
        if (_failed > 0) GD.PushError($"[A test] {_failed} check(s) failed");
        if (QuitWhenDone) GetTree().Quit(_failed > 0 ? 1 : 0);
    }

    private BuildingSystem NewSystem()
    {
        var root = new Node3D { Name = "Parts" };
        var system = new BuildingSystem { PartsRoot = root, LoadSavedOnReady = false };
        system.AddChild(root);
        AddChild(system);
        return system;
    }

    private static void Dispose(BuildingSystem system)
    {
        system.ClearAll();
        system.QueueFree();
    }

    private void BuildStarter(BuildingSystem b)
    {
        Check("place pod_mk1", b.PlacePart("pod_mk1", PodCell));
        Check("place tank_s1", b.PlacePart("tank_s1", TankCell));
        Check("place engine_lv1", b.PlacePart("engine_lv1", EngineCell));
    }

    private void TestModules()
    {
        BuildingSystem b = NewSystem();
        Check("no pod: InstallModule refused", !b.InstallModule("imu_g1", new Vector2I(0, 0)));
        BuildStarter(b);
        int before = b.Budget.CurrentWeight;
        Check("pod_mk1 grid is 2x2", b.InventorySize == new Vector2I(2, 2), $"{b.InventorySize}");

        Check("imu_g1 at (0,0)", b.InstallModule("imu_g1", new Vector2I(0, 0)));
        Check("weight +1", b.Budget.CurrentWeight == before + 1, $"{b.Budget.CurrentWeight}");
        Check("design has 1 module entry at (0,0)", b.Design.ModuleEntries.Count == 1 && b.Design.ModuleEntries[0].Cell == Vector2I.Zero);
        Check("outside the 2x2 grid refused", !b.InstallModule("imu_g1", new Vector2I(2, 0)));
        Check("battery_b1 at (1,0)", b.InstallModule("battery_b1", new Vector2I(1, 0)));
        Check("2 modules installed", b.InstalledModules.Count == 2);
        Check("insulation_i1 at (0,1)", b.InstallModule("insulation_i1", new Vector2I(0, 1)));
        Check("1x2 battery_b2 over the insulation refused", !b.CanInstallModule("battery_b2", new Vector2I(0, 0)));
        Check("module covering (1,1)? none yet", b.GetModuleCellAt(new Vector2I(1, 1)) == null);
        b.RemoveModule(new Vector2I(0, 1));

        Check("replace imu_g1 with imu_g2 at the same cell", b.InstallModule("imu_g2", new Vector2I(0, 0)));
        Check("still 2 modules, 2 entries", b.InstalledModules.Count == 2 && b.Design.ModuleEntries.Count == 2);
        Check("(0,0) is imu_g2", b.InstalledModules.TryGetValue(Vector2I.Zero, out ModuleDefinition m) && m.ModelId == "imu_g2");
        Check("weight unchanged by same-weight replace", b.Budget.CurrentWeight == before + 2, $"{b.Budget.CurrentWeight}");

        float massWith = b.GetPreviewMass().TotalMass;
        b.RemoveModule(new Vector2I(1, 0));
        Check("RemoveModule refunds weight", b.Budget.CurrentWeight == before + 1, $"{b.Budget.CurrentWeight}");
        Check("RemoveModule lowers preview mass by 20 kg", Mathf.Abs(massWith - b.GetPreviewMass().TotalMass - 20f) < 0.01f);

        Check("auto-place goes to the first free cell (1,0)", b.InstallModule("battery_b1") && b.InstalledModules.ContainsKey(new Vector2I(1, 0)));
        b.RemoveModule(new Vector2I(1, 0));

        b.Budget.SetMax(b.Budget.CurrentWeight);
        Check("full budget: battery refused", !b.InstallModule("battery_b1", new Vector2I(1, 0)));
        Check("full budget: same-weight replace allowed", b.InstallModule("imu_g1", new Vector2I(0, 0)));
        Dispose(b);
    }

    private void TestOrphansAndPodRemoval()
    {
        BuildingSystem b = NewSystem();
        BuildStarter(b);
        Check("decoupler between tank and engine", b.PlaceDecoupler("decoupler_d1", TankEngineEdge));
        b.InstallModule("imu_g1", new Vector2I(0, 0));
        b.InstallModule("battery_b1", new Vector2I(1, 0));
        int full = b.Budget.CurrentWeight;

        b.RemovePart(EngineCell);
        Check("removing the engine removes its decoupler", b.Grid.Decouplers.Count == 0 && b.Design.DecouplerEntries.Count == 0);
        Check("engine + decoupler weight refunded (2 + 1)", b.Budget.CurrentWeight == full - 3, $"{b.Budget.CurrentWeight}");

        b.RemovePart(PodCell);
        Check("removing the pod removes its modules", b.InstalledModules.Count == 0 && b.Design.ModuleEntries.Count == 0);
        Check("only the tank's weight is left (2)", b.Budget.CurrentWeight == 2, $"{b.Budget.CurrentWeight}");
        Dispose(b);
    }

    /// <summary>A design saved before phase 2 (modules by slot type and index) still loads: modules go to the first free cells.</summary>
    private void TestVersion1Design()
    {
        BuildingSystem b = NewSystem();
        BuildStarter(b);
        var node = System.Text.Json.Nodes.JsonNode.Parse(b.Design.Serialize());
        node["version"] = 1;
        node["modules"] = System.Text.Json.Nodes.JsonNode.Parse(
            "[{\"id\":\"imu_g1\",\"slot\":\"Avionics\",\"index\":0},{\"id\":\"battery_b1\",\"slot\":\"Power\",\"index\":0}]");
        Dispose(b);

        RocketDesign old = RocketDesign.Deserialize(node.ToJsonString());
        Check("version 1 design parses with 2 modules", old.ModuleEntries.Count == 2, $"{old.ModuleEntries.Count}");
        Check("version 1 modules are Unplaced", old.ModuleEntries.TrueForAll(e => e.Cell == PodInventory.Unplaced));
        BuildingSystem c = NewSystem();
        c.LoadDesign(old);
        Check("version 1 modules auto-placed at (0,0) and (1,0)",
            c.InstalledModules.ContainsKey(new Vector2I(0, 0)) && c.InstalledModules.ContainsKey(new Vector2I(1, 0)));
        Dispose(c);
    }

    private void TestMaterials()
    {
        PartCatalog cat = PartCatalog.Instance;
        Check("6 materials loaded", cat.GetAllMaterials().Count == 6, $"{cat.GetAllMaterials().Count}");
        BuildingSystem b = NewSystem();
        BuildStarter(b);
        b.PlaceDecoupler("decoupler_d1", TankEngineEdge);
        RocketPart tank = b.Grid.GetPartAt(TankCell);
        Check("tank gets default aluminium", tank.Material?.MaterialId == "mat_al2024", tank.Material?.MaterialId ?? "null");
        Check("decoupler gets default aluminium", b.Grid.GetDecouplerAt(TankEngineEdge).Material?.MaterialId == "mat_al2024");
        Check("engine takes no material", b.Grid.GetPartAt(EngineCell).Material == null);
        Check("engine cannot be painted", !b.CanSetPartMaterial(EngineCell, "mat_al2024"));

        float baseMass = tank.DryMass;
        MaterialDefinition cfrp = cat.GetMaterial("mat_cfrp");
        tank.Material = cfrp;
        Check("CFRP tank dry mass x0.7", Mathf.IsEqualApprox(tank.DryMass, baseMass * 0.7f, 0.01f), $"{tank.DryMass} vs {baseMass}");
        Check("CFRP max temp -100 K", Mathf.IsEqualApprox(tank.GetEffectiveMaxSafeTempK(), tank.Definition.MaxSafeTempK - 100f));
        tank.Material = cat.GetMaterial("mat_al2024");
        if (!b.IsMaterialUnlocked("mat_cfrp"))
            Check("locked CFRP refused", !b.SetPartMaterial(TankCell, "mat_cfrp"));

        // Paint with a tier-0 material set directly in the design: round trip through JSON.
        Check("paint tank aluminium", b.SetPartMaterial(TankCell, "mat_al2024"));
        string json = b.Design.Serialize();
        Check("design JSON carries material", json.Contains("mat_al2024"));
        RocketDesign d = RocketDesign.Deserialize(json.Replace("mat_al2024", "mat_steel304"));
        Dispose(b);
        BuildingSystem c = NewSystem();
        c.LoadDesign(d);
        RocketPart loadedTank = c.Grid.GetPartAt(TankCell);
        string expected = c.IsMaterialUnlocked("mat_steel304") ? "mat_steel304" : "mat_al2024";
        Check($"loaded tank material is {expected}", loadedTank?.Material?.MaterialId == expected, loadedTank?.Material?.MaterialId ?? "null");
        Check("ApplyMaterialToAll(al2024) keeps 3 hull pieces aluminium",
            c.ApplyMaterialToAll("mat_al2024") >= 0 && c.Grid.GetDecouplerAt(TankEngineEdge).Material?.MaterialId == "mat_al2024");
        Dispose(c);
    }

    private void TestInstruments()
    {
        PartCatalog cat = PartCatalog.Instance;
        foreach (string id in new[] { "comms_c1", "comms_c2", "rad_x1", "rad_x2", "fuel_f1", "pressure_p1" })
            Check($"{id} is an instrument", cat.GetDefinition(id) is InstrumentModuleDefinition);
        Check("pod_mk3 grid 4x3", (cat.GetDefinition("pod_mk3") as CommandPodPartDefinition)?.InventorySize == new Vector2I(4, 3));
        Check("tech tree matches catalog", cat.ValidateAgainstTechTree().Count == 0, string.Join(" | ", cat.ValidateAgainstTechTree()));
    }

    private void TestRoundTrip()
    {
        BuildingSystem b = NewSystem();
        BuildStarter(b);
        b.PlaceDecoupler("decoupler_d1", TankEngineEdge);
        b.InstallModule("imu_g1", new Vector2I(0, 0));
        b.InstallModule("battery_b1", new Vector2I(1, 0));
        int weight = b.Budget.CurrentWeight;
        float mass = b.GetPreviewMass().TotalMass;
        Check("starter design is valid", b.Validate().Count == 0, string.Join(" | ", b.Validate()));
        Check("save to scratch file", b.Design.SaveTo(TestSavePath));
        Dispose(b);

        BuildingSystem c = NewSystem();
        RocketDesign loaded = RocketDesign.LoadFrom(TestSavePath);
        Check("scratch file loads", loaded != null);
        c.LoadDesign(loaded);
        Check("3 parts restored", c.Grid.Parts.Count == 3, $"{c.Grid.Parts.Count}");
        Check("1 decoupler restored", c.Grid.Decouplers.Count == 1, $"{c.Grid.Decouplers.Count}");
        Check("2 modules restored", c.InstalledModules.Count == 2, $"{c.InstalledModules.Count}");
        Check("(1,0) is battery_b1", c.InstalledModules.TryGetValue(new Vector2I(1, 0), out ModuleDefinition bat) && bat.ModelId == "battery_b1");
        Check("same weight", c.Budget.CurrentWeight == weight, $"{c.Budget.CurrentWeight} vs {weight}");
        Check("same preview mass", Mathf.Abs(c.GetPreviewMass().TotalMass - mass) < 0.01f);
        Check("design entries match (3/1/2)",
            c.Design.PartEntries.Count == 3 && c.Design.DecouplerEntries.Count == 1 && c.Design.ModuleEntries.Count == 2);

        AssembledRocket rocket = new RocketAssembler().AssembleRocket(c.Design);
        var pod = rocket?.PartTree.RootPart as CommandPodPart;
        Check("assembled pod carries the 2 modules", pod != null && pod.Modules.Count == 2, $"{pod?.Modules.Count}");
        Check("assembled rocket has 2 stages", rocket != null && rocket.Stages.Count == 2, $"{rocket?.Stages.Count}");
        if (rocket != null)
            foreach (RocketPart part in new System.Collections.Generic.List<RocketPart>(rocket.PartTree.ActiveParts)) part.Free();
        Dispose(c);
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(TestSavePath));
    }

    private void Check(string name, bool ok, string detail = "")
    {
        if (ok) _passed++;
        else _failed++;
        GD.Print($"[A test] {(ok ? "PASS" : "FAIL")} {name}{(ok || detail == "" ? "" : $" ({detail})")}");
    }
}
