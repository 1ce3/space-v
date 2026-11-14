using Godot;

namespace RocketGame.Research;

/// <summary>One read-only node of the tech tree. Saved as data/tech/&lt;NodeId&gt;.tres. Purchase state lives in TechTreeManager.</summary>
[GlobalClass]
public partial class TechNode : Resource
{
    /// <summary>Snake_case id, same as the .tres file name.</summary>
    [Export] public string NodeId { get; set; } = "";
    /// <summary>Name shown to the player.</summary>
    [Export] public string DisplayName { get; set; } = "";
    /// <summary>Column in the research screen (1 = first).</summary>
    [Export(PropertyHint.Range, "1,10,1")] public int Tier { get; set; } = 1;
    /// <summary>Price in research points.</summary>
    [Export(PropertyHint.Range, "0,10000,1")] public int CostRP { get; set; }
    /// <summary>NodeIds that must be purchased first.</summary>
    [Export] public string[] PrerequisiteNodeIds { get; set; } = System.Array.Empty<string>();
    /// <summary>ModelIds (parts and modules) this node unlocks.</summary>
    [Export] public string[] UnlocksModelIds { get; set; } = System.Array.Empty<string>();
    /// <summary>Added to the maximum build weight when purchased.</summary>
    [Export(PropertyHint.Range, "0,100,1")] public int BuildWeightBonus { get; set; }
    /// <summary>Optional icon for the research screen.</summary>
    [Export] public Texture2D Icon { get; set; }
}
