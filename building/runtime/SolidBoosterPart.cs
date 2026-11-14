using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Solid booster: ignites when armed, burns until empty.</summary>
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

    public override float GetCurrentMass() => DryMass + RemainingPropellant;
}
