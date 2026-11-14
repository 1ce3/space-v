using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Presentation;

/// <summary>
/// Engine plume at ExhaustPoint and gimbal rotation of GimbalPivot. Used by liquid engines and solid boosters.
/// Reads the EnginePart every frame: plume length follows thrust fraction, plume widens as ambient pressure drops.
/// </summary>
public partial class EngineVisual : PartVisual
{
    /// <summary>Ambient pressure used for the plume shape (Pa). Set by whoever knows it (e.g. FlightVfxDirector); sea level by default.</summary>
    public static float AmbientPressurePa { get; set; } = EnginePart.SeaLevelPressurePa;

    [ExportGroup("Plume")]
    /// <summary>Plume length at full thrust (m).</summary>
    [Export] public float PlumeLengthM { get; set; } = 6f;
    /// <summary>Plume length at minimum burn, as a fraction of PlumeLengthM.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float MinPlumeFraction { get; set; } = 0.3f;
    /// <summary>Plume radius at the nozzle (m), at sea level.</summary>
    [Export] public float PlumeRadiusM { get; set; } = 0.55f;
    /// <summary>Radius multiplier in vacuum (the plume blooms when there is no air).</summary>
    [Export] public float VacuumWidthScale { get; set; } = 2.2f;
    [Export] public Color PlumeOuterColor { get; set; } = new(1f, 0.5f, 0.12f, 0.75f);
    [Export] public Color PlumeCoreColor { get; set; } = new(1f, 0.9f, 0.6f, 0.9f);
    /// <summary>Random length flicker (fraction).</summary>
    [Export(PropertyHint.Range, "0,0.5,0.01")] public float Flicker { get; set; } = 0.08f;
    /// <summary>Light at the nozzle while burning (0 = no light).</summary>
    [Export] public float LightEnergy { get; set; } = 2f;
    [Export] public float LightRangeM { get; set; } = 12f;

    [ExportGroup("Gimbal")]
    /// <summary>Visual multiplier of the gimbal angle (real angles of a few degrees are hard to see).</summary>
    [Export] public float GimbalExaggeration { get; set; } = 2f;
    /// <summary>+1 or -1: direction of the visual rotation for a positive GimbalAngleDeg (AGENTS.md 7).</summary>
    [Export] public float GimbalSign { get; set; } = 1f;
    [Export] public float GimbalSmoothing { get; set; } = 12f;

    /// <summary>Current plume fraction (0 when off). For tests and other effects.</summary>
    public float PlumeFraction { get; private set; }
    public bool PlumeVisible => _plume != null && _plume.Visible;

    private Node3D _exhaust;
    private Node3D _gimbal;
    private Node3D _plume;
    private MeshInstance3D _outer;
    private MeshInstance3D _core;
    private OmniLight3D _light;
    private float _gimbalDeg;
    private readonly RandomNumberGenerator _rng = new();

    public override void Bind(RocketPart part)
    {
        base.Bind(part);
        _exhaust = FindAnchor("ExhaustPoint");
        _gimbal = FindAnchor("GimbalPivot");
        BuildPlume();
    }

    protected override void UpdateVisual(float deltaTime)
    {
        base.UpdateVisual(deltaTime);
        if (Part is not EnginePart engine) return;

        float fraction = 0f;
        float maxThrust = engine.EngineDefinition?.MaxThrustVac ?? 0f;
        if (engine.IsIgnited && maxThrust > 0f) fraction = engine.ComputeThrust(0f) / maxThrust;
        // A separated stage is no longer simulated: hide its plume rather than leave it burning forever.
        bool burning = engine.IsIgnited && engine.State != ComponentState.Broken && engine.IsAttached;
        UpdateExhaust(burning, fraction, AmbientPressurePa);

        float k = 1f - Mathf.Exp(-GimbalSmoothing * deltaTime);
        _gimbalDeg = Mathf.Lerp(_gimbalDeg, engine.GimbalAngleDeg, k);
        UpdateGimbal(_gimbalDeg);
    }

    /// <summary>Shows the plume for a thrust fraction (0..1) at the given ambient pressure.</summary>
    public void UpdateExhaust(bool isIgnited, float thrustFraction, float ambientPressure)
    {
        if (_plume == null) return;
        bool on = isIgnited && thrustFraction > 0.001f;
        _plume.Visible = on;
        if (_light != null) _light.Visible = on && LightEnergy > 0f;
        PlumeFraction = on ? Mathf.Clamp(thrustFraction, 0f, 1f) : 0f;
        if (!on) return;

        float pressureRatio = Mathf.Clamp(ambientPressure / EnginePart.SeaLevelPressurePa, 0f, 1f);
        float width = Mathf.Lerp(VacuumWidthScale, 1f, pressureRatio);
        float length = PlumeLengthM * Mathf.Lerp(MinPlumeFraction, 1f, PlumeFraction);
        length *= 1f + _rng.RandfRange(-Flicker, Flicker);

        // _plume carries the inverse MeshRoot scale; its child scales are in meters.
        _outer.Scale = new Vector3(PlumeRadiusM * width, length, PlumeRadiusM * width);
        _outer.Position = new Vector3(0f, -length * 0.5f, 0f);
        _core.Scale = new Vector3(PlumeRadiusM * 0.45f, length * 0.45f, PlumeRadiusM * 0.45f);
        _core.Position = new Vector3(0f, -length * 0.225f, 0f);
        if (_light != null) _light.LightEnergy = LightEnergy * (0.6f + 0.4f * PlumeFraction);
    }

    /// <summary>Rotates GimbalPivot (degrees, before exaggeration).</summary>
    public void UpdateGimbal(float gimbalAngleDeg)
    {
        if (_gimbal == null) return;
        _gimbal.RotationDegrees = new Vector3(0f, 0f, gimbalAngleDeg * GimbalExaggeration * GimbalSign);
    }

    private void BuildPlume()
    {
        if (_plume != null || _exhaust == null) return;
        _plume = new Node3D { Name = "Plume", Visible = false, Scale = GetInverseMeshScale() };
        _exhaust.AddChild(_plume);

        // Unit cone: radius 1 at the top (nozzle), 0 at the bottom, height 1; scaled per frame.
        var cone = new CylinderMesh { TopRadius = 1f, BottomRadius = 0.05f, Height = 1f, RadialSegments = 16, Rings = 1 };
        _outer = new MeshInstance3D { Name = "Outer", Mesh = cone, MaterialOverride = MakeGlowMaterial(PlumeOuterColor) };
        _core = new MeshInstance3D { Name = "Core", Mesh = cone, MaterialOverride = MakeGlowMaterial(PlumeCoreColor, additive: true) };
        _outer.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _core.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _plume.AddChild(_outer);
        _plume.AddChild(_core);

        if (LightEnergy > 0f)
        {
            _light = new OmniLight3D
            {
                Name = "PlumeLight",
                LightColor = PlumeOuterColor,
                LightEnergy = LightEnergy,
                OmniRange = LightRangeM,
                Visible = false,
                Position = new Vector3(0f, -1f, 0f),
            };
            _plume.AddChild(_light);
        }
    }
}
