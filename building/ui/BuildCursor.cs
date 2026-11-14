using Godot;
using RocketGame.Core;
using Side = RocketGame.Core.Side;

namespace RocketGame.Building;

/// <summary>
/// Mouse side of the build screen (A_BUILDING.md 5.5): ray onto z = 0, ghost of the selected part,
/// decoupler mode with edge highlight, grid lines, CoM marker and camera zoom/pan.
/// </summary>
public partial class BuildCursor : Node3D
{
    /// <summary>Selected palette entry changed (null = none).</summary>
    [Signal] public delegate void SelectionChangedEventHandler(string modelId);
    /// <summary>Decoupler mode toggled.</summary>
    [Signal] public delegate void DecouplerModeChangedEventHandler(bool enabled);

    [Export] public BuildingSystem Building { get; set; }
    [Export] public Camera3D Camera { get; set; }

    [ExportGroup("Look")]
    [Export] public Color GridColor { get; set; } = new(1f, 1f, 1f, 0.15f);
    [Export] public Color GridBorderColor { get; set; } = new(1f, 1f, 1f, 0.45f);
    [Export] public Color EdgeValidColor { get; set; } = new(0.3f, 1f, 0.4f);
    [Export] public Color EdgeInvalidColor { get; set; } = new(1f, 0.3f, 0.3f);
    [Export] public Color ComColor { get; set; } = new(1f, 0.85f, 0.1f);
    /// <summary>Edge highlight thickness, in cells.</summary>
    [Export(PropertyHint.Range, "0.02,0.5,0.01")] public float EdgeThickness { get; set; } = 0.12f;
    /// <summary>CoM marker radius, in cells.</summary>
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float ComMarkerRadius { get; set; } = 0.25f;

    [ExportGroup("Camera")]
    /// <summary>Ortho size = grid height x this.</summary>
    [Export(PropertyHint.Range, "0.5,3,0.05")] public float FrameMargin { get; set; } = 1.15f;
    [Export(PropertyHint.Range, "1.01,2,0.01")] public float ZoomFactor { get; set; } = 1.12f;
    [Export] public float MinOrthoSize { get; set; } = 6f;
    [Export] public float MaxOrthoSize { get; set; } = 120f;
    /// <summary>Screen fraction the grid is shifted right (room for the left palette).</summary>
    [Export(PropertyHint.Range, "-0.5,0.5,0.01")] public float HorizontalOffset { get; set; } = 0.02f;

    /// <summary>Palette selection (part or decoupler ModelId), or null.</summary>
    public string SelectedModelId { get; private set; }
    public bool DecouplerMode { get; private set; }
    /// <summary>Cell under the mouse, if any.</summary>
    public Vector2I? HoverCell { get; private set; }

