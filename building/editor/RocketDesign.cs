using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>What the player placed: parts on cells, decouplers on edges, modules in slots. Saved as JSON.</summary>
public class RocketDesign
{
    /// <summary>Where the last design is auto-saved (decision #14).</summary>
    public const string SavePath = "user://current_design.json";

    /// <summary>A part on the grid.</summary>
    public class PartEntry
    {
        public string ModelId { get; set; } = "";
        /// <summary>Lower-left cell of the footprint.</summary>
        public Vector2I Cell { get; set; }
    }

    /// <summary>A decoupler on a cell edge.</summary>
    public class DecouplerEntry
    {
        public string ModelId { get; set; } = "";
        public Edge Edge { get; set; }
    }

    /// <summary>A module in a pod slot.</summary>
    public class ModuleEntry
    {
        public string ModelId { get; set; } = "";
        public ModuleSlotType SlotType { get; set; }
        /// <summary>Index within SlotType.</summary>
        public int SlotIndex { get; set; }
    }

    public List<PartEntry> PartEntries { get; } = new();
    public List<DecouplerEntry> DecouplerEntries { get; } = new();
    public List<ModuleEntry> ModuleEntries { get; } = new();

    /// <summary>JSON text of this design.</summary>
    public string Serialize()
    {
        // TODO(A3)
        return "{}";
    }

    /// <summary>Design from JSON text; empty design when the text is invalid.</summary>
    public static RocketDesign Deserialize(string json)
    {
        // TODO(A3)
        return new RocketDesign();
    }

    /// <summary>Writes this design to SavePath.</summary>
    public void SaveCurrent()
    {
        // TODO(A3)
    }

    /// <summary>Design from SavePath, or null when there is none.</summary>
    public static RocketDesign LoadLast()
    {
        // TODO(A3)
        return null;
    }
}
