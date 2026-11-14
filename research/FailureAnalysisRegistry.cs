using System.Collections.Generic;
using RocketGame.Core;

namespace RocketGame.Research;

/// <summary>Remembers which failure kinds the player has already seen and rewards new ones.</summary>
public class FailureAnalysisRegistry
{
    /// <summary>Keys "{FailureCause}:{PartGroup}" of every failure seen so far.</summary>
    public HashSet<string> DiscoveredFailures { get; } = new();

    /// <summary>RP for a failure kind never seen before.</summary>
    public int NoveltyRP { get; set; } = 15;

    /// <summary>Returns NoveltyRP for a failed flight with a new failure key and records the key; otherwise 0.</summary>
    public int CalculateNoveltyBonus(FlightResult result)
    {
        if (result.Outcome != MissionOutcome.Failed) return 0;
        return DiscoveredFailures.Add(MakeKey(result)) ? NoveltyRP : 0;
    }

    /// <summary>Novelty key of a flight result, for example "Thermal:HeatShield".</summary>
    public static string MakeKey(FlightResult result)
    {
        string group = result.FailedPartGroup >= 0 ? ((PartGroup)result.FailedPartGroup).ToString() : "None";
        return $"{result.FailureCause}:{group}";
    }
}
