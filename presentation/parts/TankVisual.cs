using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Presentation;

/// <summary>Tank; vents a white plume from its side while leaking.</summary>
public partial class TankVisual : PartVisual
{
    /// <summary>Leak rate (kg/s) that gives the full vent effect.</summary>
    [Export] public float FullVentLeakKgPerSec { get; set; } = 20f;
    [Export] public Color VentColor { get; set; } = new(0.9f, 0.95f, 1f, 0.6f);
    /// <summary>Particles per second at full vent.</summary>
    [Export] public int VentAmount { get; set; } = 40;
    [Export] public float VentSpeedMps { get; set; } = 6f;

    public bool IsVenting => _vent != null && _vent.Emitting;

    private CpuParticles3D _vent;

    protected override void UpdateVisual(float deltaTime)
    {
        base.UpdateVisual(deltaTime);
        if (Part is FuelTankPart tank) UpdateLeak(tank.CurrentFuel > 0f ? tank.LeakRateKgPerSec : 0f);
    }

    /// <summary>Starts, scales or stops the vent for a leak rate (kg/s).</summary>
    public void UpdateLeak(float leakRateKgPerSec)
    {
        bool leaking = leakRateKgPerSec > 0f;
        if (!leaking)
        {
            if (_vent != null) _vent.Emitting = false;
            return;
        }
        EnsureVent();
        float strength = FullVentLeakKgPerSec > 0f ? Mathf.Clamp(leakRateKgPerSec / FullVentLeakKgPerSec, 0.15f, 1f) : 1f;
        _vent.SpeedScale = 0.5f + 0.5f * strength;
        _vent.ScaleAmountMin = 0.5f * strength;
        _vent.ScaleAmountMax = strength;
        _vent.Emitting = true;
    }

    private void EnsureVent()
    {
        if (_vent != null) return;
        var material = MakeGlowMaterial(VentColor);
        material.VertexColorUseAsAlbedo = true;
        _vent = new CpuParticles3D
        {
            Name = "LeakVent",
            Amount = Mathf.Max(1, VentAmount),
            Lifetime = 1.2f,
            Mesh = new SphereMesh { Radius = 0.35f, Height = 0.7f, RadialSegments = 6, Rings = 3, Material = material },
            Direction = Vector3.Right,
            Spread = 20f,
            InitialVelocityMin = VentSpeedMps * 0.6f,
            InitialVelocityMax = VentSpeedMps,
            Gravity = Vector3.Zero,
            DampingMin = 2f,
            DampingMax = 4f,
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1f,
            Color = VentColor,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(RocketPart.GetCellSize() * 0.5f, 0f, 0f),
        };
        AddChild(_vent);
    }
}
