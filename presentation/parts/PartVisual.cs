using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Presentation;

/// <summary>
/// Root script of every part scene (model team contract). Reads its RocketPart each frame; never writes game state.
/// Placeholder meshes are modelled for a 1 x 1 x 1 cell; Bind scales MeshRoot to Footprint * CellSize.
/// </summary>
public partial class PartVisual : Node3D
{
    /// <summary>Holds the meshes; scaled to the part footprint by Bind.</summary>
    [Export] public Node3D MeshRoot { get; set; }
    /// <summary>Color used by placeholder meshes.</summary>
    [Export] public Color GroupColor { get; set; } = Colors.White;
    /// <summary>Temperature ratio where the glow starts (TempK / MaxSafeTempK).</summary>
    [Export(PropertyHint.Range, "0,2,0.01")] public float GlowStartRatio { get; set; } = 0.6f;
    /// <summary>Temperature ratio of full glow.</summary>
    [Export(PropertyHint.Range, "0,3,0.01")] public float GlowFullRatio { get; set; } = 1.25f;
    [Export] public Color GlowColor { get; set; } = new(1f, 0.35f, 0.05f);
    [Export(PropertyHint.Range, "0,20,0.1")] public float GlowEnergy { get; set; } = 3f;
    /// <summary>Tint of a Broken part.</summary>
    [Export] public Color BrokenColor { get; set; } = new(0.15f, 0.13f, 0.12f);
    /// <summary>Tint of a Frozen part.</summary>
    [Export] public Color FrozenColor { get; set; } = new(0.6f, 0.8f, 1f);
    /// <summary>Darkening applied when the part breaks off (SeverCause.Break).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float BreakDarken { get; set; } = 0.5f;
    /// <summary>When true the part keeps its own mesh materials (real models); placeholders use GroupColor.</summary>
    [Export] public bool KeepMeshMaterials { get; set; }

    /// <summary>The part shown; null in the palette.</summary>
    public RocketPart Part { get; private set; }

    private StandardMaterial3D _material;
    private Color _baseColor;

    /// <summary>Attaches to a part and scales MeshRoot to its footprint.</summary>
    public virtual void Bind(RocketPart part)
    {
        Part = part;
        float cell = RocketPart.GetCellSize();
        Vector2I footprint = part?.Footprint ?? Vector2I.One;
        if (MeshRoot != null) MeshRoot.Scale = new Vector3(footprint.X * cell, footprint.Y * cell, cell);
        _baseColor = GroupColor;
        EnsureMaterial();
        if (_material != null) _material.AlbedoColor = _baseColor;
    }

    /// <summary>Build-screen ghost: tinted valid or invalid.</summary>
    public virtual void SetGhost(bool isValid)
    {
        EnsureMaterial();
        if (_material == null) return;
        BuildSettings settings = PartCatalog.Instance?.Settings;
        Color tint = isValid ? settings?.GhostValidColor ?? new Color(0.3f, 1f, 0.4f, 0.5f)
                             : settings?.GhostInvalidColor ?? new Color(1f, 0.3f, 0.3f, 0.5f);
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _material.AlbedoColor = tint;
    }

    /// <summary>Mirrors the visual for left/right side parts.</summary>
    public virtual void SetMirrored(bool mirrored)
    {
        // TODO(A8)
    }

    /// <summary>Called by debris spawning when the part leaves the rocket.</summary>
    public virtual void OnSevered(SeverCause cause)
    {
        if (cause != SeverCause.Break) return;
        _baseColor = _baseColor.Darkened(BreakDarken);
        if (_material != null) _material.AlbedoColor = _baseColor;
    }

    public override void _Process(double delta)
    {
        if (Part != null && IsInstanceValid(Part)) UpdateVisual((float)delta);
    }

    /// <summary>Per-frame refresh from Part state. Subclasses call base.</summary>
    protected virtual void UpdateVisual(float deltaTime)
    {
        float max = Part.Definition?.MaxSafeTempK ?? 0f;
        ApplyThermalOverlay(max > 0f ? Part.TempK / max : 0f, Part.State);
    }

    /// <summary>Anchor node by name (ExhaustPoint, GimbalPivot, ...), or null.</summary>
    protected Node3D FindAnchor(string anchorName) => FindChild(anchorName, true, false) as Node3D;

    /// <summary>Heat glow, frozen and broken tint.</summary>
    protected virtual void ApplyThermalOverlay(float tempRatio, ComponentState state)
    {
        if (_material == null) return;
        _material.AlbedoColor = state switch
        {
            ComponentState.Broken => BrokenColor,
            ComponentState.Frozen => _baseColor.Lerp(FrozenColor, 0.6f),
            _ => _baseColor,
        };

        float glow = GlowFullRatio > GlowStartRatio
            ? Mathf.Clamp((tempRatio - GlowStartRatio) / (GlowFullRatio - GlowStartRatio), 0f, 1f)
            : 0f;
        bool emit = glow > 0f && state != ComponentState.Broken;
        _material.EmissionEnabled = emit;
        if (emit)
        {
            _material.Emission = GlowColor;
            _material.EmissionEnergyMultiplier = glow * GlowEnergy;
        }
    }

    /// <summary>One material per visual, set as override on every placeholder mesh under MeshRoot.</summary>
    private void EnsureMaterial()
    {
        if (_material != null || KeepMeshMaterials || MeshRoot == null) return;
        _material = new StandardMaterial3D { AlbedoColor = GroupColor, Roughness = 0.6f };
        foreach (MeshInstance3D mesh in FindMeshes(MeshRoot))
            mesh.MaterialOverride = _material;
    }

    private static List<MeshInstance3D> FindMeshes(Node root)
    {
        var result = new List<MeshInstance3D>();
        if (root is MeshInstance3D self) result.Add(self);
        foreach (Node child in root.FindChildren("*", nameof(MeshInstance3D), true, false))
            if (child is MeshInstance3D mesh) result.Add(mesh);
        return result;
    }
}
