using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Ablative heat shield: protects the parts above it while bottom-first.</summary>
[GlobalClass]
public partial class HeatShieldPartDefinition : PartDefinition
{
    /// <summary>Ablator mass at launch (kg).</summary>
    [ExportGroup("HeatShield")]
    [Export] public float MaxAblatorKg { get; set; } = 80f;
    /// <summary>Heat absorbed per kg of ablator (J/kg).</summary>
    [Export] public float HeatOfAblationJPerKg { get; set; } = 150_000f;

    public HeatShieldPartDefinition()
    {
        PartGroup = PartGroup.HeatShield;
    }

    public override string GetFormattedTooltip() => base.GetFormattedTooltip() + $"\nAblator: {N(MaxAblatorKg, "0")} kg ({N(MaxAblatorKg * HeatOfAblationJPerKg / 1e6f, "0")} MJ)";
}
