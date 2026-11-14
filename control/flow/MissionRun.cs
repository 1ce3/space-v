using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Control;

/// <summary>One attempt at the whole mission (all 3 segments). RefCounted so it can travel through signals.</summary>
[GlobalClass]
public partial class MissionRun : RefCounted
{
    /// <summary>The mission being flown.</summary>
    public MissionDefinition Mission { get; private set; }
    /// <summary>The rocket confirmed in the build screen.</summary>
    public AssembledRocket Rocket { get; private set; }
    /// <summary>Index into Mission.Segments of the segment being played.</summary>
    public int SegmentIndex { get; private set; }
    /// <summary>Number of segments finished successfully.</summary>
    public int SegmentsCompleted { get; private set; }
    /// <summary>Stage index, kept across segments (mirrors StagingManager.CurrentStageIndex).</summary>
    public int StageIndex { get; set; }
    /// <summary>Simulated days spent in cruise.</summary>
    public float ElapsedDays { get; set; }
    /// <summary>Flight recorder for the whole run.</summary>
    public BlackBoxTelemetryLogger BlackBox { get; private set; } = new();

    /// <summary>Part tree of the rocket.</summary>
    public PartTree PartTree => Rocket?.PartTree;
    /// <summary>Stages of the rocket.</summary>
    public List<StageGroup> Stages => Rocket?.Stages;

    /// <summary>Definition of the segment being played, or null when past the last one.</summary>
    public MissionSegmentDefinition CurrentSegment =>
        Mission != null && SegmentIndex >= 0 && SegmentIndex < Mission.Segments.Count ? Mission.Segments[SegmentIndex] : null;

    /// <summary>Kind of the segment being played (EarthAscent when unknown).</summary>
    public SegmentKind CurrentKind => CurrentSegment?.Kind ?? SegmentKind.EarthAscent;

    /// <summary>True when every segment has been completed.</summary>
    public bool IsFinished => Mission != null && SegmentIndex >= Mission.Segments.Count;

    /// <summary>Godot needs a parameterless constructor; use the other one in code.</summary>
    public MissionRun() { }

    public MissionRun(MissionDefinition mission, AssembledRocket rocket)
    {
        Mission = mission;
        Rocket = rocket;
        BlackBox.MissionId = mission?.MissionId ?? "";
    }

    /// <summary>Marks the current segment completed and moves to the next one.</summary>
    public void Advance()
    {
        SegmentsCompleted++;
        SegmentIndex++;
        BlackBox.SegmentsCompleted = SegmentsCompleted;
    }
}
