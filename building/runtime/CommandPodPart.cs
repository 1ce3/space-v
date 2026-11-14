using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Command pod: root of the tree, holds modules and the robot payload.</summary>
public partial class CommandPodPart : RocketPart
{
    /// <summary>Installed modules.</summary>
    public List<PodModule> Modules { get; } = new();
    /// <summary>Robot payload mass (kg), from the mission.</summary>
    public float PayloadMassKg { get; set; }

    public CommandPodPartDefinition PodDefinition => Definition as CommandPodPartDefinition;

    /// <summary>Dry + payload + modules (kg).</summary>
    public override float GetCurrentMass()
    {
        float mass = DryMass + PayloadMassKg;
        foreach (PodModule module in Modules) mass += module.GetCurrentMass();
        return mass;
    }

    /// <summary>Installs a module (sets its Pod back-reference).</summary>
    public void AddModule(PodModule module)
    {
        if (module == null || Modules.Contains(module)) return;
        module.Pod = this;
        Modules.Add(module);
    }

    /// <summary>Size of the module grid (columns, rows).</summary>
    public Vector2I InventorySize => PodDefinition?.InventorySize ?? Vector2I.Zero;

    /// <summary>Rectangles taken by the installed modules.</summary>
    public List<PodInventory.Item> GetOccupiedCells(PodModule except = null)
    {
        var items = new List<PodInventory.Item>();
        foreach (PodModule m in Modules)
            if (m != except) items.Add(new PodInventory.Item(m.GridCell, PodInventory.SizeOf(m.Definition)));
        return items;
    }

    /// <summary>True when a module of this definition fits at cell (inside the grid, on free cells).</summary>
    public bool CanPlaceModule(ModuleDefinition def, Vector2I cell) =>
        def != null && PodInventory.Fits(InventorySize, GetOccupiedCells(), cell, PodInventory.SizeOf(def));

    /// <summary>Installed modules of type T.</summary>
    public List<T> GetModules<T>() where T : PodModule
    {
        var result = new List<T>();
        foreach (PodModule module in Modules)
            if (module is T typed) result.Add(typed);
        return result;
    }
}
