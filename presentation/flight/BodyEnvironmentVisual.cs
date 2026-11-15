// AI-assisted: parts of this file were written with Claude (Anthropic); reviewed by The Neptunians.
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
    [Export] public bool EnableAtmosphereGlow { get; set; } = true;
    [Export] public bool EnableStars { get; set; } = true;
    /// <summary>Local map (EnvironmentScene or the boxes) fades out by camera distance between these (m).</summary>
    [Export] public float LocalFadeStartM { get; set; } = 8000f;
    [Export] public float LocalFadeEndM { get; set; } = 20000f;
    /// <summary>Fog density at the surface (exponential fog); scales with air density.</summary>
    [Export(PropertyHint.Range, "0,0.01,0.00001")] public float SurfaceFogDensity { get; set; } = 0.00025f;
    /// <summary>Planet sphere sits this far below y = 0 so the local map always covers it near the pad.</summary>
    [Export] public float PlanetSinkM { get; set; } = 30f;
    /// <summary>Visual multiplier on Body.Radius for the rendered planet sphere so 2-15 km altitude has realistic wide curvature instead of a tight dome.</summary>
    [Export(PropertyHint.Range, "1,15,0.5")] public float VisualRadiusMultiplier { get; set; } = 10f;
    [Export] public Texture2D EarthTexture { get; set; }
    [Export] public Texture2D MarsTexture { get; set; }
    [Export] public Color EarthPlanetColor { get; set; } = new(0.18f, 0.36f, 0.62f);
    [Export] public Color MarsPlanetColor { get; set; } = new(0.66f, 0.36f, 0.22f);
    [Export] public Color EarthAtmosphereColor { get; set; } = new(0.76f, 0.88f, 0.98f, 1.0f);
    [Export] public Color MarsAtmosphereColor { get; set; } = new(0.95f, 0.65f, 0.45f, 0.85f);
    /// <summary>Atmosphere shell thickness as a share of the radius.</summary>
    [Export(PropertyHint.Range, "0,0.1,0.001")] public float AtmosphereShellFraction { get; set; } = 0.012f;
    [Export] public int StarCount { get; set; } = 1500;
    /// <summary>Stars appear between these altitudes (m).</summary>
    [Export] public float StarsStartM { get; set; } = 15000f;
    [Export] public float StarsFullM { get; set; } = 45000f;

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
    /// <summary>Root of Body.EnvironmentScene (authored landing site / launch site), or null.</summary>
    private Node3D _envRoot;

    /// <summary>
    /// The physics ground is a sphere (centre at (0, -Radius)); an authored map is flat. On Mars the descent travels
    /// ~100 km sideways, where the real surface is ~15 km below a flat map, so the rocket landed far under it.
    /// When true, the map is laid on the tangent plane under the rocket every frame and shifted by the distance
    /// travelled along the surface, so the ground matches the physics ground and does not slide with the rocket.
    /// </summary>
    [ExportGroup("Curvature")]
    [Export] public bool BendMapToPlanetOnMars { get; set; } = true;
    [Export] public bool BendMapToPlanetOnEarth { get; set; }
    /// <summary>Rolled so screen-up is the local vertical while the map is bent (null = no roll).</summary>
    [Export] public FlightCameraRig CameraRig { get; set; }

    private MeshInstance3D _planet;
    private MeshInstance3D _atmosphereShell;
    private Node3D _content;
    private Color _surfaceSky;
    private Environment _defaultEnv;
    private bool _useSceneEnv;
    private float _sceneFogDensity;
    private float _sceneVolFogDensity;
    private float _sceneFogBegin;
    private float _sceneFogEnd;
    private float _sceneAmbient = 1f;
    private ProceduralSkyMaterial _sceneSkyMat;
    private ShaderMaterial _sceneShaderSkyMat;
    private ShaderMaterial _planetMat;
    private Color _sceneFogColor;
    private Color _sceneSkyTop;
    private Color _sceneSkyHorizon;
    private Color _sceneGroundBottom;
    private Color _sceneGroundHorizon;
    private float _sceneSkyCurve = 0.15f;
    private float _sceneGroundCurve = 0.02f;
    private DirectionalLight3D _sun;
    private Color _sunBaseColor = Colors.White;
    /// <summary>Ambient light energy in space (scene-authored environments only).</summary>
    [ExportGroup("Altitude transition")]
    [Export(PropertyHint.Range, "0,1,0.01")] public float SpaceAmbientEnergy { get; set; } = 0.08f;
    /// <summary>Ground haze (scene-authored fog) is full below this altitude (m)...</summary>
    [Export] public float GroundFogFadeStartM { get; set; } = 200f;
    /// <summary>...and gone above this altitude (m).</summary>
    [Export] public float GroundFogFadeEndM { get; set; } = 2500f;
    /// <summary>Altitude at which the sky and horizon finish transitioning into space black (m).</summary>
    [Export] public float SpaceTransitionAltitudeM { get; set; } = 50000f;

    /// <summary>Uses the WorldEnvironment authored inside the environment scene (fog, sky) in place of the flight scene's one.</summary>
    private void AdoptSceneEnvironment(Node root)
    {
        WorldEnvironment sceneWe = FindWorldEnvironment(root);
        if (sceneWe?.Environment == null || WorldEnvironment == null) return;
        _defaultEnv ??= WorldEnvironment.Environment;
        Environment clonedEnv = (Environment)sceneWe.Environment.Duplicate(true);
        WorldEnvironment.Environment = clonedEnv;
        sceneWe.GetParent().RemoveChild(sceneWe); // only one WorldEnvironment may be active
        sceneWe.QueueFree();
        _useSceneEnv = true;
        _sceneFogDensity = clonedEnv.FogDensity;
        _sceneVolFogDensity = clonedEnv.VolumetricFogDensity;
        _sceneFogBegin = clonedEnv.FogDepthBegin;
        _sceneFogEnd = clonedEnv.FogDepthEnd;
        _sceneFogColor = clonedEnv.FogLightColor;
        _sceneAmbient = clonedEnv.AmbientLightEnergy;
        if (clonedEnv.Sky?.SkyMaterial is ShaderMaterial sm)
        {
            _sceneShaderSkyMat = sm;
            _sceneSkyMat = null;
        }
        else if (clonedEnv.Sky?.SkyMaterial is ProceduralSkyMaterial psm)
        {
            _sceneSkyMat = psm;
            _sceneShaderSkyMat = null;
            _sceneSkyTop = psm.SkyTopColor;
            _sceneSkyHorizon = psm.SkyHorizonColor;
            _sceneGroundBottom = psm.GroundBottomColor;
            _sceneGroundHorizon = psm.GroundHorizonColor;
            _sceneSkyCurve = psm.SkyCurve;
            _sceneGroundCurve = psm.GroundCurve;
        }
        else
        {
            _sceneSkyMat = null;
            _sceneShaderSkyMat = null;
        }
    }

    private static DirectionalLight3D FindSun(Node node)
    {
        if (node is DirectionalLight3D sun) return sun;
        foreach (Node child in node.GetChildren())
        {
            DirectionalLight3D found = FindSun(child);
            if (found != null) return found;
        }
        return null;
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
        _sceneSkyMat = null;
        _sceneShaderSkyMat = null;
        _planetMat = null;
        _sun = null;
        _content = new Node3D { Name = "Content" };
        _envRoot = null;
        _planet = null;
        _atmosphereShell = null;
        if (CameraRig != null) CameraRig.RollRad = 0f;
        if (WorldEnvironment?.Environment != null) WorldEnvironment.Environment.SkyRotation = Vector3.Zero;
        AddChild(_content);

        bool isMars = body != null && Settings != null && body.BodyId == Settings.MarsBodyId;
        if (body?.EnvironmentScene != null)
        {
            Node envRoot = body.EnvironmentScene.Instantiate();
            _content.AddChild(envRoot);
            _envRoot = envRoot as Node3D;
            BindFollowTarget(envRoot);
            _sun = FindSun(envRoot);
            if (_sun != null) _sunBaseColor = _sun.LightColor;
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

    public override void _Process(double delta) => BendMapToPlanet();

    /// <summary>Lays the authored map on the planet surface under FollowTarget (see BendMapToPlanetOnMars).</summary>
    private void BendMapToPlanet()
    {
        if (FollowTarget == null || Body == null || Body.Radius <= 0f) return;
        if (!(_isMars ? BendMapToPlanetOnMars : BendMapToPlanetOnEarth)) return;
        float r = Body.Radius;
        Vector3 p = FollowTarget.GlobalPosition;
        // Planet centre is (0, -r): angle of the rocket from the vertical through the origin.
        var n = new Vector2(p.X, p.Y + r);
        if (n.LengthSquared() < 1f) return;
        n = n.Normalized();
        float theta = Mathf.Atan2(n.X, n.Y);
        var surface = new Vector3(n.X * r, n.Y * r - r, 0f);
        // Rotate the map's up (0,1) to the surface normal, then shift it back by the arc length so a map point
        // keeps (to second order) the same world position while the rocket moves.
        var tangentFrame = new Transform3D(new Basis(Vector3.Back, -theta), surface);
        if (_envRoot != null && IsInstanceValid(_envRoot))
            _envRoot.GlobalTransform = tangentFrame * new Transform3D(Basis.Identity, new Vector3(-r * theta, 0f, 0f));

        // The backdrop sphere is drawn bigger than the physics planet: keep it tangent to the ground under the rocket
        // so its horizon agrees with the bent map and the rolled camera.
        var normal = new Vector3(n.X, n.Y, 0f);
        float visualRadius = r * Mathf.Max(1f, VisualRadiusMultiplier);
        Vector3 visualCenter = surface - normal * (visualRadius + PlanetSinkM);
        if (_planet != null && IsInstanceValid(_planet)) _planet.GlobalPosition = visualCenter;
        if (_atmosphereShell != null && IsInstanceValid(_atmosphereShell)) _atmosphereShell.GlobalPosition = visualCenter;
        if (CameraRig != null) CameraRig.RollRad = -theta;
        // The sky draws its horizon against world up: turn it with the local vertical too.
        if (WorldEnvironment?.Environment != null) WorldEnvironment.Environment.SkyRotation = new Vector3(0f, 0f, -theta);
    }

    /// <summary>Gives the rocket to any node in the environment scene that has set_target_player (streaming terrain).</summary>
    private void BindFollowTarget(Node node)
    {
        if (FollowTarget == null) return;
        if (node.HasMethod("set_target_player")) node.Call("set_target_player", FollowTarget);
        foreach (Node child in node.GetChildren()) BindFollowTarget(child);
    }

    /// <summary>Blends the sky toward space color as the air thins and altitude increases.</summary>
    public void UpdateSky(float altitudeMeters, float airDensity)
    {
        if (Body == null || Settings == null) return;
        float ratio = Body.SeaLevelDensity > 0f ? Mathf.Clamp(airDensity / Body.SeaLevelDensity, 0f, 1f) : 0f;
        float alt = Mathf.Max(altitudeMeters, 0f);
        float altFactor = 1f - Mathf.Clamp(alt / Mathf.Max(1f, SpaceTransitionAltitudeM), 0f, 1f);
        float skyFactor = Mathf.Min(Mathf.Pow(ratio, 0.35f), altFactor);
        float visualRadius = Body.Radius * Mathf.Max(1f, VisualRadiusMultiplier);
        if (_sceneShaderSkyMat != null)
        {
            _sceneShaderSkyMat.SetShaderParameter("rocket_altitude", alt);
            _sceneShaderSkyMat.SetShaderParameter("visual_planet_radius", visualRadius);
            _sceneShaderSkyMat.SetShaderParameter("space_altitude", SpaceTransitionAltitudeM);
        }
        if (_planetMat != null)
        {
            float limbStrength = 1f - Mathf.SmoothStep(32000f, SpaceTransitionAltitudeM * 1.3f, alt);
            Color lowHaze = _isMars ? new Color(0.92f, 0.68f, 0.50f) : new Color(0.76f, 0.88f, 0.98f);
            Color highLimb = _isMars ? new Color(0.75f, 0.42f, 0.22f) : new Color(0.32f, 0.68f, 1.0f);
            Color limbCol = lowHaze.Lerp(highLimb, Mathf.SmoothStep(3000f, 25000f, alt));
            _planetMat.SetShaderParameter("haze_color", limbCol);
            _planetMat.SetShaderParameter("haze_strength", limbStrength);
        }
        ApplySkyRatio(skyFactor);

        Environment env = WorldEnvironment?.Environment;
        if (env != null && _useSceneEnv)
        {
            // Fog/sky authored in the environment scene: thin the fog out with the air and altitude.
            if (env.VolumetricFogEnabled) env.VolumetricFogDensity = _sceneVolFogDensity * skyFactor;
            if (env.FogEnabled)
            {
                // Ground haze keeps its authored begin/end near the pad (to hide terrain chunk edges),
                // then transitions into wide aerial-perspective haze matched to the horizon band.
                float groundFade = 1f - Mathf.SmoothStep(GroundFogFadeStartM, GroundFogFadeEndM, alt);
                float highAerial = Mathf.Clamp(1f - alt / 28000f, 0f, 1f) * 0.65f;
                env.FogDensity = _sceneFogDensity * Mathf.Max(groundFade, highAerial * skyFactor);
                env.FogDepthBegin = Mathf.Lerp(_sceneFogBegin, _sceneFogBegin + alt * 0.35f, 1f - groundFade);
                env.FogDepthEnd = Mathf.Lerp(_sceneFogEnd, Mathf.Max(_sceneFogEnd, alt * 3.2f + 1800f), 1f - groundFade);
                Color lowHaze = _isMars ? _sceneFogColor : new Color(0.76f, 0.88f, 0.98f);
                Color highLimb = _isMars ? _sceneFogColor : new Color(0.32f, 0.68f, 1.0f);
                env.FogLightColor = lowHaze.Lerp(highLimb, Mathf.SmoothStep(3000f, 25000f, alt));
            }
            // Ambient light fades with the air; sunlight loses its warm tint and turns harsh white in space.
            env.AmbientLightEnergy = Mathf.Lerp(SpaceAmbientEnergy, _sceneAmbient, skyFactor);
            if (_sun != null) _sun.LightColor = Colors.White.Lerp(_sunBaseColor, skyFactor);
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
            // Keep the entire visual planet horizon inside the camera frustum so the sphere never clips into a chord.
            float h = Mathf.Max(altitudeMeters, 0f) + 100f;
            float horizon = Mathf.Sqrt(2f * visualRadius * h + h * h);
            float camDist = Mathf.Max(5f, cam.Position.Length());
            cam.Near = Mathf.Clamp(camDist * 0.1f, 1.0f, 50f);
            cam.Far = Mathf.Clamp(horizon * 1.35f + 25000f, 50000f, cam.Near * 80000f);
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

    /// <summary>Planet sphere (curved horizon) with built-in Rayleigh/Mie rim scattering so the horizon edge blends seamlessly into the sky.</summary>
    private void AddPlanet(CelestialBodyDefinition body, bool isMars)
    {
        if (!EnablePlanet || body == null || body.Radius <= 0f) return;
        float r = body.Radius * Mathf.Max(1f, VisualRadiusMultiplier);
        var center = new Vector3(0f, -r - PlanetSinkM, 0f);
        Color surfaceCol = isMars ? MarsPlanetColor : EarthPlanetColor;
        Color hazeCol = isMars ? MarsAtmosphereColor : EarthAtmosphereColor;
        var planetShader = new Shader
        {
            Code = @"shader_type spatial;
render_mode specular_disabled, fog_disabled;
uniform vec4 surface_color : source_color = vec4(0.18, 0.36, 0.62, 1.0);
uniform vec4 haze_color : source_color = vec4(0.76, 0.88, 0.98, 1.0);
uniform float haze_strength = 1.0;
void fragment() {
    float ndotv = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
    // Grazing angles near the limb smoothly turn into bright atmospheric haze
    float rim = pow(1.0 - ndotv, 2.2);
    float edge_fade = smoothstep(0.0, 0.18, ndotv);
    ALBEDO = mix(surface_color.rgb, haze_color.rgb, clamp(rim * 1.15, 0.0, 1.0) * haze_strength);
    EMISSION = haze_color.rgb * (rim * 0.55 * haze_strength);
    ROUGHNESS = 1.0;
}",
        };
        _planetMat = new ShaderMaterial { Shader = planetShader };
        _planetMat.SetShaderParameter("surface_color", surfaceCol);
        _planetMat.SetShaderParameter("haze_color", hazeCol);
        _planetMat.SetShaderParameter("haze_strength", 1.0f);
        var planet = new MeshInstance3D
        {
            Name = "Planet",
            Mesh = new SphereMesh { Radius = r, Height = 2f * r, RadialSegments = 256, Rings = 128 },
            Position = center,
            MaterialOverride = _planetMat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _content.AddChild(planet);
        _planet = planet;

        if (!EnableAtmosphereGlow || body.SeaLevelDensity <= 0f || AtmosphereShellFraction <= 0f) return;
        float ra = r * (1f + AtmosphereShellFraction * 0.35f);
        var shader = new Shader
        {
            Code = @"shader_type spatial;
render_mode unshaded, cull_front, blend_add, depth_draw_never, fog_disabled;
uniform vec4 glow : source_color;
void fragment() {
    float rim = pow(1.0 - abs(dot(NORMAL, VIEW)), 3.5);
    ALBEDO = glow.rgb;
    ALPHA = glow.a * rim * 0.65;
}",
        };
        var mat = new ShaderMaterial { Shader = shader };
        mat.SetShaderParameter("glow", hazeCol);
        _atmosphereShell = new MeshInstance3D
        {
            Name = "Atmosphere",
            Mesh = new SphereMesh { Radius = ra, Height = 2f * ra, RadialSegments = 128, Rings = 64 },
            Position = center,
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _content.AddChild(_atmosphereShell);
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
        if (_useSceneEnv && _sceneShaderSkyMat != null)
        {
            env.BackgroundEnergyMultiplier = 1.0f;
            return;
        }
        if (_useSceneEnv && _sceneSkyMat != null)
        {
            // Upper sky darkens first (k^1.6), while the horizon keeps a thin atmospheric band longer (sqrt(k)).
            float topT = Mathf.Pow(k, 1.6f);
            float horT = Mathf.Sqrt(k);
            Color deepSpace = new(0.002f, 0.003f, 0.008f);
            Color limbColor = _isMars ? new Color(0.55f, 0.28f, 0.14f) : new Color(0.12f, 0.38f, 0.85f);
            Color midHorizon = limbColor.Lerp(_sceneSkyHorizon, k);
            _sceneSkyMat.SkyTopColor = deepSpace.Lerp(_sceneSkyTop, topT);
            _sceneSkyMat.SkyHorizonColor = deepSpace.Lerp(midHorizon, horT);
            _sceneSkyMat.GroundHorizonColor = deepSpace.Lerp(midHorizon, horT);
            _sceneSkyMat.GroundBottomColor = deepSpace.Lerp(_sceneGroundBottom, topT);
            // Tighten the horizon band as we climb so it looks like a thin atmospheric limb from high altitude.
            _sceneSkyMat.SkyCurve = Mathf.Lerp(0.025f, _sceneSkyCurve, k);
            _sceneSkyMat.GroundCurve = Mathf.Lerp(0.01f, _sceneGroundCurve, k);
            env.BackgroundEnergyMultiplier = Mathf.Lerp(0.25f, 1f, horT);
            return;
        }
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
