using Godot;
using RocketGame.Control;
using RocketGame.Core;
using RocketGame.Physics;

namespace RocketGame.Presentation;

/// <summary>
/// Ground strip, launch pad and sky for the current celestial body (C_CONTROL.md 4.6).
/// Uses Body.EnvironmentScene when the model team provides one, otherwise builds a minimal one.
/// The ground is only visual; ground contact is computed from altitude.
/// </summary>
public partial class BodyEnvironmentVisual : Node3D
{
    [Export] public FlightSettings Settings { get; set; }
    [Export] public WorldEnvironment WorldEnvironment { get; set; }
    /// <summary>Half width of the generated ground strip (m).</summary>
    [Export] public float GroundHalfWidthM { get; set; } = 50000f;
    /// <summary>Depth of the generated ground strip (m, along Z).</summary>
    [Export] public float GroundDepthM { get; set; } = 400f;
    [Export] public Color EarthGroundColor { get; set; } = new(0.28f, 0.42f, 0.22f);
    [Export] public Color MarsGroundColor { get; set; } = new(0.62f, 0.32f, 0.18f);
    /// <summary>Launch pad size (m).</summary>
    [Export] public Vector3 PadSize { get; set; } = new(24f, 2f, 16f);
    [Export] public Color PadColor { get; set; } = new(0.45f, 0.45f, 0.48f);

    /// <summary>Body currently shown.</summary>
    public CelestialBodyDefinition Body { get; private set; }

    private Node3D _surfaceAnchor;
    private Node3D _content;
    private Color _surfaceSky;

    public override void _Ready()
    {
        NodeGuard.Require(this, Settings, nameof(Settings));
    }

    /// <summary>Switches to another body (Earth at segment 1, Mars at segment 3).</summary>
    public void SetBody(CelestialBodyDefinition body)
    {
        Body = body;
        _content?.QueueFree();
        _content = new Node3D { Name = "Content" };
        AddChild(_content);

        bool isMars = body != null && Settings != null && body.BodyId == Settings.MarsBodyId;
        if (body?.EnvironmentScene != null)
        {
            _content.AddChild(body.EnvironmentScene.Instantiate());
        }
        else
        {
            AddBox(_content, "Ground", new Vector3(GroundHalfWidthM * 2f, 10f, GroundDepthM), new Vector3(0f, -5f, 0f),
                isMars ? MarsGroundColor : EarthGroundColor);
            if (!isMars) AddBox(_content, "Pad", PadSize, new Vector3(0f, -PadSize.Y * 0.5f + 0.01f, 0f), PadColor);
        }
        _surfaceAnchor = _content;

        _surfaceSky = Settings == null ? Colors.SkyBlue : isMars ? Settings.MarsSkyColor : Settings.EarthSkyColor;
        ApplySky(_surfaceSky);
    }

    /// <summary>Blends the sky toward space color as the air thins.</summary>
    public void UpdateSky(float altitudeMeters, float airDensity)
    {
        if (Body == null || Settings == null) return;
        float ratio = Body.SeaLevelDensity > 0f ? Mathf.Clamp(airDensity / Body.SeaLevelDensity, 0f, 1f) : 0f;
        ApplySky(Settings.SpaceSkyColor.Lerp(_surfaceSky, Mathf.Sqrt(ratio)));
    }

    /// <summary>Node at the surface origin (pad or landing site).</summary>
    public Node3D GetSurfaceAnchor() => _surfaceAnchor ?? this;

    private void ApplySky(Color color)
    {
        Environment env = WorldEnvironment?.Environment;
        if (env == null) return;
        env.BackgroundMode = Environment.BGMode.Color;
        env.BackgroundColor = color;
    }

    private static void AddBox(Node3D parent, string name, Vector3 size, Vector3 position, Color color)
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = size },
            Position = position,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f },
        };
        parent.AddChild(mesh);
    }
}
