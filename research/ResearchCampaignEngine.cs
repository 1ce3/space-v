using Godot;
using RocketGame.Core;

namespace RocketGame.Research;

/// <summary>Autoload that owns the campaign: RP, records, failures seen, the mission and the tech tree. Root of research_campaign_engine.tscn.</summary>
[GlobalClass]
public partial class ResearchCampaignEngine : Node
{
    /// <summary>CurrentRP changed.</summary>
    [Signal] public delegate void RPChangedEventHandler(int newValue);

    /// <summary>The autoload instance.</summary>
    public static ResearchCampaignEngine Instance { get; private set; }

    /// <summary>Child TechTreeManager.</summary>
    [Export] public TechTreeManager TechTree { get; set; }
    /// <summary>The campaign mission (mars_mission.tres).</summary>
    [Export] public MissionDefinition Mission { get; set; }

    /// <summary>Meters above the altitude record per 1 RP.</summary>
    [ExportGroup("Rewards")]
    [Export] public float AltitudeMetersPerRP { get; set; } = 2000f;
    /// <summary>Cap of altitude record RP per flight.</summary>
    [Export] public int AltitudeRecordMaxRP { get; set; } = 40;
    /// <summary>m/s above the speed record per 1 RP.</summary>
    [Export] public float SpeedMpsPerRP { get; set; } = 100f;
    /// <summary>Cap of speed record RP per flight.</summary>
    [Export] public int SpeedRecordMaxRP { get; set; } = 20;
    /// <summary>RP for a failure kind never seen before.</summary>
    [Export] public int NoveltyRP { get; set; } = 15;

    [ExportGroup("Balance")]
    /// <summary>Multiplies every reward (segments, records, novelty, altitude marks). 1 = original balance.</summary>
    [Export(PropertyHint.Range, "0.1,5,0.1")] public float RewardMultiplier { get; set; } = 2f;
    /// <summary>One-time bonuses the first time a flight passes these altitudes (km).</summary>
    [Export] public float[] AltitudeMarksKm { get; set; } = { 10f, 30f, 50f, 70f };
    [Export] public int AltitudeMarkRP { get; set; } = 10;
    /// <summary>Flights above MinRewardAltitudeM give at least this much RP, so stuck players still progress.</summary>
    [Export] public int MinRewardRP { get; set; } = 5;
    /// <summary>The minimum reward only applies to flights that reached this altitude (m): no farming by aborting on the pad.</summary>
    [Export] public float MinRewardAltitudeM { get; set; } = 30000f;

    /// <summary>Save file path.</summary>
    [ExportGroup("Save")]
    [Export] public string SavePath { get; set; } = "user://save.json";
    /// <summary>Load the save file when the game starts.</summary>
    [Export] public bool LoadSaveOnStart { get; set; } = true;

    /// <summary>Research points the player owns.</summary>
    public int CurrentRP { get; private set; }
    /// <summary>The mission being played.</summary>
    public MissionDefinition CurrentMission => Mission;

