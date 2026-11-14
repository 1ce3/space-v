using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Presentation;

/// <summary>Control fins rotate around FinHinge by their deflection; fixed fins stay still.</summary>
public partial class FinVisual : PartVisual
{
    /// <summary>Visual multiplier of the deflection angle.</summary>
    [Export] public float DeflectionExaggeration { get; set; } = 1.5f;
    [Export] public float Smoothing { get; set; } = 15f;

    private Node3D _hinge;
    private float _angle;

    public override void Bind(RocketPart part)
    {
        base.Bind(part);
        _hinge = FindAnchor("FinHinge");
    }

    protected override void UpdateVisual(float deltaTime)
    {
        base.UpdateVisual(deltaTime);
        if (Part is not ControlFinPart fin) return;
        _angle = Mathf.Lerp(_angle, fin.DeflectionAngleDeg, 1f - Mathf.Exp(-Smoothing * deltaTime));
        UpdateDeflection(_angle);
    }

    public void UpdateDeflection(float deflectionAngleDeg)
    {
        if (_hinge != null) _hinge.RotationDegrees = new Vector3(0f, 0f, deflectionAngleDeg * DeflectionExaggeration);
    }
}
