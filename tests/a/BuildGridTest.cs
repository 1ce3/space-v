using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using Side = RocketGame.Core.Side;

namespace RocketGame.Tests;

/// <summary>
/// A3 check: run tests/a/build_grid_test.tscn (F6). Places the Mars stack (A_BUILDING.md 5.4) on a BuildGrid
/// by code, checks the neighbor rules, the budget and the JSON save/load. Needs the .tres in data/parts and data/modules.
/// </summary>
public partial class BuildGridTest : Node
{
    [Export] public bool QuitWhenDone { get; set; }
    /// <summary>Scratch file for the save/load check (the real save is not touched).</summary>
    [Export] public string TestSavePath { get; set; } = "user://test_design.json";

    private int _passed;
    private int _failed;
    private readonly List<RocketPart> _created = new();

    public override void _Ready()
    {
        GD.Print("[A test] ---- build grid test ----");
        TestCatalog();
        TestMarsStackPlacement();
        TestRejectedPlacements();
        TestDecouplerRules();
        TestJson();
        foreach (RocketPart part in _created) part.Free();
        _created.Clear();
        GD.Print($"[A test] ---- {_passed} passed, {_failed} failed ----");
        if (_failed > 0) GD.PushError($"[A test] {_failed} check(s) failed");
        if (QuitWhenDone) GetTree().Quit(_failed > 0 ? 1 : 0);
    }

    private void TestCatalog()
    {
        PartCatalog catalog = PartCatalog.Instance;
        Check("PartCatalog autoload exists", catalog != null);
        if (catalog == null) return;
        Check("38 definitions loaded", catalog.GetAll().Count == 38, $"{catalog.GetAll().Count}");
        Check("build_settings loaded (cell 2 m, 9 x 16)",
            catalog.Settings != null && Near(catalog.Settings.CellSize, 2f) && catalog.Settings.GridWidth == 9 && catalog.Settings.GridHeight == 16);
        Check("tank_m1 is a 1x3 Kerolox tank",
            catalog.GetDefinition("tank_m1") is FuelTankPartDefinition t && t.Footprint == new Vector2I(1, 3) && t.PropellantType == FuelType.Kerolox);
        Check("booster_m1 has a burn curve",
            catalog.GetDefinition("booster_m1") is SolidBoosterPartDefinition b && b.BurnCurve != null && Near(b.BurnCurve.Sample(1f), 0.8f, 0.01f));
        Check("decoupler_d1 is a DecouplerPartDefinition", catalog.GetDefinition("decoupler_d1") is DecouplerPartDefinition);
        Check("imu_g2 allows all 4 SAS modes",
            catalog.GetDefinition("imu_g2") is AcsModuleDefinition imu && imu.EnabledSasModes == (SasModeMask.StabilityAssist | SasModeMask.HoldPitch | SasModeMask.Prograde | SasModeMask.Retrograde));
    }

