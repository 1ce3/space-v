using System.Collections.Generic;
using Godot;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Building;

/// <summary>
/// Build screen logic: place/remove parts, decouplers and modules within the budget, validate,
/// and emit DesignConfirmed. Never calls Control. Preview parts are real RocketParts under PartsRoot.
/// </summary>
public partial class BuildingSystem : Node
{
    [Signal] public delegate void PartPlacedEventHandler(RocketPart part);
    [Signal] public delegate void PartRemovedEventHandler(RocketPart part);
    [Signal] public delegate void PartMovedEventHandler(RocketPart part);
    [Signal] public delegate void ModuleChangedEventHandler(ModuleSlotType slotType, int slotIndex);
    [Signal] public delegate void DesignConfirmedEventHandler(AssembledRocket rocket);
    /// <summary>Anything in the design changed (parts, decouplers, modules, clear). The UI refreshes on it.</summary>
    [Signal] public delegate void DesignChangedEventHandler();
    /// <summary>ConfirmDesign refused: the design has validation errors.</summary>
    [Signal] public delegate void ConfirmRejectedEventHandler(string[] errors);

    /// <summary>Parent node for the preview parts shown on the grid.</summary>
    [Export] public Node3D PartsRoot { get; set; }
    /// <summary>Restore user://current_design.json when the screen opens.</summary>
    [Export] public bool LoadSavedOnReady { get; set; } = true;
    [Export] public bool DebugLog { get; set; }

    public BuildGrid Grid { get; private set; }
    public BuildBudget Budget { get; private set; }
    /// <summary>The PartCatalog autoload.</summary>
    public PartCatalog Catalog => PartCatalog.Instance;
    public RocketDesign Design { get; private set; } = new();
    /// <summary>Preview pod modules by (slot type, index).</summary>
    public IReadOnlyDictionary<(ModuleSlotType, int), ModuleDefinition> InstalledModules => _modules;

    private readonly Dictionary<(ModuleSlotType, int), ModuleDefinition> _modules = new();

    public override void _Ready()
    {
        NodeGuard.Require(this, PartsRoot, nameof(PartsRoot));
        BuildSettings settings = Catalog?.Settings;
        Grid = new BuildGrid(settings?.GridWidth ?? 9, settings?.GridHeight ?? 16, settings?.CellSize ?? BuildSettings.DefaultCellSize);
        Budget = new BuildBudget(GetMaxWeight());
        if (LoadSavedOnReady) Callable.From(LoadSavedDesign).CallDeferred();
    }

    /// <summary>Max build weight from the tech tree (no campaign: unlimited).</summary>
    public static int GetMaxWeight()
    {
        TechTreeManager techTree = ResearchCampaignEngine.Instance?.TechTree;
        return techTree?.GetMaxBuildWeight() ?? RocketAssembler.NoWeightLimit;
    }

    /// <summary>Unlocked part definitions for the palette.</summary>
    public List<PartDefinition> GetAvailableParts() => Catalog?.GetUnlocked<PartDefinition>() ?? new List<PartDefinition>();

    /// <summary>Unlocked modules that fit a slot type.</summary>
    public List<ModuleDefinition> GetAvailableModules(ModuleSlotType slot)
    {
        var result = new List<ModuleDefinition>();
        if (Catalog == null) return result;
        foreach (ModuleDefinition def in Catalog.GetUnlocked<ModuleDefinition>())
            if (def.SlotType == slot) result.Add(def);
        return result;
    }

    /// <summary>True when the part fits (cell, neighbors) and the budget allows it.</summary>
    public bool CanPlacePart(string modelId, Vector2I cell) =>
        Catalog?.GetDefinition(modelId) is PartDefinition def && def is not DecouplerPartDefinition
        && Grid.CanPlace(def, cell) && Budget.CanAfford(def);

    public bool PlacePart(string modelId, Vector2I cell)
    {
        if (!CanPlacePart(modelId, cell)) return false;
        var def = (PartDefinition)Catalog.GetDefinition(modelId);
        RocketPart part = CreatePreview(def, cell);
        Grid.Occupy(part);
        Design.AddPart(modelId, cell);
        Budget.Add(def);
        if (DebugLog) GD.Print($"[Building] Placed {modelId} at {cell} ({Budget.CurrentWeight}/{Budget.MaxWeight})");
        EmitSignal(SignalName.PartPlaced, part);
        EmitSignal(SignalName.DesignChanged);
        return true;
    }

