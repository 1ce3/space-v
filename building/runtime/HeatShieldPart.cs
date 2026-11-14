using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Ablative heat shield.</summary>
public partial class HeatShieldPart : RocketPart
{
    /// <summary>Ablator left (kg). Physics writes it through Ablate.</summary>
    public float AblatorRemainingKg { get; set; }
    /// <summary>Parts it protects when bottom-first (chain of parents up to the pod).</summary>
    public List<RocketPart> ShieldedParts { get; } = new();

    public HeatShieldPartDefinition ShieldDefinition => Definition as HeatShieldPartDefinition;

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        AblatorRemainingKg = ShieldDefinition?.MaxAblatorKg ?? 0f;
    }

    public override float GetCurrentMass() => GetEffectiveDryMass() + AblatorRemainingKg;

    /// <summary>Ablator fraction left, 0..1.</summary>
    public float GetAblatorFraction()
    {
        float max = ShieldDefinition?.MaxAblatorKg ?? 0f;
        return max > 0f ? AblatorRemainingKg / max : 0f;
    }

    /// <summary>Burns ablator to absorb heat; returns the heat that gets through (J).</summary>
    public float Ablate(float heatJoule)
    {
        float heatPerKg = ShieldDefinition?.HeatOfAblationJPerKg ?? 0f;
        if (heatJoule <= 0f || heatPerKg <= 0f || AblatorRemainingKg <= 0f) return heatJoule;
        float burnedKg = Mathf.Min(heatJoule / heatPerKg, AblatorRemainingKg);
        AblatorRemainingKg -= burnedKg;
        return heatJoule - burnedKg * heatPerKg;
    }
}
