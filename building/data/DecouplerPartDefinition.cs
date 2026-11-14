using Godot;

namespace RocketGame.Building;

/// <summary>Decoupler placed on a cell edge. Its PartGroup stays Structural but is not used for neighbor rules.</summary>
[GlobalClass]
public partial class DecouplerPartDefinition : StructuralPartDefinition
{
    /// <summary>Separation impulse applied to the jettisoned subtree, in N*s.</summary>
    [ExportGroup("Decoupler")]
    [Export] public float EjectionImpulse { get; set; } = 3000f;
}
