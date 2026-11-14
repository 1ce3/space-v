using Godot;
using RocketGame.Control;
using RocketGame.Core;
using RocketGame.Physics;

namespace RocketGame.Presentation;

/// <summary>
/// Ground strip, launch pad and sky for the current celestial body (C_CONTROL.md 4.6).
/// Uses Body.EnvironmentScene when the model team provides one, otherwise builds a minimal one.
/// </summary>
public partial class BodyEnvironmentVisual : Node3D
{
    [Export] public FlightSettings Settings { get; set; }
    [Export] public WorldEnvironment WorldEnvironment { get; set; }
    /// <summary>Half width of the generated ground strip (m).</summary>
    [Export] public float GroundHalfWidthM { get; set; } = 50000f;

    /// <summary>Body currently shown.</summary>
    public CelestialBodyDefinition Body { get; private set; }

    private Node3D _surfaceAnchor;

    public override void _Ready()
    {
        NodeGuard.Require(this, Settings, nameof(Settings));
    }

    /// <summary>Switches to another body (Earth at segment 1, Mars at segment 3).</summary>
    public void SetBody(CelestialBodyDefinition body)
    {
        Body = body;
        // TODO(C6): instance EnvironmentScene or build ground strip + pad; set sky color.
    }

    /// <summary>Blends the sky toward space color as the air thins.</summary>
    public void UpdateSky(float altitudeMeters, float airDensity)
    {
        // TODO(C6)
    }

    /// <summary>Node at the surface origin (pad or landing site).</summary>
    public Node3D GetSurfaceAnchor() => _surfaceAnchor ?? this;
}
