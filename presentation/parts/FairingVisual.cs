using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Presentation;

/// <summary>Fairing halves (HalfLeft, HalfRight) swing open and drift apart when the fairing is jettisoned.</summary>
public partial class FairingVisual : PartVisual
{
    /// <summary>Seconds of the opening animation.</summary>
    [Export] public float JettisonTimeS { get; set; } = 1.5f;
    /// <summary>Sideways travel of each half (local units of MeshRoot, 1 = one cell).</summary>
    [Export] public float SpreadDistance { get; set; } = 0.8f;
    /// <summary>Outward tilt of each half at the end (degrees).</summary>
    [Export] public float SpreadAngleDeg { get; set; } = 35f;

    public bool IsJettisoned { get; private set; }

    private Node3D _left;
    private Node3D _right;

    public override void Bind(RocketPart part)
    {
        base.Bind(part);
        _left = FindAnchor("HalfLeft");
        _right = FindAnchor("HalfRight");
    }

    /// <summary>Jettison arrives as SeverCause.Jettison from debris spawning.</summary>
    public override void OnSevered(SeverCause cause)
    {
        base.OnSevered(cause);
        if (cause == SeverCause.Jettison) PlayJettison();
    }

    public void PlayJettison()
    {
        if (IsJettisoned) return;
        IsJettisoned = true;
        if (_left == null || _right == null || !IsInsideTree()) return;

        Tween tween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        Spread(tween, _left, -1f);
        Spread(tween, _right, 1f);
    }

    private void Spread(Tween tween, Node3D half, float direction)
    {
        tween.TweenProperty(half, "position", half.Position + new Vector3(direction * SpreadDistance, 0f, 0f), JettisonTimeS);
        tween.TweenProperty(half, "rotation_degrees", half.RotationDegrees + new Vector3(0f, 0f, -direction * SpreadAngleDeg), JettisonTimeS);
    }
}
