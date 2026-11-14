using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>
/// Build screen logic: place/remove parts, decouplers and modules within the budget, validate,
/// and emit DesignConfirmed. Never calls Control.
/// </summary>
public partial class BuildingSystem : Node
{
    [Signal] public delegate void PartPlacedEventHandler(RocketPart part);
    [Signal] public delegate void PartRemovedEventHandler(RocketPart part);
    [Signal] public delegate void PartMovedEventHandler(RocketPart part);
    [Signal] public delegate void ModuleChangedEventHandler(ModuleSlotType slotType, int slotIndex);
    [Signal] public delegate void DesignConfirmedEventHandler(AssembledRocket rocket);

    /// <summary>Parent node for the preview parts shown on the grid.</summary>
    [Export] public Node3D PartsRoot { get; set; }
    [Export] public bool DebugLog { get; set; }

    public BuildGrid Grid { get; private set; }
    public BuildBudget Budget { get; private set; }
    /// <summary>The PartCatalog autoload.</summary>
    public PartCatalog Catalog => PartCatalog.Instance;
    public RocketDesign Design { get; private set; } = new();

    public override void _Ready()
    {
        BuildSettings settings = Catalog?.Settings;
        Grid = new BuildGrid(settings?.GridWidth ?? 9, settings?.GridHeight ?? 16, settings?.CellSize ?? BuildSettings.DefaultCellSize);
        Budget = new BuildBudget();
        // TODO(A6): Budget.SetMax(ResearchCampaignEngine.Instance.TechTree.GetMaxBuildWeight()); LoadSavedDesign().
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

    public bool PlacePart(string modelId, Vector2I cell)
    {
        // TODO(A6)
        return false;
    }

    public void RemovePart(Vector2I cell)
    {
        // TODO(A6): also removes orphan decouplers and refunds weight (A7).
    }

    public bool MovePart(Vector2I from, Vector2I to)
    {
        // TODO(A9)
        return false;
    }

    public bool PlaceDecoupler(string modelId, Edge edge)
    {
        // TODO(A6)
        return false;
    }

    public void RemoveDecoupler(Edge edge)
    {
        // TODO(A6)
    }

    /// <summary>Installs a module; its slot type comes from the definition, slotIndex is within that type.</summary>
    public bool InstallModule(string modelId, int slotIndex)
    {
        // TODO(A7)
        return false;
    }

    public void RemoveModule(ModuleSlotType slotType, int slotIndex)
    {
        // TODO(A7)
    }

    /// <summary>Player-facing errors of the current design.</summary>
    public List<string> Validate() => new RocketAssembler().ValidateConnections(Design);

    /// <summary>Validates, assembles a fresh rocket, saves the design and emits DesignConfirmed.</summary>
    public void ConfirmDesign()
    {
        // TODO(A6)
    }

    public void ShowWindTunnel()
    {
        // TODO(A9, P2)
    }

    /// <summary>Restores user://current_design.json onto the grid.</summary>
    public void LoadSavedDesign()
    {
        // TODO(A7)
    }
}