    /// <summary>Removes the part covering cell, its orphan decouplers and (for the pod) its modules; refunds weight.</summary>
    public void RemovePart(Vector2I cell)
    {
        RocketPart part = Grid.GetPartAt(cell);
        if (part == null) return;

        Grid.Vacate(part);
        Design.PartEntries.RemoveAll(e => e.ModelId == part.ModelId && e.Cell == part.Cell);
        Budget.Remove(part.Definition);
        if (part is CommandPodPart) ClearModules();
        RemoveOrphanDecouplers();

        EmitSignal(SignalName.PartRemoved, part);
        part.QueueFree();
        EmitSignal(SignalName.DesignChanged);
    }

    public bool MovePart(Vector2I from, Vector2I to)
    {
        // TODO(A9)
        return false;
    }

    /// <summary>True when the edge can take a decoupler and the budget allows it.</summary>
    public bool CanPlaceDecoupler(string modelId, Edge edge) =>
        Catalog?.GetDefinition(modelId) is DecouplerPartDefinition def && Grid.CanPlaceDecoupler(def, edge) && Budget.CanAfford(def);

    public bool PlaceDecoupler(string modelId, Edge edge)
    {
        if (!CanPlaceDecoupler(modelId, edge)) return false;
        var def = (DecouplerPartDefinition)Catalog.GetDefinition(modelId);
        var decoupler = (DecouplerPart)RocketPart.Create(def);
        Grid.AddDecoupler(decoupler, edge);
        decoupler.Cell = decoupler.Edge.Cell;
        AddPreviewNode(decoupler);
        Design.AddDecoupler(modelId, edge);
        Budget.Add(def);
        EmitSignal(SignalName.PartPlaced, decoupler);
        EmitSignal(SignalName.DesignChanged);
        return true;
    }

    public void RemoveDecoupler(Edge edge)
    {
        DecouplerPart decoupler = Grid.RemoveDecoupler(edge);
        if (decoupler == null) return;
        Edge normalized = BuildGrid.Normalize(edge);
        Design.DecouplerEntries.RemoveAll(e => e.Edge == normalized);
        Budget.Remove(decoupler.Definition);
        EmitSignal(SignalName.PartRemoved, decoupler);
        decoupler.QueueFree();
        EmitSignal(SignalName.DesignChanged);
    }

    /// <summary>The command pod on the grid, or null.</summary>
    public CommandPodPart GetPod()
    {
        foreach (RocketPart part in Grid.Parts)
            if (part is CommandPodPart pod) return pod;
        return null;
    }

    /// <summary>Installs a module; its slot type comes from the definition, slotIndex is within that type.</summary>
    public bool InstallModule(string modelId, int slotIndex)
    {
        if (Catalog?.GetDefinition(modelId) is not ModuleDefinition def) return false;
        CommandPodPart pod = GetPod();
        int slots = pod?.PodDefinition?.GetSlotCount(def.SlotType) ?? 0;
        if (slotIndex < 0 || slotIndex >= slots) return false;

        (ModuleSlotType, int) key = (def.SlotType, slotIndex);
        ModuleDefinition old = _modules.TryGetValue(key, out ModuleDefinition existing) ? existing : null;
        int extra = def.BuildWeight - (old?.BuildWeight ?? 0);
        if (extra > Budget.Remaining) return false;

        if (old != null) RemoveModule(def.SlotType, slotIndex);
        _modules[key] = def;
        Design.AddModule(modelId, def.SlotType, slotIndex);
        Budget.Add(def);
        EmitSignal(SignalName.ModuleChanged, Variant.From(def.SlotType), slotIndex);
        EmitSignal(SignalName.DesignChanged);
        return true;
    }

    public void RemoveModule(ModuleSlotType slotType, int slotIndex)
    {
        if (!_modules.TryGetValue((slotType, slotIndex), out ModuleDefinition def)) return;
        _modules.Remove((slotType, slotIndex));
        Design.ModuleEntries.RemoveAll(e => e.SlotType == slotType && e.SlotIndex == slotIndex);
        Budget.Remove(def);
        EmitSignal(SignalName.ModuleChanged, Variant.From(slotType), slotIndex);
        EmitSignal(SignalName.DesignChanged);
    }

    /// <summary>Player-facing errors of the current design.</summary>
    public List<string> Validate() => new RocketAssembler().ValidateConnections(Design, Budget.MaxWeight);

