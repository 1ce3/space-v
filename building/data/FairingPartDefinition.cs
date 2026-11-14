using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Nose fairing: shields the parts below it while nose-first.</summary>
[GlobalClass]
public partial class FairingPartDefinition : PartDefinition
{
    /// <summary>Strength of its self-separating joint (N).</summary>
    [ExportGroup("Fairing")]
    [Export] public float AttachmentStrength { get; set; } = 100_000f;
    /// <summary>q above which the fairing fails (Pa).</summary>
    [Export] public float MaxDynamicPressure { get; set; } = 60_000f;

    public FairingPartDefinition()
    {
        PartGroup = PartGroup.Fairing;
    }
}
