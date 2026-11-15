using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Tests;

/// <summary>
/// Run tests/a/engine_model_preview.tscn (F6). Fits every part that has its own model (.glb) into its group placeholder
/// the same way the game does (RocketAssembler.FitModel) and lays them out in a row, each beside the bare placeholder.
/// Red dot = ExhaustPoint (flame origin): it should sit at the nozzle exit. Prints size checks; stays open.
/// </summary>
public partial class EngineModelPreview : Node3D
{
    [Export] public float Spacing { get; set; } = 3.2f;

    private int _passed;
    private int _failed;

    public override void _Ready()
    {
        PartCatalog catalog = PartCatalog.Instance;
        if (catalog == null) { GD.PrintErr("[Model preview] no PartCatalog autoload"); return; }

        var withModel = new List<PartDefinition>();
        foreach (ComponentDefinition def in catalog.GetAll())
            if (def is PartDefinition part && part.Scene != null) withModel.Add(part);
        withModel.Sort((a, b) => string.CompareOrdinal(a.ModelId, b.ModelId));
        Check("parts with their own model found", withModel.Count > 0, $"{withModel.Count}");

        float x = 0f;
        // Reference: the bare placeholder of the first group, so sizes can be compared.
        if (withModel.Count > 0) AddColumn(null, catalog.GetPlaceholderScene(withModel[0].PartGroup), x, "placeholder");
        foreach (PartDefinition part in withModel)
        {
            x += Spacing;
            AddColumn(part, catalog.GetPlaceholderScene(part.PartGroup), x, part.DisplayName);
        }
        GD.Print($"[Model preview] {_passed} passed, {_failed} failed");
    }

    private void AddColumn(PartDefinition part, PackedScene placeholder, float x, string label)
    {
        if (placeholder == null) { Check($"{label}: placeholder exists", false); return; }
        var visual = placeholder.Instantiate<Node3D>();
        visual.Position = new Vector3(x, 0f, 0f);
        AddChild(visual);
        if (part != null)
        {
            Node model = part.Scene.Instantiate();
            bool isModel = model is Node3D;
            Check($"{part.ModelId}: model root is Node3D", isModel);
            if (!isModel) { model.Free(); return; }
            int meshes = model.FindChildren("*", nameof(MeshInstance3D), true, false).Count;
            Check($"{part.ModelId}: model has meshes", meshes > 0, $"{meshes}");
            RocketAssembler.FitModel(visual, (Node3D)model);
            var m3 = (Node3D)model;
            Check($"{part.ModelId}: fitted scale is sane", m3.Scale.X > 0.01f && m3.Scale.X < 100f, $"scale {m3.Scale.X:0.###}");
            GD.Print($"[Model preview] {part.ModelId}: scale {m3.Scale.X:0.###}, meshes {meshes}");
        }
        if (visual.FindChild("ExhaustPoint", true, false) is Node3D exhaust)
        {
            var dot = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f } };
            dot.MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.Red, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true };
            AddChild(dot);
            dot.GlobalPosition = exhaust.GlobalPosition;
        }
        var text = new Label3D { Text = label, FontSize = 40, PixelSize = 0.006f, Position = new Vector3(x, -1.6f, 0.5f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled };
        AddChild(text);
    }

    private void Check(string name, bool ok, string detail = "")
    {
        if (ok) _passed++; else _failed++;
        GD.Print($"[Model preview] {(ok ? "OK  " : "FAIL")} {name}{(detail == "" ? "" : $" ({detail})")}");
    }
}