    /// <summary>Mass, CoM and inertia of the preview (parts, decouplers, modules; no payload).</summary>
    public MassProperties GetPreviewMass()
    {
        var parts = new List<RocketPart>(Grid.Parts);
        parts.AddRange(Grid.Decouplers);
        MassProperties mp = MassCenterCalculator.Calculate(parts);
        float modules = 0f;
        foreach (ModuleDefinition def in _modules.Values) modules += def.DryMass;
        CommandPodPart pod = GetPod();
        if (modules > 0f && pod != null && mp.TotalMass > 0f)
        {
            // Modules sit in the pod: shift the CoM toward it.
            Vector2 podPos = pod.GetLocalPosition();
            float total = mp.TotalMass + modules;
            mp.CenterOfMass = (mp.CenterOfMass * mp.TotalMass + podPos * modules) / total;
            mp.TotalMass = total;
        }
        return mp;
    }

    /// <summary>Validates, assembles a fresh rocket, saves the design and emits DesignConfirmed.</summary>
    public void ConfirmDesign()
    {
        List<string> errors = Validate();
        if (errors.Count > 0)
        {
            if (DebugLog) GD.Print($"[Building] Confirm refused: {string.Join(" | ", errors)}");
            EmitSignal(SignalName.ConfirmRejected, errors.ToArray());
            return;
        }

        AssembledRocket rocket = new RocketAssembler().AssembleRocket(Design);
        Design.SaveCurrent();
        if (DebugLog)
            GD.Print($"[Building] Design confirmed: {rocket.PartTree.ActiveParts.Count} parts, {rocket.Stages.Count} stages, {rocket.MassProperties.TotalMass:0} kg");
        EmitSignal(SignalName.DesignConfirmed, rocket);
    }

    /// <summary>Removes everything from the grid.</summary>
    public void ClearAll()
    {
        foreach (RocketPart part in Grid.Parts) part.QueueFree();
        foreach (DecouplerPart decoupler in Grid.Decouplers) decoupler.QueueFree();
        Grid.Clear();
        _modules.Clear();
        Design.Clear();
        Budget.Clear();
        Budget.SetMax(GetMaxWeight());
        EmitSignal(SignalName.DesignChanged);
    }

    public void ShowWindTunnel()
    {
        // TODO(A9, P2)
    }

    /// <summary>Restores user://current_design.json onto the grid (unknown or locked parts are skipped).</summary>
    public void LoadSavedDesign()
    {
        RocketDesign saved = RocketDesign.LoadLast();
        if (saved == null || saved.IsEmpty) return;
        LoadDesign(saved);
    }

    /// <summary>Puts a design on the grid through the normal place calls (so every rule applies).</summary>
    public void LoadDesign(RocketDesign design)
    {
        ClearAll();
        if (design == null) return;
        int skipped = 0;
        foreach (RocketDesign.PartEntry e in design.PartEntries)
            if (!IsUnlocked(e.ModelId) || !PlacePart(e.ModelId, e.Cell)) skipped++;
        foreach (RocketDesign.DecouplerEntry e in design.DecouplerEntries)
            if (!IsUnlocked(e.ModelId) || !PlaceDecoupler(e.ModelId, e.Edge)) skipped++;
        foreach (RocketDesign.ModuleEntry e in design.ModuleEntries)
            if (!IsUnlocked(e.ModelId) || !InstallModule(e.ModelId, e.SlotIndex)) skipped++;
        if (skipped > 0) GD.PushWarning($"[Building] LoadDesign: {skipped} entries could not be restored");
    }

    private static bool IsUnlocked(string modelId)
    {
        TechTreeManager techTree = ResearchCampaignEngine.Instance?.TechTree;
        return techTree == null || techTree.IsUnlocked(modelId);
    }

    private RocketPart CreatePreview(PartDefinition def, Vector2I cell)
    {
        RocketPart part = RocketPart.Create(def);
        part.Cell = cell;
        AddPreviewNode(part);
        return part;
    }

    private void AddPreviewNode(RocketPart part)
    {
        PartsRoot.AddChild(part);
        Vector2 local = part.GetLocalPosition();
        part.Position = new Vector3(local.X, local.Y, 0f);
        RocketAssembler.AttachVisual(part);
    }

    private void RemoveOrphanDecouplers()
    {
        foreach (DecouplerPart orphan in Grid.GetOrphanDecouplers()) RemoveDecoupler(orphan.Edge);
    }

    private void ClearModules()
    {
        foreach ((ModuleSlotType type, int index) in new List<(ModuleSlotType, int)>(_modules.Keys)) RemoveModule(type, index);
    }
}
