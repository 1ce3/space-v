namespace RocketGame.Research;

/// <summary>RP granted for one finished flight, split by source. Shown on the debrief screen.</summary>
public class RewardBreakdown
{
    /// <summary>Sum of RewardRP of the segments completed in this flight.</summary>
    public int SegmentRP { get; set; }
    /// <summary>RP for new altitude and speed records.</summary>
    public int MilestoneRP { get; set; }
    /// <summary>RP for a failure kind never seen before.</summary>
    public int NoveltyRP { get; set; }
    /// <summary>SegmentRP + MilestoneRP + NoveltyRP.</summary>
    public int Total => SegmentRP + MilestoneRP + NoveltyRP;

    public override string ToString() => $"Segment {SegmentRP} + Milestone {MilestoneRP} + Novelty {NoveltyRP} = {Total}";
}
