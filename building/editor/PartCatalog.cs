using System.Collections.Generic;
using Godot;
using RocketGame.Core;
using RocketGame.Research;

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
    /// <summary>Run ValidateAgainstTechTree and ValidateScenes when the game starts.</summary>
    [Export] public bool ValidateOnStart { get; set; } = true;

    [ExportGroup("Folders")]
    [Export(PropertyHint.Dir)] public string PartsFolder { get; set; } = "res://data/parts";
    [Export(PropertyHint.Dir)] public string ModulesFolder { get; set; } = "res://data/modules";
    /// <summary>Phase 2: hull materials (MaterialDefinition .tres, file name = MaterialId).</summary>
    [Export(PropertyHint.Dir)] public string MaterialsFolder { get; set; } = "res://data/materials";

    private readonly Dictionary<string, MaterialDefinition> _materials = new();
    /// <summary>Placeholder scenes named after the PartGroup in snake_case, plus decoupler.tscn.</summary>
    [Export(PropertyHint.Dir)] public string PlaceholderFolder { get; set; } = "res://assets/parts/_placeholder";

    private readonly Dictionary<string, ComponentDefinition> _byId = new();
    private readonly Dictionary<string, PackedScene> _placeholders = new();

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
        LoadMaterials(MaterialsFolder);
        if (DebugLog) GD.Print($"[Building] PartCatalog: {_byId.Count} definitions loaded");
    }

    public override void _Ready()
    {
        // ResearchCampaignEngine is the next autoload: validate after every autoload is ready.
        if (ValidateOnStart) Callable.From(ValidateOnStartup).CallDeferred();
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
    /// <summary>Phase 2: the material with this id, or null.</summary>
    public MaterialDefinition GetMaterial(string materialId) =>
        materialId != null && _materials.TryGetValue(materialId, out MaterialDefinition m) ? m : null;

    /// <summary>Phase 2: every loaded material.</summary>
    public List<MaterialDefinition> GetAllMaterials() => new(_materials.Values);

    /// <summary>A12: materials the player may use: tier 0, or listed in an unlocked tech node. Sorted by tier.</summary>
    public List<MaterialDefinition> GetUnlockedMaterials()
    {
        TechTreeManager tree = ResearchCampaignEngine.Instance?.TechTree;
        var result = new List<MaterialDefinition>();
        foreach (MaterialDefinition m in _materials.Values)
            if (m.Tier <= 0 || tree == null || tree.IsUnlocked(m.MaterialId)) result.Add(m);
        result.Sort((a, b) => a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : string.CompareOrdinal(a.MaterialId, b.MaterialId));
        return result;
    }

    /// <summary>A12: the material a part of this definition gets for materialId (empty = its default); null when it takes none.</summary>
    public MaterialDefinition ResolveMaterial(PartDefinition def, string materialId)
    {
        if (def == null || !def.AcceptsMaterial) return null;
        return GetMaterial(string.IsNullOrEmpty(materialId) ? def.DefaultMaterialId : materialId) ?? GetMaterial(def.DefaultMaterialId);
    }

    private void LoadMaterials(string folder)
    {
        _materials.Clear();
        if (string.IsNullOrEmpty(folder) || !DirAccess.DirExistsAbsolute(folder)) return; // folder arrives with A12
        using DirAccess dir = DirAccess.Open(folder);
        if (dir == null) return;
        foreach (string file in dir.GetFiles())
        {
            string name = file.EndsWith(".remap") ? file[..^6] : file;
            if (!name.EndsWith(".tres")) continue;
            if (ResourceLoader.Load(folder + "/" + name) is MaterialDefinition m && !string.IsNullOrEmpty(m.MaterialId))
                _materials[m.MaterialId] = m;
        }
    }

    public ComponentDefinition GetDefinition(string modelId)
    {
        if (string.IsNullOrEmpty(modelId)) return null;
        return _byId.TryGetValue(modelId, out ComponentDefinition def) ? def : null;
    }

    /// <summary>Every loaded definition.</summary>
    public List<ComponentDefinition> GetAll() => new(_byId.Values);

    /// <summary>
    /// Unlocked definitions of type T, sorted by Tier then ModelId (filters TechTree.GetUnlockedModels()).
    /// Without a campaign (sandboxes) every definition counts as unlocked.
    /// </summary>
    public List<T> GetUnlocked<T>() where T : ComponentDefinition
    {
        TechTreeManager techTree = ResearchCampaignEngine.Instance?.TechTree;
        var result = new List<T>();
        foreach (ComponentDefinition def in _byId.Values)
            if (def is T typed && (techTree == null || techTree.IsUnlocked(def.ModelId))) result.Add(typed);
        result.Sort((a, b) => a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : string.CompareOrdinal(a.ModelId, b.ModelId));
        return result;
    }

    /// <summary>ModelIds that the tech tree knows but no .tres provides (and the reverse). Empty = consistent.</summary>
    public List<string> ValidateAgainstTechTree()
    {
        var problems = new List<string>();
        TechTreeManager techTree = ResearchCampaignEngine.Instance?.TechTree;
        if (techTree == null) return problems;

        var known = new HashSet<string>();
        foreach (string id in techTree.StarterModelIds) known.Add(id);
        foreach (TechNode node in techTree.AllNodes)
        {
            if (node == null) continue;
            foreach (string id in node.UnlocksModelIds) known.Add(id);
        }

        foreach (string id in known)
            if (!_byId.ContainsKey(id) && GetMaterial(id) == null) problems.Add($"tech tree unlocks '{id}' but there is no .tres for it");
        foreach (string id in _byId.Keys)
            if (!known.Contains(id)) problems.Add($"'{id}' has a .tres but no starter entry or tech node unlocks it");
        return problems;
    }

    /// <summary>Part scenes (own or placeholder) missing a PartVisual root, MeshRoot or a required anchor. Empty = all fine.</summary>
    public List<string> ValidateScenes()
    {
        var problems = new List<string>();
        foreach (ComponentDefinition def in _byId.Values)
        {
            if (def is not PartDefinition part) continue;
            bool isDecoupler = part is DecouplerPartDefinition;
            PackedScene scene = part.Scene ?? (isDecoupler ? GetDecouplerPlaceholderScene() : GetPlaceholderScene(part.PartGroup));
            if (scene == null)
            {
                problems.Add($"{part.ModelId}: no scene and no placeholder");
                continue;
            }

            Node root = scene.Instantiate();
            if (root is Node3D && !root.HasMethod(BindMethod) && part.Scene != null)
            {
                // Plain model (.glb): RocketAssembler.AttachVisual fits it into the group placeholder.
                root.Free();
                continue;
            }
            if (root is not Node3D || !root.HasMethod(BindMethod))
                problems.Add($"{part.ModelId}: scene root is not a PartVisual ({scene.ResourcePath})");
            if (root.GetNodeOrNull("MeshRoot") == null)
                problems.Add($"{part.ModelId}: scene has no MeshRoot child ({scene.ResourcePath})");
            foreach (string anchor in isDecoupler ? System.Array.Empty<string>() : GetRequiredAnchors(part.PartGroup))
                if (root.FindChild(anchor, true, false) == null)
                    problems.Add($"{part.ModelId}: missing anchor '{anchor}' ({scene.ResourcePath})");
            root.Free();
        }
        return problems;
    }

    /// <summary>Anchor nodes a part scene of this group must contain (A_BUILDING.md 6).</summary>
    public static string[] GetRequiredAnchors(PartGroup group) => group switch
    {
        PartGroup.Fairing => new[] { "HalfLeft", "HalfRight" },
        PartGroup.Parachute => new[] { "ChuteAttach", "Canopy" },
        PartGroup.LiquidEngine => new[] { "ExhaustPoint", "GimbalPivot" },
        PartGroup.SolidBooster => new[] { "ExhaustPoint" },
        PartGroup.AeroSurface => new[] { "FinHinge" },
        _ => System.Array.Empty<string>(),
    };

    /// <summary>Placeholder scene of a group (assets/parts/_placeholder/&lt;group&gt;.tscn), or null.</summary>
    public PackedScene GetPlaceholderScene(PartGroup group) => LoadPlaceholder(ToSnakeCase(group.ToString()));

    /// <summary>Placeholder scene for decouplers (assets/parts/_placeholder/decoupler.tscn), or null.</summary>
    public PackedScene GetDecouplerPlaceholderScene() => LoadPlaceholder("decoupler");

    /// <summary>Name of the PartVisual method the assembler calls (Building never references PartVisual, AGENTS 1.5).</summary>
    public const string BindMethod = "Bind";

    private PackedScene LoadPlaceholder(string name)
    {
        if (_placeholders.TryGetValue(name, out PackedScene cached)) return cached;
        string path = PlaceholderFolder.TrimEnd('/') + "/" + name + ".tscn";
        PackedScene scene = ResourceLoader.Exists(path) ? ResourceLoader.Load<PackedScene>(path) : null;
        if (scene == null) GD.PushWarning($"[Building] PartCatalog: no placeholder scene {path}");
        _placeholders[name] = scene;
        return scene;
    }

    /// <summary>CommandPod -> command_pod.</summary>
    private static string ToSnakeCase(string name)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(name[i]));
        }
        return sb.ToString();
    }

    /// <summary>Runs both validators once every autoload is ready and reports problems as errors.</summary>
    private void ValidateOnStartup()
    {
        foreach (string problem in ValidateAgainstTechTree()) GD.PushError($"[Building] PartCatalog: {problem}");
        foreach (string problem in ValidateScenes()) GD.PushError($"[Building] PartCatalog: {problem}");
    }
}
