using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Presentation;

/// <summary>Heat shield darkens toward CharredColor as the ablator burns.</summary>
public partial class HeatShieldVisual : PartVisual
{
    [Export] public Color CharredColor { get; set; } = new(0.06f, 0.05f, 0.05f);
    /// <summary>Ablator fraction burned at which the shield looks fully charred.</summary>
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float FullCharAt { get; set; } = 0.6f;

    /// <summary>0 = fresh, 1 = fully charred (visual).</summary>
    public float CharAmount { get; private set; }

    private Color _fresh;

    public override void Bind(RocketPart part)
    {
        base.Bind(part);
        _fresh = BaseColor;
    }

    protected override void UpdateVisual(float deltaTime)
    {
        if (Part is HeatShieldPart shield) UpdateAblation(shield.GetAblatorFraction(), 0f);
        base.UpdateVisual(deltaTime);
    }

    /// <summary>ablatorFraction: 1 = full, 0 = gone. heatFlux is not used by the placeholder (glow comes from TempK).</summary>
    public void UpdateAblation(float ablatorFraction, float heatFlux)
    {
        float burned = 1f - Mathf.Clamp(ablatorFraction, 0f, 1f);
        float amount = Mathf.Clamp(burned / FullCharAt, 0f, 1f);
        if (Mathf.IsEqualApprox(amount, CharAmount)) return;
        CharAmount = amount;
        SetBaseColor(_fresh.Lerp(CharredColor, amount));
    }
}
