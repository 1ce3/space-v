using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Solid booster: ignites when armed, burns until empty, cannot shut down.</summary>
public partial class SolidBoosterPart : EnginePart
{
    /// <summary>0..1, physics advances it.</summary>
    public float BurnProgress { get; set; }
    /// <summary>Propellant left (kg). Physics writes it.</summary>
    public float RemainingPropellant { get; set; }

    public SolidBoosterPartDefinition BoosterDefinition => Definition as SolidBoosterPartDefinition;

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        BurnProgress = 0f;
        RemainingPropellant = BoosterDefinition?.PropellantMass ?? 0f;
    }

    public override float GetCurrentMass() => GetEffectiveDryMass() + RemainingPropellant;

    /// <summary>Arming a solid booster lights it.</summary>
    public override void Arm()
    {
        base.Arm();
        Ignite();
    }

    protected override bool TryConsumeIgnition() => RemainingPropellant > 0f;

    protected override float GetThrottleFactor() => 1f;

    /// <summary>BurnCurve sampled at BurnProgress (1 when no curve).</summary>
    protected override float GetThrustCurveFactor() =>
        BoosterDefinition?.BurnCurve?.Sample(Mathf.Clamp(BurnProgress, 0f, 1f)) ?? 1f;
}