    private Node3D _ghost;
    private MeshInstance3D _edgeMarker;
    private MeshInstance3D _comMarker;
    private MeshInstance3D _gridLines;
    private Edge? _hoverEdge;
    private string _decouplerId = SampleDesigns.DecouplerId;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Building, nameof(Building))) return;
        if (!NodeGuard.Require(this, Camera, nameof(Camera))) return;
        Callable.From(Setup).CallDeferred(); // Building.Grid exists after its _Ready
    }

    private void Setup()
    {
        DrawGrid();
        FrameCamera();
        _edgeMarker = new MeshInstance3D { Name = "EdgeMarker", Visible = false, Mesh = new BoxMesh(), MaterialOverride = Unshaded(EdgeValidColor) };
        AddChild(_edgeMarker);
        float r = ComMarkerRadius * Building.Grid.CellSize;
        _comMarker = new MeshInstance3D
        {
            Name = "ComMarker",
            Mesh = new SphereMesh { Radius = r, Height = r * 2f },
            MaterialOverride = Unshaded(ComColor),
            Visible = false,
        };
        AddChild(_comMarker);
        Building.DesignChanged += UpdateComMarker;
        UpdateComMarker();
    }

    /// <summary>Palette pick. Decoupler ids switch to decoupler mode.</summary>
    public void Select(string modelId)
    {
        bool isDecoupler = PartCatalog.Instance?.GetDefinition(modelId) is DecouplerPartDefinition;
        if (isDecoupler)
        {
            _decouplerId = modelId;
            SetDecouplerMode(true);
            return;
        }
        SetDecouplerMode(false);
        SelectedModelId = modelId;
        RebuildGhost();
        EmitSignal(SignalName.SelectionChanged, modelId ?? "");
    }

    public void ClearSelection()
    {
        SelectedModelId = null;
        RebuildGhost();
        SetDecouplerMode(false);
        EmitSignal(SignalName.SelectionChanged, "");
    }

    public void SetDecouplerMode(bool enabled)
    {
        if (DecouplerMode == enabled) return;
        DecouplerMode = enabled;
        if (enabled)
        {
            SelectedModelId = null;
            RebuildGhost();
        }
        if (_edgeMarker != null) _edgeMarker.Visible = false;
        EmitSignal(SignalName.DecouplerModeChanged, enabled);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Building?.Grid == null) return;

        if (@event is InputEventMouseButton { Pressed: true } wheel &&
            (wheel.ButtonIndex == MouseButton.WheelUp || wheel.ButtonIndex == MouseButton.WheelDown))
        {
            float factor = wheel.ButtonIndex == MouseButton.WheelUp ? 1f / ZoomFactor : ZoomFactor;
            Camera.Size = Mathf.Clamp(Camera.Size * factor, MinOrthoSize, MaxOrthoSize);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("build_toggle_decoupler"))
        {
            SetDecouplerMode(!DecouplerMode);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("build_cancel"))
        {
            ClearSelection();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("build_place"))
        {
            if (DecouplerMode)
            {
                if (_hoverEdge is Edge edge) Building.PlaceDecoupler(_decouplerId, edge);
            }
            else if (SelectedModelId != null && HoverCell is Vector2I cell)
            {
                Building.PlacePart(SelectedModelId, cell);
            }
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("build_remove"))
        {
            if (DecouplerMode)
            {
                if (_hoverEdge is Edge edge) Building.RemoveDecoupler(edge);
            }
            else if (HoverCell is Vector2I cell)
            {
                Building.RemovePart(cell);
            }
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (Building?.Grid == null || Camera == null) return;
        UpdateHover();
        UpdateGhost();
        UpdateEdgeMarker();
    }

    private void UpdateHover()
    {
        HoverCell = null;
        _hoverEdge = null;
        Vector2 mouse = GetViewport().GetMousePosition();
        if (GetViewport().GuiGetHoveredControl() != null) return; // over the UI panels
        Vector3 origin = Camera.ProjectRayOrigin(mouse);
        Vector3 dir = Camera.ProjectRayNormal(mouse);
        if (Mathf.IsZeroApprox(dir.Z)) return;
        float t = -origin.Z / dir.Z;
        if (t < 0f) return;
        Vector3 hit = origin + dir * t;

        float cell = Building.Grid.CellSize;
        var gridPos = new Vector2(hit.X / cell, hit.Y / cell);
        var c = new Vector2I(Mathf.FloorToInt(gridPos.X), Mathf.FloorToInt(gridPos.Y));
        if (!Building.Grid.IsInside(c)) return;
        HoverCell = c;

        // Nearest edge of the hovered cell.
        float fx = gridPos.X - c.X, fy = gridPos.Y - c.Y;
        float dTop = 1f - fy, dBottom = fy, dLeft = fx, dRight = 1f - fx;
        float min = Mathf.Min(Mathf.Min(dTop, dBottom), Mathf.Min(dLeft, dRight));
        Side side = min == dTop ? Side.Top : min == dBottom ? Side.Bottom : min == dLeft ? Side.Left : Side.Right;
        _hoverEdge = BuildGrid.Normalize(new Edge(c, side));
    }

    private void RebuildGhost()
    {
        _ghost?.QueueFree();
        _ghost = null;
        if (SelectedModelId == null || PartCatalog.Instance?.GetDefinition(SelectedModelId) is not PartDefinition def) return;
        PackedScene scene = RocketAssembler.GetVisualScene(def);
        if (scene == null) return;

        // A throwaway RocketPart drives Bind (footprint scale); it never joins the grid.
        RocketPart dummy = RocketPart.Create(def);
        dummy.Name = "Ghost";
        AddChild(dummy);
        RocketAssembler.AttachVisual(dummy);
        dummy.SetProcess(false);
        _ghost = dummy;
    }

    private void UpdateGhost()
    {
        if (_ghost is not RocketPart ghost) return;
        if (HoverCell is not Vector2I cell)
        {
            ghost.Visible = false;
            return;
        }
        ghost.Visible = true;
        ghost.Cell = cell;
        Vector2 local = ghost.GetLocalPosition();
        ghost.Position = new Vector3(local.X, local.Y, 0.05f);
        bool ok = Building.CanPlacePart(SelectedModelId, cell);
        Node visual = ghost.GetNodeOrNull("Visual");
        if (visual != null && visual.HasMethod("SetGhost")) visual.Call("SetGhost", ok);
    }

    private void UpdateEdgeMarker()
    {
        if (_edgeMarker == null) return;
        if (!DecouplerMode || _hoverEdge is not Edge edge)
        {
            _edgeMarker.Visible = false;
            return;
        }
        float cell = Building.Grid.CellSize;
        float thick = EdgeThickness * cell;
        bool top = edge.Side == Side.Top;
        Vector2 mid = top ? new Vector2(edge.Cell.X + 0.5f, edge.Cell.Y + 1f) : new Vector2(edge.Cell.X + 1f, edge.Cell.Y + 0.5f);
        _edgeMarker.Visible = true;
        _edgeMarker.Position = new Vector3(mid.X * cell, mid.Y * cell, 0.1f);
        ((BoxMesh)_edgeMarker.Mesh).Size = top ? new Vector3(cell, thick, thick) : new Vector3(thick, cell, thick);
        bool ok = Building.CanPlaceDecoupler(_decouplerId, edge) || Building.Grid.GetDecouplerAt(edge) != null;
        ((StandardMaterial3D)_edgeMarker.MaterialOverride).AlbedoColor = ok ? EdgeValidColor : EdgeInvalidColor;
    }

    private void UpdateComMarker()
    {
        if (_comMarker == null) return;
        MassProperties mp = Building.GetPreviewMass();
        _comMarker.Visible = mp.TotalMass > 0f;
        _comMarker.Position = new Vector3(mp.CenterOfMass.X, mp.CenterOfMass.Y, 0.3f);
    }

    private void DrawGrid()
    {
        BuildGrid grid = Building.Grid;
        float cell = grid.CellSize;
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        for (int x = 0; x <= grid.Width; x++)
        {
            mesh.SurfaceSetColor(x == 0 || x == grid.Width ? GridBorderColor : GridColor);
            mesh.SurfaceAddVertex(new Vector3(x * cell, 0f, -0.05f));
            mesh.SurfaceAddVertex(new Vector3(x * cell, grid.Height * cell, -0.05f));
        }
        for (int y = 0; y <= grid.Height; y++)
        {
            mesh.SurfaceSetColor(y == 0 || y == grid.Height ? GridBorderColor : GridColor);
            mesh.SurfaceAddVertex(new Vector3(0f, y * cell, -0.05f));
            mesh.SurfaceAddVertex(new Vector3(grid.Width * cell, y * cell, -0.05f));
        }
        mesh.SurfaceEnd();
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        _gridLines = new MeshInstance3D { Name = "GridLines", Mesh = mesh, MaterialOverride = material };
        AddChild(_gridLines);
    }

    /// <summary>Orthographic camera on +Z looking along -Z, framing the whole grid.</summary>
    private void FrameCamera()
    {
        BuildGrid grid = Building.Grid;
        float w = grid.Width * grid.CellSize, h = grid.Height * grid.CellSize;
        Camera.Projection = Camera3D.ProjectionType.Orthogonal;
        Camera.Size = h * FrameMargin;
        float aspect = GetViewport().GetVisibleRect().Size.Aspect();
        float shiftX = HorizontalOffset * Camera.Size * aspect;
        Camera.GlobalPosition = new Vector3(w * 0.5f + shiftX, h * 0.5f, 100f);
        Camera.GlobalRotation = Vector3.Zero;
        Camera.Far = 500f;
        Camera.Current = true;
    }

    private static StandardMaterial3D Unshaded(Color color) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = color,
        NoDepthTest = true,
    };
}
