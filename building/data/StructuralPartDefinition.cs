using Godot;

using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Beam, adapter or other passive part.</summary>
[GlobalClass]
public partial class StructuralPartDefinition : PartDefinition
{
    /// <summary>Joint strength contributed by this part (N).</summary>
    [ExportGroup("Structural")]
    [Export] public float AttachmentStrength { get; set; } = 500_000f;

    public StructuralPartDefinition()
    {
        PartGroup = PartGroup.Structural;
    }
}
