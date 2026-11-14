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
    [Export] public bool DebugLog { get; set; }

    [ExportGroup("Folders")]
    [Export(PropertyHint.Dir)] public string PartsFolder { get; set; } = "res://data/parts";
    [Export(PropertyHint.Dir)] public string ModulesFolder { get; set; } = "res://data/modules";
    /// <summary>Placeholder scenes named after the PartGroup in snake_case, plus decoupler.tscn.</summary>
    [Export(PropertyHint.Dir)] public string PlaceholderFolder { get; set; } = "res://assets/parts/_placeholder";

    private readonly Dictionary<string, ComponentDefinition> _byId = new();

    public override void _EnterTree()
    {
        Instance = this;
        if (Settings == null)
        {
            GD.PushWarning("[Building] PartCatalog: Settings not set, using BuildSettings defaults");
            Settings = new BuildSettings();
        }
        _byId.Clear();
        LoadFolder(PartsFolder);
        LoadFolder(ModulesFolder);
        if (DebugLog) GD.Print($"[Building] PartCatalog: {_byId.Count} definitions loaded");
    }

    /// <summary>Loads every .tres of a folder whose resource is a ComponentDefinition.</summary>
    private void LoadFolder(string folder)
    {
        using DirAccess dir = DirAccess.Open(folder);
        if (dir == null)
        {
            GD.PushError($"[Building] PartCatalog: cannot open {folder}");
            return;
        }

        foreach (string rawName in dir.GetFiles())
        {
            // Exported builds list "x.tres.remap"; the loader resolves the original path.
            string fileName = rawName.EndsWith(".remap") ? rawName[..^".remap".Length] : rawName;
            if (!fileName.EndsWith(".tres")) continue;

            string path = folder.TrimEnd('/') + "/" + fileName;
            if (ResourceLoader.Load(path) is not ComponentDefinition def)
            {
                GD.PushError($"[Building] PartCatalog: {path} is not a ComponentDefinition");
                continue;
            }

            string expectedId = fileName[..^".tres".Length];
            if (def.ModelId != expectedId)
                GD.PushWarning($"[Building] PartCatalog: {path} has ModelId '{def.ModelId}' (expected '{expectedId}')");
            if (string.IsNullOrEmpty(def.ModelId)) continue;
            if (_byId.ContainsKey(def.ModelId))
                GD.PushError($"[Building] PartCatalog: duplicate ModelId '{def.ModelId}' ({path})");
            _byId[def.ModelId] = def;
        }
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
