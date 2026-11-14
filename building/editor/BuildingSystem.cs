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
    /// <summary>A module was installed or removed; cell is its top-left inventory cell.</summary>
    [Signal] public delegate void ModuleChangedEventHandler(Vector2I cell);
    [Signal] public delegate void DesignConfirmedEventHandler(AssembledRocket rocket);
    /// <summary>Anything in the design changed (parts, decouplers, modules, clear). The UI refreshes on it.</summary>
    [Signal] public delegate void DesignChangedEventHandler();
    /// <summary>ConfirmDesign refused: the design has validation errors.</summary>
    [Signal] public delegate void ConfirmRejectedEventHandler(string[] errors);
    /// <summary>ShowWindTunnel finished; read LastAeroReport.</summary>
    [Signal] public delegate void WindTunnelUpdatedEventHandler();

    /// <summary>Parent node for the preview parts shown on the grid.</summary>
    [Export] public Node3D PartsRoot { get; set; }
    /// <summary>Restore user://current_design.json when the screen opens.</summary>
    [Export] public bool LoadSavedOnReady { get; set; } = true;
    [Export] public bool DebugLog { get; set; }

    [ExportGroup("Wind tunnel")]
    /// <summary>Altitude of the ShowWindTunnel test (m, Earth air).</summary>
    [Export] public float WindTunnelAltitudeM { get; set; } = 10000f;
    /// <summary>Speed of the ShowWindTunnel test (m/s).</summary>
    [Export] public float WindTunnelSpeedMps { get; set; } = 400f;
    /// <summary>Angle between nose and flow in the ShowWindTunnel test (deg).</summary>
    [Export(PropertyHint.Range, "0,30,0.5")] public float WindTunnelAngleDeg { get; set; } = 5f;

    /// <summary>Result of the last ShowWindTunnel.</summary>
    public AeroReport LastAeroReport { get; private set; }

    public BuildGrid Grid { get; private set; }
    public BuildBudget Budget { get; private set; }
    /// <summary>The PartCatalog autoload.</summary>
    public PartCatalog Catalog => PartCatalog.Instance;
    public RocketDesign Design { get; private set; } = new();
    /// <summary>Preview pod modules by (slot type, index).</summary>
    /// <summary>Installed modules by top-left inventory cell.</summary>
    public IReadOnlyDictionary<Vector2I, ModuleDefinition> InstalledModules => _modules;

    private readonly Dictionary<Vector2I, ModuleDefinition> _modules = new();

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

    /// <summary>Unlocked modules (any group) for the pod inventory.</summary>
    public List<ModuleDefinition> GetAvailableModules() => Catalog?.GetUnlocked<ModuleDefinition>() ?? new List<ModuleDefinition>();

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

    /// <summary>True when the part covering from can have its lower-left cell at to (cells, neighbors; weight does not change).</summary>
    public bool CanMovePart(Vector2I from, Vector2I to)
    {
        RocketPart part = Grid.GetPartAt(from);
        if (part == null || part is DecouplerPart) return false;
        if (to == part.Cell) return true;
        Grid.Vacate(part);
        bool ok = Grid.CanPlace(part.Definition, to);
        Grid.Occupy(part);
        return ok;
    }

    /// <summary>
    /// Moves the part covering from so its lower-left cell becomes to (A9, drag and drop). Pod modules stay; decouplers that
    /// no longer sit between two touching parts are removed and refunded. False (nothing changes) when it does not fit.
    /// </summary>
    public bool MovePart(Vector2I from, Vector2I to)
    {
        if (!CanMovePart(from, to)) return false;
        RocketPart part = Grid.GetPartAt(from);
        Vector2I old = part.Cell;
        if (to == old) return true;

        Grid.Vacate(part);
        part.Cell = to;
        Grid.Occupy(part);
        RocketDesign.PartEntry entry = Design.PartEntries.Find(e => e.ModelId == part.ModelId && e.Cell == old);
        if (entry != null) entry.Cell = to;
        Vector2 local = part.GetLocalPosition();
        part.Position = new Vector3(local.X, local.Y, 0f);
        RemoveOrphanDecouplers();

        if (DebugLog) GD.Print($"[Building] Moved {part.ModelId} {old} -> {to}");
        EmitSignal(SignalName.PartMoved, part);
        EmitSignal(SignalName.DesignChanged);
        return true;
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

    /// <summary>Size of the pod inventory grid, or (0,0) without a pod.</summary>
    public Vector2I InventorySize => GetPod()?.InventorySize ?? Vector2I.Zero;

    /// <summary>Rectangles taken by the installed modules, optionally without the one anchored at skipCell.</summary>
    public List<PodInventory.Item> GetOccupiedCells(Vector2I? skipCell = null)
    {
        var items = new List<PodInventory.Item>();
        foreach (KeyValuePair<Vector2I, ModuleDefinition> pair in _modules)
            if (skipCell == null || pair.Key != skipCell.Value) items.Add(new PodInventory.Item(pair.Key, PodInventory.SizeOf(pair.Value)));
        return items;
    }

    /// <summary>Top-left cell of the module covering cell, or null.</summary>
    public Vector2I? GetModuleCellAt(Vector2I cell) => PodInventory.ItemAt(GetOccupiedCells(), cell)?.Cell;

    /// <summary>
    /// True when the module can go at cell: a pod exists, the rectangle is inside the grid and on free cells (a module
    /// anchored exactly at cell is replaced), and the weight budget allows it.
    /// </summary>
    public bool CanInstallModule(string modelId, Vector2I cell)
    {
        if (Catalog?.GetDefinition(modelId) is not ModuleDefinition def || GetPod() == null) return false;
        ModuleDefinition old = _modules.TryGetValue(cell, out ModuleDefinition existing) ? existing : null;
        if (!PodInventory.Fits(InventorySize, GetOccupiedCells(old != null ? cell : null), cell, PodInventory.SizeOf(def))) return false;
        return def.BuildWeight - (old?.BuildWeight ?? 0) <= Budget.Remaining;
    }

    /// <summary>Installs a module with its top-left at cell (replacing a module anchored there). False when it does not fit.</summary>
    public bool InstallModule(string modelId, Vector2I cell)
    {
        if (!CanInstallModule(modelId, cell)) return false;
        var def = (ModuleDefinition)Catalog.GetDefinition(modelId);
        if (_modules.ContainsKey(cell)) RemoveModuleAnchoredAt(cell);
        _modules[cell] = def;
        Design.AddModule(modelId, cell);
        Budget.Add(def);
        if (DebugLog) GD.Print($"[Building] Installed {modelId} at ({cell.X}, {cell.Y}) ({Budget.CurrentWeight}/{Budget.MaxWeight})");
        EmitSignal(SignalName.ModuleChanged, cell);
        EmitSignal(SignalName.DesignChanged);
        return true;
    }

    /// <summary>Installs a module at the first free cell (row by row). False when it fits nowhere or the budget is short.</summary>
    public bool InstallModule(string modelId)
    {
        if (Catalog?.GetDefinition(modelId) is not ModuleDefinition def || GetPod() == null) return false;
        Vector2I? cell = PodInventory.FindFreeCell(InventorySize, GetOccupiedCells(), PodInventory.SizeOf(def));
        return cell != null && InstallModule(modelId, cell.Value);
    }

    /// <summary>Removes the module covering cell (any of its cells).</summary>
    public void RemoveModule(Vector2I cell)
    {
        Vector2I? anchor = GetModuleCellAt(cell);
        if (anchor != null) RemoveModuleAnchoredAt(anchor.Value);
    }

    private void RemoveModuleAnchoredAt(Vector2I anchor)
    {
        if (!_modules.TryGetValue(anchor, out ModuleDefinition def)) return;
        _modules.Remove(anchor);
        Design.ModuleEntries.RemoveAll(e => e.Cell == anchor);
        Budget.Remove(def);
        EmitSignal(SignalName.ModuleChanged, anchor);
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

    /// <summary>Aero forces and stability of the preview for the given flow (A9, P2). Modules count in the center of mass.</summary>
    public AeroReport AnalyzeAero(TestConditions conditions)
    {
        var parts = new List<RocketPart>(Grid.Parts);
        parts.AddRange(Grid.Decouplers);
        return new WindTunnelAnalyzer().Analyze(parts, conditions, GetPreviewMass().CenterOfMass);
    }

    /// <summary>Runs AnalyzeAero with the Wind tunnel exports, stores LastAeroReport and emits WindTunnelUpdated.</summary>
    public void ShowWindTunnel()
    {
        LastAeroReport = AnalyzeAero(new TestConditions
        {
            AltitudeM = WindTunnelAltitudeM,
            SpeedMps = WindTunnelSpeedMps,
            AngleOfAttackDeg = WindTunnelAngleDeg,
        });
        if (DebugLog)
            GD.Print($"[Building] Wind tunnel: drag {LastAeroReport.DragN:0} N, side {LastAeroReport.LiftN:0} N, stable {LastAeroReport.IsStable}");
        EmitSignal(SignalName.WindTunnelUpdated);
    }

    /// <summary>Writes the current design to user://current_design.json (also done by ConfirmDesign).</summary>
    public void SaveDesign() => Design.SaveCurrent();

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
            if (!IsUnlocked(e.ModelId) || !(e.Cell == PodInventory.Unplaced ? InstallModule(e.ModelId) : InstallModule(e.ModelId, e.Cell))) skipped++;
        if (skipped > 0) GD.PushWarning($"[Building] LoadDesign: {skipped} entries could not be restored");
        if (DebugLog)
            GD.Print($"[Building] Loaded design: {Grid.Parts.Count} parts, {Grid.Decouplers.Count} decouplers, {_modules.Count} modules");
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
        foreach (Vector2I anchor in new List<Vector2I>(_modules.Keys)) RemoveModuleAnchoredAt(anchor);
    }
}
