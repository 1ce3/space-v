using Godot;

namespace RocketGame.Research;

/// <summary>Autoload stub created by C0. Owned by Dev B, who replaces it in B0/B4.</summary>
public partial class ResearchCampaignEngine : Node
{
    public static ResearchCampaignEngine Instance { get; private set; }

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }
}
