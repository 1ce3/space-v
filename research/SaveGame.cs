using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace RocketGame.Research;

/// <summary>Campaign progress stored as JSON (default user://save.json). Does not contain the rocket design.</summary>
public class SaveGame
{
    /// <summary>File the save is written to.</summary>
    public string FilePath { get; set; } = "user://save.json";

    /// <summary>Research points the player owns.</summary>
    public int CurrentRP { get; set; }
    /// <summary>Purchased tech NodeIds.</summary>
    public HashSet<string> UnlockedNodes { get; set; } = new();
    /// <summary>Failure keys already rewarded.</summary>
    public HashSet<string> DiscoveredFailures { get; set; } = new();
    /// <summary>SegmentIds completed at least once.</summary>
    public HashSet<string> CompletedSegments { get; set; } = new();
    /// <summary>True after a successful Mars landing.</summary>
    public bool IsCampaignComplete { get; set; }
    /// <summary>Best altitude record (m).</summary>
    public float BestAltitude { get; set; }
    /// <summary>Best speed record (m/s).</summary>
    public float BestSpeed { get; set; }

    /// <summary>Writes the save file. Returns false and logs an error on failure.</summary>
    public bool Save()
    {
        string json = JsonSerializer.Serialize(new SaveData
        {
            CurrentRP = CurrentRP,
            UnlockedNodes = new List<string>(UnlockedNodes),
            DiscoveredFailures = new List<string>(DiscoveredFailures),
            CompletedSegments = new List<string>(CompletedSegments),
            IsCampaignComplete = IsCampaignComplete,
            BestAltitude = BestAltitude,
            BestSpeed = BestSpeed,
        });
        using FileAccess file = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError($"[Research] Cannot write save file {FilePath}: {FileAccess.GetOpenError()}");
            return false;
        }
        file.StoreString(json);
        return true;
    }

    /// <summary>Reads the save file. Missing file keeps defaults and returns false; a broken file logs an error.</summary>
    public bool Load()
    {
        if (!FileAccess.FileExists(FilePath)) return false;
        using FileAccess file = FileAccess.Open(FilePath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"[Research] Cannot read save file {FilePath}: {FileAccess.GetOpenError()}");
            return false;
        }

        SaveData data;
        try
        {
            data = JsonSerializer.Deserialize<SaveData>(file.GetAsText());
        }
        catch (JsonException e)
        {
            GD.PushError($"[Research] Save file {FilePath} is broken, starting fresh: {e.Message}");
            return false;
        }
        if (data == null) return false;

        CurrentRP = data.CurrentRP;
        UnlockedNodes = new HashSet<string>(data.UnlockedNodes ?? new List<string>());
        DiscoveredFailures = new HashSet<string>(data.DiscoveredFailures ?? new List<string>());
        CompletedSegments = new HashSet<string>(data.CompletedSegments ?? new List<string>());
        IsCampaignComplete = data.IsCampaignComplete;
        BestAltitude = data.BestAltitude;
        BestSpeed = data.BestSpeed;
        return true;
    }

    private class SaveData
    {
        public int CurrentRP { get; set; }
        public List<string> UnlockedNodes { get; set; }
        public List<string> DiscoveredFailures { get; set; }
        public List<string> CompletedSegments { get; set; }
        public bool IsCampaignComplete { get; set; }
        public float BestAltitude { get; set; }
        public float BestSpeed { get; set; }
    }
}
