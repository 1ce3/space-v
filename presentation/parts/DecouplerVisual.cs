using Godot;
using RocketGame.Building;
using RocketGame.Core;
using Side = RocketGame.Core.Side;

namespace RocketGame.Presentation;

/// <summary>Decoupler ring; a short flash and a puff of light when its joint separates.</summary>
public partial class DecouplerVisual : PartVisual
{
    [Export] public Color FlashColor { get; set; } = new(1f, 0.95f, 0.8f, 0.85f);
    [Export] public float FlashTimeS { get; set; } = 0.7f;
    /// <summary>Flash sphere radius at its peak (m).</summary>
    [Export] public float FlashRadiusM { get; set; } = 4f;
    [Export] public float FlashLightEnergy { get; set; } = 6f;

    public bool HasSeparated { get; private set; }

    private MeshInstance3D _flash;
    private OmniLight3D _light;
    private StandardMaterial3D _flashMaterial;
    private float _flashT = -1f;

    /// <summary>Lies flat on Top edges; stands up on Left/Right edges (side boosters).</summary>
    public override void Bind(RocketPart part)
    {
        base.Bind(part);
        if (MeshRoot != null && part is DecouplerPart decoupler && (decoupler.Edge.Side == Side.Right || decoupler.Edge.Side == Side.Left))
            MeshRoot.RotationDegrees = new Vector3(0f, 0f, 90f);
        HasSeparated = part is DecouplerPart { Joint.IsSeparated: true };
        if (part is DecouplerPart { Edge.Side: Side.Top }) EnsureShell();
    }

    // ------------------------------------------------------------------ A12: interstage shell

    /// <summary>Interstage shell around a stacked decoupler: width of the stage, this share of a cell tall.</summary>
    [Export(PropertyHint.Range, "0.1,1,0.05")] public float ShellHeightCells { get; set; } = 0.5f;
    /// <summary>Shell radius as a share of half a cell (slightly wider than the tanks).</summary>
    [Export(PropertyHint.Range, "0.8,1.3,0.01")] public float ShellRadiusFactor { get; set; } = 1.06f;
    [Export] public Color ShellDefaultColor { get; set; } = new(0.75f, 0.77f, 0.8f);

    private MeshInstance3D _shell;
    private StandardMaterial3D _shellMaterial;
    private RocketGame.Building.MaterialDefinition _shellMaterialDef;

    private void EnsureShell()
    {
        if (_shell != null) return;
        float cell = RocketPart.GetCellSize();
        float r = 0.5f * cell * ShellRadiusFactor;
        _shellMaterial = new StandardMaterial3D { AlbedoColor = ShellDefaultColor, Roughness = 0.6f };
        // Children of the root (not MeshRoot): sizes are in meters.
        _shell = new MeshInstance3D
        {
            Name = "InterstageShell",
            Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = ShellHeightCells * cell, RadialSegments = 24 },
            MaterialOverride = _shellMaterial,
        };
        AddChild(_shell);
        UpdateShellColor();
    }

    private void UpdateShellColor()
    {
        if (_shell == null || Part == null) return;
        _shellMaterialDef = Part.Material;
        _shellMaterial.AlbedoColor = _shellMaterialDef != null ? ShellDefaultColor * _shellMaterialDef.Tint : ShellDefaultColor;
    }

    protected override void UpdateVisual(float deltaTime)
    {
        base.UpdateVisual(deltaTime);
        if (_shell != null && Part.Material != _shellMaterialDef) UpdateShellColor();
        if (!HasSeparated && Part is DecouplerPart { Joint.IsSeparated: true }) PlaySeparation();
        if (_flashT < 0f) return;

        _flashT += deltaTime;
        float t = FlashTimeS > 0f ? _flashT / FlashTimeS : 1f;
        if (t >= 1f)
        {
            _flashT = -1f;
            _flash.Visible = false;
            _light.Visible = false;
            return;
        }
        float fade = 1f - t;
        float r = FlashRadiusM * Mathf.Lerp(0.3f, 1f, Mathf.Sqrt(t));
        _flash.Scale = new Vector3(r, r, r);
        _flashMaterial.AlbedoColor = new Color(FlashColor, FlashColor.A * fade);
        _light.LightEnergy = FlashLightEnergy * fade;
    }

    /// <summary>Starts the separation flash (once).</summary>
    public void PlaySeparation()
    {
        if (HasSeparated) return;
        HasSeparated = true;
        EnsureFlash();
        _flashT = 0f;
        _flash.Visible = true;
        _light.Visible = true;
    }

    private void EnsureFlash()
    {
        if (_flash != null) return;
        _flashMaterial = MakeGlowMaterial(FlashColor);
        _flash = new MeshInstance3D
        {
            Name = "SeparationFlash",
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = _flashMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        _light = new OmniLight3D { Name = "SeparationLight", LightColor = FlashColor, OmniRange = FlashRadiusM * 6f, Visible = false };
        // Children of the root (not MeshRoot): sizes are in meters.
        AddChild(_flash);
        AddChild(_light);
    }
}