    private void TestMarsStackPlacement()
    {
        RocketDesign design = SampleDesigns.CreateMarsStack();
        BuildGrid grid = NewGrid();
        var budget = new BuildBudget(100);

        bool allPlaced = true;
        foreach (RocketDesign.PartEntry entry in design.PartEntries)
        {
            PartDefinition def = GetPart(entry.ModelId);
            bool ok = grid.CanPlace(def, entry.Cell);
            if (!ok)
            {
                allPlaced = false;
                GD.PushError($"[A test] CanPlace({entry.ModelId}, {entry.Cell}) = false");
                continue;
            }
            Place(grid, def, entry.Cell);
            budget.Add(def);
        }
        Check("every Mars stack part: CanPlace = true", allPlaced);
        Check("14 parts on the grid", grid.Parts.Count == 14, $"{grid.Parts.Count}");

        bool allDecouplers = true;
        foreach (RocketDesign.DecouplerEntry entry in design.DecouplerEntries)
        {
            PartDefinition def = GetPart(entry.ModelId);
            if (!grid.CanPlaceDecoupler(def, entry.Edge))
            {
                allDecouplers = false;
                GD.PushError($"[A test] CanPlaceDecoupler({entry.Edge}) = false");
                continue;
            }
            var decoupler = (DecouplerPart)Track(RocketPart.Create(def));
            grid.AddDecoupler(decoupler, entry.Edge);
            budget.Add(def);
        }
        Check("every Mars stack decoupler: CanPlaceDecoupler = true", allDecouplers);
        Check("same edge twice is refused", !grid.CanPlaceDecoupler(GetPart("decoupler_d1"), new Edge(new Vector2I(4, 7), Side.Top)));
        Check("Bottom form of a taken edge is refused too", !grid.CanPlaceDecoupler(GetPart("decoupler_d1"), new Edge(new Vector2I(4, 8), Side.Bottom)));

        foreach (RocketDesign.ModuleEntry entry in design.ModuleEntries)
            budget.Add(PartCatalog.Instance?.GetDefinition(entry.ModelId));

        // Parts 28 + decouplers 5 + modules 5.
        Check("budget of the Mars stack = 38", budget.CurrentWeight == 38, $"{budget.CurrentWeight}");
        Check("CanAfford false when over the max", !new BuildBudget(2).CanAfford(GetPart("engine_lv2")));

        RocketPart tankM = grid.GetPartAt(new Vector2I(4, 2));
        Check("GetPartAt(4,2) is tank_m1 (covers y 1-3)", tankM?.ModelId == "tank_m1");
        List<RocketPart> neighbors = grid.GetNeighbors(tankM);
        Check("tank_m1 has 4 neighbors (engine_vac1, engine_lv2, 2 boosters)", neighbors.Count == 4, $"{neighbors.Count}");
        RocketPart leftBooster = grid.GetPartAt(new Vector2I(3, 1));
        List<Edge> contacts = grid.GetContactEdges(leftBooster, tankM);
        Check("booster / tank_m1 touch on 3 edges", contacts.Count == 3, $"{contacts.Count}");
        Check("contact edges are normalized to Right of the booster cells",
            contacts.TrueForAll(e => e.Side == Side.Right && e.Cell.X == 3));
        Check("1 decoupler between left booster and tank_m1", grid.GetDecouplersBetween(leftBooster, tankM).Count == 1);
        Check("no orphan decouplers", grid.GetOrphanDecouplers().Count == 0);

        // Removing the heat shield orphans both decouplers around it.
        RocketPart shield = grid.GetPartAt(new Vector2I(4, 7));
        grid.Vacate(shield);
        Check("vacating heatshield_h1 orphans 2 decouplers", grid.GetOrphanDecouplers().Count == 2, $"{grid.GetOrphanDecouplers().Count}");
        grid.Occupy(shield);

        List<RocketPart> placed = grid.Parts;
        MassProperties mp = MassCenterCalculator.Calculate(placed);
        Check("CoM x = 9 m (symmetric around column 4)", Near(mp.CenterOfMass.X, 9f), $"{mp.CenterOfMass}");
        GD.Print($"[A test] Mars stack on grid (no payload, no modules): {mp.TotalMass:0} kg, CoM {mp.CenterOfMass}, I {mp.MomentOfInertia:0}");
    }

    private void TestRejectedPlacements()
    {
        BuildGrid grid = NewGrid();
        Place(grid, GetPart("tank_s1"), new Vector2I(4, 1));
        Check("pod under a tank: false", !grid.CanPlace(GetPart("pod_mk1"), new Vector2I(4, 0)));
        Check("pod on top of a tank: true", grid.CanPlace(GetPart("pod_mk1"), new Vector2I(4, 3)));
        Check("overlapping cell: false", !grid.CanPlace(GetPart("engine_lv1"), new Vector2I(4, 2)));
        Check("engine under the tank: true", grid.CanPlace(GetPart("engine_lv1"), new Vector2I(4, 0)));
        Check("outside the grid: false", !grid.CanPlace(GetPart("tank_s1"), new Vector2I(8, 15)));
        Check("negative cell: false", !grid.CanPlace(GetPart("fin_f1"), new Vector2I(-1, 0)));
        Check("fin beside a tank: true", grid.CanPlace(GetPart("fin_f1"), new Vector2I(3, 1)));
        Check("engine beside a tank: false", !grid.CanPlace(GetPart("engine_lv1"), new Vector2I(5, 1)));
        Check("null definition: false", !grid.CanPlace(null, new Vector2I(0, 0)));

        Place(grid, GetPart("pod_mk1"), new Vector2I(4, 3));
        Check("fin beside the pod: false", !grid.CanPlace(GetPart("fin_f1"), new Vector2I(5, 3)));
        Check("fairing on the pod: true", grid.CanPlace(GetPart("fairing_n1"), new Vector2I(4, 4)));
    }

