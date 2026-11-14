using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Decoupler placed on a cell edge; separates the joint it sits on.</summary>
[GlobalClass]
public partial class DecouplerPartDefinition : StructuralPartDefinition
{
    /// <summary>Separation impulse (N*s).</summary>
    [ExportGroup("Decoupler")]
    [Export] public float EjectionImpulse { get; set; } = 3000f;

    public DecouplerPartDefinition()
    {
        AttachmentStrength = 300_000f;
    }

    public override string GetFormattedTooltip() => base.GetFormattedTooltip() + (EjectionImpulse > 0f ? $"\nEjection: {N(EjectionImpulse, "0")} N·s" : "");
}