    private readonly MilestoneEvaluator _milestones = new();
    private readonly FailureAnalysisRegistry _failures = new();
    private SaveGame _save = new();
    private bool _isCampaignComplete;

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, TechTree, nameof(TechTree))) return;
        if (!NodeGuard.Require(this, Mission, nameof(Mission))) return;
        ApplyRewardSettings();
        _save = new SaveGame { FilePath = SavePath };
        if (LoadSaveOnStart && _save.Load()) RestoreFromSave();
        GD.Print($"[Research] Campaign ready: {CurrentRP} RP, {TechTree.UnlockedNodes.Count} nodes bought, max weight {TechTree.GetMaxBuildWeight()}");
    }

    /// <summary>True after a successful Mars landing.</summary>
    public bool IsCampaignComplete() => _isCampaignComplete;

    /// <summary>Segment of the current mission by index, or null.</summary>
    public MissionSegmentDefinition GetSegment(int segmentIndex)
    {
        if (Mission == null || segmentIndex < 0 || segmentIndex >= Mission.Segments.Count)
        {
            GD.PushError($"[Research] GetSegment({segmentIndex}) is out of range.");
            return null;
        }
        return Mission.Segments[segmentIndex];
    }

    private int Scaled(int rp) => Mathf.RoundToInt(rp * RewardMultiplier);

    /// <summary>Turns a finished flight into RP, updates records and failures, then saves.</summary>
    public RewardBreakdown ProcessMissionEnd(FlightResult result)
    {
        var reward = new RewardBreakdown();
        if (result == null)
        {
            GD.PushError("[Research] ProcessMissionEnd got a null FlightResult.");
            return reward;
        }

        // Altitude marks: first time past each mark (uses the best altitude before this flight).
        int marks = 0;
        foreach (float km in AltitudeMarksKm ?? System.Array.Empty<float>())
            if (_milestones.BestAltitude < km * 1000f && result.MaxAltitude >= km * 1000f) marks += AltitudeMarkRP;

        reward.SegmentRP = Scaled(CompletedSegmentRP(result.SegmentsCompleted));
        reward.MilestoneRP = Scaled(_milestones.EvaluateAltRecords(result) + _milestones.EvaluateSpeedRecords(result) + marks);
        reward.NoveltyRP = Scaled(_failures.CalculateNoveltyBonus(result));
        if (result.MaxAltitude >= MinRewardAltitudeM && reward.Total < MinRewardRP) reward.MilestoneRP += MinRewardRP - reward.Total;
        if (result.Outcome == MissionOutcome.Success) _isCampaignComplete = true;

        AddRP(reward.Total);
        GD.Print($"[Research] Mission end ({result.Outcome}, {result.FailureCause}): {reward}");
        SaveProgress();
        return reward;
    }

    /// <summary>Removes RP if the player has enough. Returns false otherwise.</summary>
    public bool SpendRP(int amount)
    {
        if (amount < 0 || CurrentRP < amount) return false;
        CurrentRP -= amount;
        EmitSignal(SignalName.RPChanged, CurrentRP);
        return true;
    }

    /// <summary>Adds RP (rewards and debug). Negative amounts are ignored.</summary>
    public void AddRP(int amount)
    {
        if (amount <= 0) return;
        CurrentRP += amount;
        EmitSignal(SignalName.RPChanged, CurrentRP);
    }

    /// <summary>Writes the current campaign state to the save file.</summary>
    public void SaveProgress()
    {
        _save.CurrentRP = CurrentRP;
        _save.UnlockedNodes = new(TechTree.UnlockedNodes);
        _save.DiscoveredFailures = new(_failures.DiscoveredFailures);
        _save.IsCampaignComplete = _isCampaignComplete;
        _save.BestAltitude = _milestones.BestAltitude;
        _save.BestSpeed = _milestones.BestSpeed;
        _save.Save();
    }

    /// <summary>Debug: wipes all progress and overwrites the save file.</summary>
    public void ResetSave()
    {
        CurrentRP = 0;
        _isCampaignComplete = false;
        _milestones.BestAltitude = 0f;
        _milestones.BestSpeed = 0f;
        _failures.DiscoveredFailures.Clear();
        TechTree.LoadUnlockedNodes(System.Array.Empty<string>());
        _save = new SaveGame { FilePath = SavePath };
        _save.Save();
        EmitSignal(SignalName.RPChanged, CurrentRP);
        GD.Print("[Research] Save reset");
    }

    private int CompletedSegmentRP(int segmentsCompleted)
    {
        int rp = 0;
        int count = Mathf.Min(segmentsCompleted, Mission.Segments.Count);
        for (int i = 0; i < count; i++)
        {
            MissionSegmentDefinition segment = Mission.Segments[i];
            rp += segment.RewardRP;
            _save.CompletedSegments.Add(segment.SegmentId);
        }
        return rp;
    }

    private void ApplyRewardSettings()
    {
        _milestones.AltitudeMetersPerRP = AltitudeMetersPerRP;
        _milestones.AltitudeRecordMaxRP = AltitudeRecordMaxRP;
        _milestones.SpeedMpsPerRP = SpeedMpsPerRP;
        _milestones.SpeedRecordMaxRP = SpeedRecordMaxRP;
        _failures.NoveltyRP = NoveltyRP;
    }

    private void RestoreFromSave()
    {
        CurrentRP = _save.CurrentRP;
        _isCampaignComplete = _save.IsCampaignComplete;
        _milestones.BestAltitude = _save.BestAltitude;
        _milestones.BestSpeed = _save.BestSpeed;
        _failures.DiscoveredFailures.Clear();
        _failures.DiscoveredFailures.UnionWith(_save.DiscoveredFailures);
        TechTree.LoadUnlockedNodes(_save.UnlockedNodes);
    }
}
