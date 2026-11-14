using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using RocketGame.Core;
using Side = RocketGame.Core.Side;

namespace RocketGame.Building;

/// <summary>What the player placed: parts on cells, decouplers on edges, modules in slots. Saved as JSON.</summary>
public class RocketDesign
{
    /// <summary>Where the last design is auto-saved (decision #14).</summary>
    public const string SavePath = "user://current_design.json";
    /// <summary>Format version written in the JSON.</summary>
    /// <summary>2 = modules stored by inventory cell (phase 2). Version 1 files load with every module Unplaced.</summary>
    public const int FormatVersion = 2;

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

    /// <summary>A module in the pod inventory grid.</summary>
    public class ModuleEntry
    {
        public string ModelId { get; set; } = "";
        /// <summary>Top-left cell, or PodInventory.Unplaced (first free cell when built).</summary>
        public Vector2I Cell { get; set; } = PodInventory.Unplaced;
    }

    public List<PartEntry> PartEntries { get; } = new();
    public List<DecouplerEntry> DecouplerEntries { get; } = new();
    public List<ModuleEntry> ModuleEntries { get; } = new();

    public void AddPart(string modelId, Vector2I cell) =>
        PartEntries.Add(new PartEntry { ModelId = modelId, Cell = cell });

    /// <summary>Adds a decoupler entry; the edge is stored normalized.</summary>
    public void AddDecoupler(string modelId, Edge edge) =>
        DecouplerEntries.Add(new DecouplerEntry { ModelId = modelId, Edge = BuildGrid.Normalize(edge) });

    public void AddModule(string modelId, Vector2I cell) =>
        ModuleEntries.Add(new ModuleEntry { ModelId = modelId, Cell = cell });

    /// <summary>Removes every entry.</summary>
    public void Clear()
    {
        PartEntries.Clear();
        DecouplerEntries.Clear();
        ModuleEntries.Clear();
    }

    public bool IsEmpty => PartEntries.Count == 0 && DecouplerEntries.Count == 0 && ModuleEntries.Count == 0;

    /// <summary>JSON text of this design.</summary>
    public string Serialize()
    {
        var dto = new DesignDto { Version = FormatVersion };
        foreach (PartEntry p in PartEntries)
            dto.Parts.Add(new PartDto { Id = p.ModelId, X = p.Cell.X, Y = p.Cell.Y });
        foreach (DecouplerEntry d in DecouplerEntries)
            dto.Decouplers.Add(new DecouplerDto { Id = d.ModelId, X = d.Edge.Cell.X, Y = d.Edge.Cell.Y, Side = d.Edge.Side });
        foreach (ModuleEntry m in ModuleEntries)
            dto.Modules.Add(new ModuleDto { Id = m.ModelId, X = m.Cell.X, Y = m.Cell.Y });
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>Design from JSON text; empty design when the text is invalid.</summary>
    public static RocketDesign Deserialize(string json)
    {
        var design = new RocketDesign();
        if (string.IsNullOrWhiteSpace(json)) return design;

        DesignDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<DesignDto>(json, JsonOptions);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or ArgumentException)
        {
            GD.PushWarning($"[Building] RocketDesign.Deserialize: invalid JSON ({e.Message})");
            return design;
        }
        if (dto == null) return design;

        foreach (PartDto p in dto.Parts ?? new List<PartDto>())
            if (!string.IsNullOrEmpty(p?.Id)) design.AddPart(p.Id, new Vector2I(p.X, p.Y));
        foreach (DecouplerDto d in dto.Decouplers ?? new List<DecouplerDto>())
            if (!string.IsNullOrEmpty(d?.Id)) design.AddDecoupler(d.Id, new Edge(new Vector2I(d.X, d.Y), d.Side));
        foreach (ModuleDto m in dto.Modules ?? new List<ModuleDto>())
            if (!string.IsNullOrEmpty(m?.Id))
                design.AddModule(m.Id, dto.Version >= 2 && m.X.HasValue && m.Y.HasValue ? new Vector2I(m.X.Value, m.Y.Value) : PodInventory.Unplaced);
        return design;
    }

    /// <summary>Writes this design to SavePath.</summary>
    public void SaveCurrent() => SaveTo(SavePath);

    /// <summary>Design from SavePath, or null when there is none.</summary>
    public static RocketDesign LoadLast() => LoadFrom(SavePath);

    /// <summary>Writes this design to a file (user:// path). False on error.</summary>
    public bool SaveTo(string path)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError($"[Building] RocketDesign: cannot write {path} ({FileAccess.GetOpenError()})");
            return false;
        }
        file.StoreString(Serialize());
        return true;
    }

    /// <summary>Design from a file, or null when the file does not exist.</summary>
    public static RocketDesign LoadFrom(string path)
    {
        if (!FileAccess.FileExists(path)) return null;
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushWarning($"[Building] RocketDesign: cannot read {path} ({FileAccess.GetOpenError()})");
            return null;
        }
        return Deserialize(file.GetAsText());
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // JSON shape (Vector2I and Edge are not JSON-friendly, so the file uses plain numbers).
    private class DesignDto
    {
        public int Version { get; set; }
        public List<PartDto> Parts { get; set; } = new();
        public List<DecouplerDto> Decouplers { get; set; } = new();
        public List<ModuleDto> Modules { get; set; } = new();
    }

    private class PartDto
    {
        public string Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    private class DecouplerDto
    {
        public string Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public Side Side { get; set; }
    }

    private class ModuleDto
    {
        public string Id { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
        /// <summary>Version 1 only (slot type name and index); read and ignored, never written.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Slot { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Index { get; set; }
    }
}
