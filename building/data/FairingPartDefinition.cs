using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Nose fairing. Shields the parts below it until jettisoned.</summary>
[GlobalClass]
public partial class FairingPartDefinition : PartDefinition
{
    /// <summary>Force the fairing joint can carry before it breaks, in N.</summary>
    [ExportGroup("Fairing")]
    [Export] public float AttachmentStrength { get; set; } = 100_000f;

    /// <summary>Dynamic pressure above which the fairing breaks, in Pa.</summary>
    [Export] public float MaxDynamicPressure { get; set; } = 60_000f;

    /// <summary>Sets the group to Fairing so new definitions are valid without extra setup.</summary>
    public FairingPartDefinition() => PartGroup = PartGroup.Fairing;
}
