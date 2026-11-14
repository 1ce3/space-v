using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Presentation;
using RocketGame.Research;

namespace RocketGame.Tests;

/// <summary>
/// A5 check: run tests/a/visual_test.tscn (F6). Validates the catalog against the tech tree and the part scenes,
/// then shows the assembled Mars stack with its placeholder visuals (stays open; close the window to end).
/// </summary>
public partial class VisualTest : Node3D
{
    /// <summary>Parent of the rocket parts.</summary>
    [Export] public Node3D Vehicle { get; set; }

    private int _passed;
    private int _failed;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Vehicle, nameof(Vehicle))) return;
        GD.Print("[A test] ---- visual / catalog test ----");
        // Autoload validation runs deferred; run the checks one frame later too.
        Callable.From(Run).CallDeferred();
    }

    private void Run()
    {
        PartCatalog catalog = PartCatalog.Instance;
        Check("PartCatalog autoload exists", catalog != null);
        if (catalog == null) return;

        TechTreeManager techTree = ResearchCampaignEngine.Instance?.TechTree;
        Check("tech tree available", techTree != null);
        List<string> techProblems = catalog.ValidateAgainstTechTree();
        Check("ValidateAgainstTechTree: no problems", techProblems.Count == 0, string.Join(" | ", techProblems));
        List<string> sceneProblems = catalog.ValidateScenes();
        Check("ValidateScenes: no problems", sceneProblems.Count == 0, string.Join(" | ", sceneProblems));

        foreach (PartGroup group in System.Enum.GetValues<PartGroup>())
            Check($"placeholder scene for {group}", catalog.GetPlaceholderScene(group) != null);
        Check("placeholder scene for decouplers", catalog.GetDecouplerPlaceholderScene() != null);

        List<PartDefinition> parts = catalog.GetUnlocked<PartDefinition>();
        List<ModuleDefinition> modules = catalog.GetUnlocked<ModuleDefinition>();
        GD.Print($"[A test] unlocked now: {parts.Count} parts, {modules.Count} modules (depends on the save)");
        if (techTree != null)
        {
            Check("every unlocked part is unlocked in the tech tree", parts.TrueForAll(p => techTree.IsUnlocked(p.ModelId)));
            Check("starter pod_mk1 is in the palette", parts.Exists(p => p.ModelId == "pod_mk1"));
        }

        AssembledRocket rocket = TestRocketFactory.CreateMarsStack();
        Check("Mars stack assembled", rocket != null);
        if (rocket == null) return;
        int withVisual = 0;
        foreach (RocketPart part in rocket.PartTree.ActiveParts)
        {
            Vehicle.AddChild(part);
            if (part.GetNodeOrNull<PartVisual>("Visual") is { Part: not null }) withVisual++;
        }
        Check("every part has a bound PartVisual (19)", withVisual == 19, $"{withVisual}");
        var engineVisual = rocket.PartTree.GetParts<LiquidEnginePart>()[0].GetNodeOrNull<EngineVisual>("Visual");
        Check("liquid engine uses EngineVisual", engineVisual != null);
        var tank = rocket.PartTree.ActiveParts.Find(p => p.ModelId == "tank_m1");
        Check("tank_m1 MeshRoot scaled to 1 x 3 cells", tank?.GetNodeOrNull<PartVisual>("Visual")?.MeshRoot?.Scale.Y is float y && Mathf.IsEqualApprox(y, 6f));

        GD.Print($"[A test] ---- {_passed} passed, {_failed} failed ---- (window shows the Mars stack)");
        if (_failed > 0) GD.PushError($"[A test] {_failed} check(s) failed");
    }

    private void Check(string name, bool ok, string detail = "")
    {
        if (ok) _passed++;
        else _failed++;
        GD.Print($"[A test] {(ok ? "PASS" : "FAIL")} {name}{(ok || detail == "" ? "" : $" ({detail})")}");
    }
}