    private void TestDecouplerRules()
    {
        BuildGrid grid = NewGrid();
        PartDefinition decoupler = GetPart("decoupler_d1");
        Place(grid, GetPart("tank_s1"), new Vector2I(4, 1));
        Place(grid, GetPart("engine_lv1"), new Vector2I(4, 0));
        Check("decoupler between tank and engine: true", grid.CanPlaceDecoupler(decoupler, new Edge(new Vector2I(4, 0), Side.Top)));
        Check("same edge written as Bottom of the tank: true", grid.CanPlaceDecoupler(decoupler, new Edge(new Vector2I(4, 1), Side.Bottom)));
        Check("edge inside one tank (y1/y2): false", !grid.CanPlaceDecoupler(decoupler, new Edge(new Vector2I(4, 1), Side.Top)));
        Check("edge with an empty side: false", !grid.CanPlaceDecoupler(decoupler, new Edge(new Vector2I(4, 2), Side.Top)));
        Check("non-decoupler definition: false", !grid.CanPlaceDecoupler(GetPart("structural_s1"), new Edge(new Vector2I(4, 0), Side.Top)));
        Check("Normalize(Bottom) = Top of the cell below",
            BuildGrid.Normalize(new Edge(new Vector2I(4, 1), Side.Bottom)) == new Edge(new Vector2I(4, 0), Side.Top));
        Check("Normalize(Left) = Right of the cell on the left",
            BuildGrid.Normalize(new Edge(new Vector2I(4, 1), Side.Left)) == new Edge(new Vector2I(3, 1), Side.Right));
    }

    private void TestJson()
    {
        RocketDesign design = SampleDesigns.CreateMarsStack();
        string json = design.Serialize();
        RocketDesign copy = RocketDesign.Deserialize(json);
        Check("JSON round trip keeps 14 / 5 / 5 entries",
            copy.PartEntries.Count == 14 && copy.DecouplerEntries.Count == 5 && copy.ModuleEntries.Count == 5);
        Check("JSON round trip keeps cells, edges and slots", SameDesign(design, copy));
        Check("JSON round trip is stable", copy.Serialize() == json);
        Check("invalid JSON gives an empty design", RocketDesign.Deserialize("{ not json").IsEmpty);
        Check("empty text gives an empty design", RocketDesign.Deserialize("").IsEmpty);

        Check("SaveTo writes the file", design.SaveTo(TestSavePath));
        RocketDesign loaded = RocketDesign.LoadFrom(TestSavePath);
        Check("LoadFrom reads the same design", loaded != null && SameDesign(design, loaded));
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(TestSavePath));
        Check("LoadFrom of a missing file returns null", RocketDesign.LoadFrom(TestSavePath) == null);
    }

    private static bool SameDesign(RocketDesign a, RocketDesign b)
    {
        if (a.PartEntries.Count != b.PartEntries.Count || a.DecouplerEntries.Count != b.DecouplerEntries.Count
            || a.ModuleEntries.Count != b.ModuleEntries.Count) return false;
        for (int i = 0; i < a.PartEntries.Count; i++)
            if (a.PartEntries[i].ModelId != b.PartEntries[i].ModelId || a.PartEntries[i].Cell != b.PartEntries[i].Cell) return false;
        for (int i = 0; i < a.DecouplerEntries.Count; i++)
            if (a.DecouplerEntries[i].ModelId != b.DecouplerEntries[i].ModelId || a.DecouplerEntries[i].Edge != b.DecouplerEntries[i].Edge) return false;
        for (int i = 0; i < a.ModuleEntries.Count; i++)
            if (a.ModuleEntries[i].ModelId != b.ModuleEntries[i].ModelId || a.ModuleEntries[i].Cell != b.ModuleEntries[i].Cell) return false;
        return true;
    }

    private static BuildGrid NewGrid()
    {
        BuildSettings s = PartCatalog.Instance?.Settings;
        return new BuildGrid(s?.GridWidth ?? 9, s?.GridHeight ?? 16, s?.CellSize ?? BuildSettings.DefaultCellSize);
    }

    private static PartDefinition GetPart(string modelId)
    {
        var def = PartCatalog.Instance?.GetDefinition(modelId) as PartDefinition;
        if (def == null) GD.PushError($"[A test] no part definition '{modelId}' in PartCatalog");
        return def;
    }

    private RocketPart Place(BuildGrid grid, PartDefinition def, Vector2I cell)
    {
        RocketPart part = Track(RocketPart.Create(def));
        if (part == null) return null;
        part.Cell = cell;
        grid.Occupy(part);
        return part;
    }

    private RocketPart Track(RocketPart part)
    {
        if (part != null) _created.Add(part);
        return part;
    }

    private void Check(string name, bool ok, string detail = "")
    {
        if (ok) _passed++;
        else _failed++;
        GD.Print($"[A test] {(ok ? "PASS" : "FAIL")} {name}{(ok || detail == "" ? "" : $" ({detail})")}");
    }

    private static bool Near(float a, float b, float tolerance = 0.001f) => Mathf.Abs(a - b) <= tolerance;
}
