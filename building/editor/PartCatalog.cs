using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>
/// Autoload: loads every part and module .tres and turns a ModelId into its definition.
/// The only place where ModelIds become definitions (research only deals in ids).
/// </summary>
public partial class PartCatalog : Node
{
    public static PartCatalog Instance { get; private set; }

    [Export] public BuildSettings Settings { get; set; }

    [ExportGroup("Folders")]
    [Export(PropertyHint.Dir)] public string PartsFolder { get; set; } = "res://data/parts";
    [Export(PropertyHint.Dir)] public string ModulesFolder { get; set; } = "res://data/modules";
    /// <summary>Placeholder scenes named after the PartGroup in snake_case, plus decoupler.tscn.</summary>
    [Export(PropertyHint.Dir)] public string PlaceholderFolder { get; set; } = "res://assets/parts/_placeholder";

    private readonly Dictionary<string, ComponentDefinition> _byId = new();

    public override void _EnterTree()
    {
        Instance = this;
        // TODO(A5): load every .tres in PartsFolder and ModulesFolder into _byId.
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Definition for a ModelId, or null.</summary>
    public ComponentDefinition GetDefinition(string modelId)
    {
        if (string.IsNullOrEmpty(modelId)) return null;
        return _byId.TryGetValue(modelId, out ComponentDefinition def) ? def : null;
    }

    /// <summary>Every loaded definition.</summary>
    public List<ComponentDefinition> GetAll() => new(_byId.Values);

    /// <summary>Unlocked definitions of type T (filters TechTree.GetUnlockedModels()).</summary>
    public List<T> GetUnlocked<T>() where T : ComponentDefinition
    {
        // TODO(A5): filter by ResearchCampaignEngine.Instance.TechTree.GetUnlockedModels().
        var result = new List<T>();
        foreach (ComponentDefinition def in _byId.Values)
            if (def is T typed) result.Add(typed);
        return result;
    }

    /// <summary>ModelIds that the tech tree knows but no .tres provides (and the reverse).</summary>
    public List<string> ValidateAgainstTechTree()
    {
        // TODO(A5)
        return new List<string>();
    }

    /// <summary>Part scenes missing a required anchor or a PartVisual root.</summary>
    public List<string> ValidateScenes()
    {
        // TODO(A5)
        return new List<string>();
    }

    /// <summary>Placeholder scene of a group (assets/parts/_placeholder/&lt;group&gt;.tscn).</summary>
    public PackedScene GetPlaceholderScene(PartGroup group)
    {
        // TODO(A5)
        return null;
    }

    /// <summary>Placeholder scene for decouplers.</summary>
    public PackedScene GetDecouplerPlaceholderScene()
    {
        // TODO(A5)
        return null;
    }
}
