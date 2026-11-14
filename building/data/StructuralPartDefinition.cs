using Godot;

namespace RocketGame.Building;

/// <summary>Plain structural part that holds other parts together.</summary>
[GlobalClass]
public partial class StructuralPartDefinition : PartDefinition
{
    /// <summary>Force the joint to this part can carry before it breaks, in N.</summary>
    [ExportGroup("Structural")]
    [Export] public float AttachmentStrength { get; set; } = 500_000f;
}
