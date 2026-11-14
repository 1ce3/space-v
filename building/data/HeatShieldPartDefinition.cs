using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Ablative heat shield used during atmospheric entry.</summary>
[GlobalClass]
public partial class HeatShieldPartDefinition : PartDefinition
{
    /// <summary>Ablator mass when new, in kg.</summary>
    [ExportGroup("Heat shield")]
    [Export] public float MaxAblatorKg { get; set; } = 80f;

    /// <summary>Heat absorbed per kg of ablator burned away, in J/kg.</summary>
    [Export] public float HeatOfAblationJPerKg { get; set; } = 150_000f;

    /// <summary>Sets the group to HeatShield so new definitions are valid without extra setup.</summary>
    public HeatShieldPartDefinition() => PartGroup = PartGroup.HeatShield;
}
