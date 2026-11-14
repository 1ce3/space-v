using Godot;

namespace RocketGame.Building;

/// <summary>Autoload stub created by C0. Owned by Dev A, who replaces it in A0/A5.</summary>
public partial class PartCatalog : Node
{
    public static PartCatalog Instance { get; private set; }

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }
}
