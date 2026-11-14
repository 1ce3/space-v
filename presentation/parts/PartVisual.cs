using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Presentation;

/// <summary>
/// Root script of every part scene (model team contract). Reads its RocketPart each frame; never writes game state.
/// </summary>
public partial class PartVisual : Node3D
{
    /// <summary>Holds the meshes; scaled to the part footprint by Bind.</summary>
    [Export] public Node3D MeshRoot { get; set; }
    /// <summary>Color used by placeholder meshes.</summary>
    [Export] public Color GroupColor { get; set; } = Colors.White;
    /// <summary>Temperature ratio where the glow starts (TempK / MaxSafeTempK).</summary>
    [Export(PropertyHint.Range, "0,2,0.01")] public float GlowStartRatio { get; set; } = 0.6f;

    /// <summary>The part shown; null in the palette.</summary>
    public RocketPart Part { get; private set; }

    /// <summary>Attaches to a part and scales MeshRoot to its footprint.</summary>
    public virtual void Bind(RocketPart part)
    {
        Part = part;
        // TODO(A5): scale MeshRoot by Footprint, apply GroupColor.
    }

    /// <summary>Build-screen ghost: tinted valid or invalid.</summary>
    public virtual void SetGhost(bool isValid)
    {
        // TODO(A5)
    }

    /// <summary>Mirrors the visual for left/right side parts.</summary>
    public virtual void SetMirrored(bool mirrored)
    {
        // TODO(A8)
    }

    /// <summary>Called by debris spawning when the part leaves the rocket.</summary>
    public virtual void OnSevered(SeverCause cause)
    {
        // TODO(A5)
    }

    public override void _Process(double delta)
    {
        if (Part != null) UpdateVisual((float)delta);
    }

    /// <summary>Per-frame refresh from Part state. Subclasses call base.</summary>
    protected virtual void UpdateVisual(float deltaTime)
    {
        float max = Part.Definition?.MaxSafeTempK ?? 0f;
        ApplyThermalOverlay(max > 0f ? Part.TempK / max : 0f, Part.State);
    }

    /// <summary>Anchor node by name (ExhaustPoint, GimbalPivot, ...), or null.</summary>
    protected Node3D FindAnchor(string anchorName) => FindChild(anchorName, true, false) as Node3D;

    /// <summary>Heat glow and broken tint.</summary>
    protected virtual void ApplyThermalOverlay(float tempRatio, ComponentState state)
    {
        // TODO(A8)
    }
}
