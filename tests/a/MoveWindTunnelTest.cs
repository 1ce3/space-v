using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Tests;

/// <summary>
/// Sandbox for A9: MovePart (drag and drop rules, orphan decouplers, design entries, modules kept), part tooltips and
/// the wind tunnel (Mars stack and a small rocket with and without low fins). Unlocks the tech tree in memory only.
/// </summary>
public partial class MoveWindTunnelTest : Node
{
    /// <summary>Quit when done so the log is flushed.</summary>
    [Export] public bool QuitWhenDone { get; set; } = true;

    private int _ok, _fail;

    public override void _Ready() => Callable.From(Run).CallDeferred();

    private void Run()
    {
        if (ResearchCampaignEngine.Instance?.TechTree is TechTreeManager tree)
        {
            var ids = new List<string>();
            foreach (TechNode node in tree.AllNodes) if (node != null) ids.Add(node.NodeId);
            tree.LoadUnlockedNodes(ids);
        }

        BuildingSystem b = NewBuilding();
        b.LoadDesign(SampleDesigns.CreateMarsStack());
        Check($"Mars stack loaded ({b.Grid.Parts.Count} parts, {b.Grid.Decouplers.Count} decouplers, weight {b.Budget.CurrentWeight}/{b.Budget.MaxWeight})",
            b.Grid.Parts.Count == 14 && b.Validate().Count == 0);

        var tc = new TestConditions { AltitudeM = 10000f, SpeedMps = 400f, AngleOfAttackDeg = 5f };
        AeroReport mars = b.AnalyzeAero(tc);
        GD.Print($"[A9] Mars stack at 400 m/s, 10 km, 5 deg: drag {mars.DragN / 1000f:0.0} kN, side {mars.LiftN / 1000f:0.0} kN, " +
                 $"margin {mars.StabilityMarginM:0.0} m, stable {mars.IsStable}");
        Check("wind tunnel gives drag", mars.DragN > 0f);

        Check("booster cannot slide up next to engine_vac1", !b.MovePart(new Vector2I(5, 2), new Vector2I(5, 2)));
        Check("fin (6,1) -> (6,2) along the booster", b.MovePart(new Vector2I(6, 1), new Vector2I(6, 2)));
        Check("still valid", b.Validate().Count == 0);
        int weight = b.Budget.CurrentWeight, decouplers = b.Grid.Decouplers.Count;
        Check("fin (2,1) -> (0,8)", b.MovePart(new Vector2I(2, 1), new Vector2I(0, 8)));
        Check("booster (3,1) -> (1,1)", b.MovePart(new Vector2I(3, 2), new Vector2I(1, 1)));
        Check($"orphan decoupler removed and refunded ({decouplers} -> {b.Grid.Decouplers.Count}, weight {weight} -> {b.Budget.CurrentWeight})",
            b.Grid.Decouplers.Count == decouplers - 1 && b.Budget.CurrentWeight < weight);
        Check("validation reports the loose parts", b.Validate().Count > 0);
        Check("design entry follows the move", b.Design.PartEntries.Exists(e => e.ModelId == "booster_m1" && e.Cell == new Vector2I(1, 1)));
        Check("cannot move onto a taken cell", !b.MovePart(new Vector2I(4, 10), new Vector2I(4, 9)));
        Check("pod modules kept", b.InstalledModules.Count == 5);

        BuildingSystem s = NewBuilding();
        s.PlacePart("pod_mk1", new Vector2I(4, 5));
        s.PlacePart("tank_s1", new Vector2I(4, 3));
        s.PlacePart("tank_s1", new Vector2I(4, 1));
        s.PlacePart("engine_lv1", new Vector2I(4, 0));
        AeroReport noFins = s.AnalyzeAero(tc);
        s.PlacePart("fin_f1", new Vector2I(3, 1));
        s.PlacePart("fin_f1", new Vector2I(5, 1));
        AeroReport fins = s.AnalyzeAero(tc);
        GD.Print($"[A9] Small rocket: no fins margin {noFins.StabilityMarginM:0.0} m (stable {noFins.IsStable}), low fins margin {fins.StabilityMarginM:0.0} m (stable {fins.IsStable})");
        Check("low fins make the small rocket stable", !noFins.IsStable && fins.IsStable);

        foreach (string id in new[] { "engine_lv2", "booster_m1", "pod_mk2", "chute_p1", "solar_s1" })
            GD.Print($"[A9] Tooltip {id}: {PartCatalog.Instance.GetDefinition(id)?.GetFormattedTooltip().Replace("\n", " | ")}");

        GD.Print($"[A9] Done: {_ok} OK, {_fail} FAIL. The tech tree unlock was in memory only.");
        if (QuitWhenDone) GetTree().Quit();
    }

    private BuildingSystem NewBuilding()
    {
        var root = new Node3D();
        AddChild(root);
        var b = new BuildingSystem { PartsRoot = root, LoadSavedOnReady = false };
        AddChild(b);
        return b;
    }

    private void Check(string label, bool ok)
    {
        if (ok) _ok++;
        else _fail++;
        GD.Print($"[A9] {(ok ? "OK  " : "FAIL")} {label}");
    }
}
