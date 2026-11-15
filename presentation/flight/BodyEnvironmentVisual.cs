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
    /// <summary>Node the streaming terrain follows (the rocket's VehicleRoot).</summary>
    [Export] public Node3D FollowTarget { get; set; }
    /// <summary>Half width of the generated ground strip (m).</summary>
    [Export] public float GroundHalfWidthM { get; set; } = 50000f;
    /// <summary>Depth of the generated ground strip (m, along Z).</summary>
    [Export] public float GroundDepthM { get; set; } = 100000f;
    [Export] public Color EarthGroundColor { get; set; } = new(0.28f, 0.42f, 0.22f);
    [Export] public Color MarsGroundColor { get; set; } = new(0.62f, 0.32f, 0.18f);
    /// <summary>Launch pad size (m).</summary>
    [Export] public Vector3 PadSize { get; set; } = new(24f, 2f, 16f);
    [Export] public Color PadColor { get; set; } = new(0.45f, 0.45f, 0.48f);

    /// <summary>Body currently shown.</summary>
    [ExportGroup("Altitude transition")]
    [Export] public bool EnableFog { get; set; } = true;
    [Export] public bool EnablePlanet { get; set; } = true;
    [Export] public bool EnableAtmosphereGlow { get; set; } = false;
    [Export] public bool EnableStars { get; set; } = true;
    /// <summary>Local map (EnvironmentScene or the boxes) fades out by camera distance between these (m).</summary>
    [Export] public float LocalFadeStartM { get; set; } = 8000f;
    [Export] public float LocalFadeEndM { get; set; } = 20000f;
    /// <summary>Fog density at the surface (exponential fog); scales with air density.</summary>
    [Export(PropertyHint.Range, "0,0.01,0.00001")] public float SurfaceFogDensity { get; set; } = 0.00025f;
    /// <summary>Planet sphere sits this far below y = 0 so the local map always covers it near the pad.</summary>
    [Export] public float PlanetSinkM { get; set; } = 30f;
    [Export] public Texture2D EarthTexture { get; set; }
    [Export] public Texture2D MarsTexture { get; set; }
    [Export] public Color EarthPlanetColor { get; set; } = new(0.18f, 0.32f, 0.55f);
    [Export] public Color MarsPlanetColor { get; set; } = new(0.66f, 0.36f, 0.22f);
    [Export] public Color EarthAtmosphereColor { get; set; } = new(0.45f, 0.7f, 1f, 0.55f);
    [Export] public Color MarsAtmosphereColor { get; set; } = new(0.95f, 0.65f, 0.45f, 0.35f);
    /// <summary>Atmosphere shell thickness as a share of the radius.</summary>
    [Export(PropertyHint.Range, "0,0.1,0.001")] public float AtmosphereShellFraction { get; set; } = 0.012f;
    [Export] public int StarCount { get; set; } = 1500;
    /// <summary>Stars appear between these altitudes (m).</summary>
    [Export] public float StarsStartM { get; set; } = 25000f;
    [Export] public float StarsFullM { get; set; } = 60000f;

    [ExportGroup("Sky")]
    /// <summary>True: keep the WorldEnvironment sky as set up in the scene (Godot default procedural sky if none) and only dim it with altitude.</summary>
    [Export] public bool UseSceneSky { get; set; } = true;
    /// <summary>Depth fog: clear up to Begin, fully opaque at End (m). Hides the map edge; must be less than the map half-width.</summary>
    [Export] public float FogDepthBeginM { get; set; } = 3000f;
    [Export] public float FogDepthEndM { get; set; } = 15000f;
    [Export] public Color EarthFogColor { get; set; } = new(0.6463f, 0.6558f, 0.6708f);
    [Export] public Color MarsFogColor { get; set; } = new(0.8f, 0.6f, 0.45f);
    [Export] public Color EarthSkyTop { get; set; } = new(0.2f, 0.42f, 0.8f);
    [Export] public Color EarthSkyHorizon { get; set; } = new(0.65f, 0.78f, 0.92f);
    [Export] public Color MarsSkyTop { get; set; } = new(0.48f, 0.33f, 0.25f);
    [Export] public Color MarsSkyHorizon { get; set; } = new(0.86f, 0.66f, 0.5f);
    [Export] public Color SpaceTop { get; set; } = new(0.005f, 0.006f, 0.012f);
    [Export] public Color SpaceHorizon { get; set; } = new(0.02f, 0.025f, 0.05f);

    public CelestialBodyDefinition Body { get; private set; }
    private ProceduralSkyMaterial _skyMat;
    private bool _isMars;
    private MultiMeshInstance3D _stars;
    private StandardMaterial3D _starMaterial;

    private Node3D _surfaceAnchor;
    private Node3D _content;
    private Color _surfaceSky;
    private Environment _defaultEnv;
    private bool _useSceneEnv;
    private float _sceneFogDensity;
    private float _sceneVolFogDensity;

    /// <summary>Uses the WorldEnvironment authored inside the environment scene (fog, sky) in place of the flight scene's one.</summary>
    private void AdoptSceneEnvironment(Node root)
    {
        WorldEnvironment sceneWe = FindWorldEnvironment(root);
        if (sceneWe?.Environment == null || WorldEnvironment == null) return;
        _defaultEnv ??= WorldEnvironment.Environment;
        WorldEnvironment.Environment = sceneWe.Environment;
        sceneWe.GetParent().RemoveChild(sceneWe); // only one WorldEnvironment may be active
        sceneWe.QueueFree();
        _useSceneEnv = true;
        _sceneFogDensity = WorldEnvironment.Environment.FogDensity;
        _sceneVolFogDensity = WorldEnvironment.Environment.VolumetricFogDensity;
    }

    private static WorldEnvironment FindWorldEnvironment(Node node)
    {
        if (node is WorldEnvironment we) return we;
        foreach (Node child in node.GetChildren())
        {
            WorldEnvironment found = FindWorldEnvironment(child);
            if (found != null) return found;
        }
        return null;
    }

    public override void _Ready()
    {
        NodeGuard.Require(this, Settings, nameof(Settings));
    }

    /// <summary>Switches to another body (Earth at segment 1, Mars at segment 3).</summary>
    public void SetBody(CelestialBodyDefinition body)
    {
        Body = body;
        _content?.QueueFree();
        if (_useSceneEnv && _defaultEnv != null && WorldEnvironment != null) WorldEnvironment.Environment = _defaultEnv;
        _useSceneEnv = false;
        _content = new Node3D { Name = "Content" };
        AddChild(_content);

        bool isMars = body != null && Settings != null && body.BodyId == Settings.MarsBodyId;
        if (body?.EnvironmentScene != null)
        {
            Node envRoot = body.EnvironmentScene.Instantiate();
            _content.AddChild(envRoot);
            BindFollowTarget(envRoot);
            AdoptSceneEnvironment(envRoot);
        }
        else
        {
            AddBox(_content, "Ground", new Vector3(GroundHalfWidthM * 2f, 10f, GroundDepthM), new Vector3(0f, -5f, 0f),
                isMars ? MarsGroundColor : EarthGroundColor);
            if (!isMars) AddBox(_content, "Pad", PadSize, new Vector3(0f, -PadSize.Y * 0.5f + 0.01f, 0f), PadColor);
        }
        _surfaceAnchor = _content;
        ApplyLocalFade(_content);
        AddPlanet(body, isMars);

        _isMars = isMars;
        _surfaceSky = isMars ? MarsSkyHorizon : EarthSkyHorizon;
        ApplySkyRatio(1f);
    }

    /// <summary>Gives the rocket to any node in the environment scene that has set_target_player (streaming terrain).</summary>
    private void BindFollowTarget(Node node)
    {
        if (FollowTarget == null) return;
        if (node.HasMethod("set_target_player")) node.Call("set_target_player", FollowTarget);
        foreach (Node child in node.GetChildren()) BindFollowTarget(child);
    }

    /// <summary>Blends the sky toward space color as the air thins.</summary>
    public void UpdateSky(float altitudeMeters, float airDensity)
    {
        if (Body == null || Settings == null) return;
        float ratio = Body.SeaLevelDensity > 0f ? Mathf.Clamp(airDensity / Body.SeaLevelDensity, 0f, 1f) : 0f;
        ApplySkyRatio(Mathf.Sqrt(ratio));

        Environment env = WorldEnvironment?.Environment;
        if (env != null && _useSceneEnv)
        {
            // Fog/sky authored in the environment scene: only thin the fog out with the air.
            if (env.VolumetricFogEnabled) env.VolumetricFogDensity = _sceneVolFogDensity * Mathf.Sqrt(ratio);
            if (env.FogEnabled) env.FogDensity = _sceneFogDensity * Mathf.Sqrt(ratio);
        }
        else if (env != null)
        {
            env.FogEnabled = EnableFog && ratio > 0.001f;
            env.FogMode = Environment.FogModeEnum.Depth;
            env.FogDepthBegin = FogDepthBeginM;
            env.FogDepthEnd = FogDepthEndM;
            env.FogDepthCurve = 1f;
            env.FogLightColor = _isMars ? MarsFogColor : EarthFogColor;
            env.FogDensity = Mathf.Clamp(Mathf.Sqrt(ratio), 0f, 1f);
            env.FogSkyAffect = 0f;
        }
        Camera3D cam = GetViewport()?.GetCamera3D();
        if (cam != null && Body != null && EnablePlanet)
        {
            // Only as far as the horizon (plus margin): keeps depth precision for the rocket and the local map.
            float h = Mathf.Max(altitudeMeters, 0f) + 100f;
            float horizon = Mathf.Sqrt(2f * Body.Radius * h + h * h);
            cam.Far = Mathf.Clamp(horizon * 1.3f + 20000f, 20000f, Body.Radius * 3f);
            // Far/Near must stay below ~1e5 or Godot's frustum math fails (create_frustum_points errors, blank screen).
            // Near must stay well in front of the rocket (camera is ~30-400 m away), so cap it by the camera distance
            // and shrink Far instead: Far/Near must stay under ~5e4 or Godot's frustum math fails.
            float camDist = Mathf.Max(5f, cam.Position.Length());
            cam.Near = Mathf.Clamp(cam.Far / 50000f, 0.5f, camDist * 0.2f);
            cam.Far = Mathf.Min(cam.Far, cam.Near * 50000f);
        }
        UpdateStars(altitudeMeters, cam);
    }

    // ------------------------------------------------------------------ altitude transition

    /// <summary>Every mesh of the local map dithers out between LocalFadeStartM and LocalFadeEndM of camera distance.</summary>
    private void ApplyLocalFade(Node node)
    {
        if (node is GeometryInstance3D g)
        {
            g.VisibilityRangeEnd = LocalFadeEndM;
            g.VisibilityRangeEndMargin = Mathf.Max(1f, LocalFadeEndM - LocalFadeStartM);
            g.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
        }
        foreach (Node child in node.GetChildren()) ApplyLocalFade(child);
    }

    /// <summary>Planet sphere (curved horizon) and a thin glowing atmosphere shell, centred under the pad.</summary>
    private void AddPlanet(CelestialBodyDefinition body, bool isMars)
    {
        if (!EnablePlanet || body == null || body.Radius <= 0f) return;
        float r = body.Radius;
        var center = new Vector3(0f, -r - PlanetSinkM, 0f);
        Texture2D tex = isMars ? MarsTexture : EarthTexture;
        var planet = new MeshInstance3D
        {
            Name = "Planet",
            Mesh = new SphereMesh { Radius = r, Height = 2f * r, RadialSegments = 256, Rings = 128 },
            Position = center,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = tex != null ? Colors.White : (isMars ? MarsPlanetColor : EarthPlanetColor),
                AlbedoTexture = tex,
                Roughness = 1f,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _content.AddChild(planet);

        if (!EnableAtmosphereGlow || body.SeaLevelDensity <= 0f || AtmosphereShellFraction <= 0f) return;
        float ra = r * (1f + AtmosphereShellFraction);
        var shader = new Shader
        {
            Code = @"shader_type spatial;
render_mode unshaded, cull_front, blend_add, depth_draw_never;
uniform vec4 glow : source_color;
void fragment() {
    float rim = pow(1.0 - abs(dot(NORMAL, VIEW)), 3.0);
    ALBEDO = glow.rgb;
    ALPHA = glow.a * rim;
}",
        };
        var mat = new ShaderMaterial { Shader = shader };
        mat.SetShaderParameter("glow", isMars ? MarsAtmosphereColor : EarthAtmosphereColor);
        _content.AddChild(new MeshInstance3D
        {
            Name = "Atmosphere",
            Mesh = new SphereMesh { Radius = ra, Height = 2f * ra, RadialSegments = 128, Rings = 64 },
            Position = center,
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>Star points on a big shell that follows the camera; fade in with altitude.</summary>
    private void UpdateStars(float altitude, Camera3D cam)
    {
        if (!EnableStars || StarCount <= 0 || cam == null) return;
        if (_stars == null)
        {
            _starMaterial = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                AlbedoColor = new Color(1f, 1f, 1f, 0f),
            };
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new QuadMesh { Size = Vector2.One } };
            mm.InstanceCount = StarCount;
            var rng = new RandomNumberGenerator { Seed = 42 };
            float shell = 15000f; // inside the smallest Far
            for (int i = 0; i < StarCount; i++)
            {
                Vector3 dir = new Vector3(rng.Randfn(), rng.Randfn(), rng.Randfn()).Normalized();
                float size = shell * rng.RandfRange(0.0008f, 0.0022f);
                mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * size), dir * shell));
            }
            _stars = new MultiMeshInstance3D { Name = "Stars", Multimesh = mm, MaterialOverride = _starMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, TopLevel = true };
            AddChild(_stars);
        }
        float a = Mathf.Clamp((altitude - StarsStartM) / Mathf.Max(1f, StarsFullM - StarsStartM), 0f, 1f);
        if (Body != null && Settings != null && Body.BodyId == Settings.MarsBodyId) a *= 0.8f;
        _starMaterial.AlbedoColor = new Color(1f, 1f, 1f, a);
        _stars.Visible = a > 0.01f;
        _stars.GlobalPosition = cam.GlobalPosition;
        _stars.Scale = Vector3.One * (cam.Far * 0.95f / 15000f); // behind the planet, inside Far
    }

    /// <summary>Node at the surface origin (pad or landing site).</summary>
    public Node3D GetSurfaceAnchor() => _surfaceAnchor ?? this;

    /// <summary>Procedural gradient sky (like Godot's default) that fades to space as the air thins (k = 1 surface, 0 space).</summary>
    private void ApplySkyRatio(float k)
    {
        Environment env = WorldEnvironment?.Environment;
        if (env == null) return;
        if (UseSceneSky)
        {
            if (env.Sky == null)
            {
                env.Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial() }; // Godot's default look
                env.BackgroundMode = Environment.BGMode.Sky;
            }
            env.BackgroundEnergyMultiplier = Mathf.Lerp(0.03f, 1f, k);
            return;
        }
        if (_skyMat == null)
        {
            _skyMat = new ProceduralSkyMaterial { SunAngleMax = 30f, SunCurve = 0.15f };
            env.Sky = new Sky { SkyMaterial = _skyMat };
        }
        env.BackgroundMode = Environment.BGMode.Sky;
        Color top = _isMars ? MarsSkyTop : EarthSkyTop;
        Color hor = _isMars ? MarsSkyHorizon : EarthSkyHorizon;
        _skyMat.SkyTopColor = SpaceTop.Lerp(top, k);
        _skyMat.SkyHorizonColor = SpaceHorizon.Lerp(hor, k);
        Color ground = _isMars ? MarsGroundColor : EarthGroundColor;
        _skyMat.GroundBottomColor = SpaceTop.Lerp(ground.Darkened(0.5f), k);
        _skyMat.GroundHorizonColor = SpaceHorizon.Lerp(hor, k);
        _skyMat.SkyEnergyMultiplier = Mathf.Lerp(0.6f, 1f, k);
    }

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
