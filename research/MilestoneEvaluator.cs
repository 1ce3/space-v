using Godot;
using RocketGame.Core;

namespace RocketGame.Research;

/// <summary>Grants RP when a flight beats the best altitude or speed so far, then stores the new record.</summary>
public class MilestoneEvaluator
{
    /// <summary>Best altitude reached in any flight (m).</summary>
    public float BestAltitude { get; set; }
    /// <summary>Best speed reached in any flight (m/s).</summary>
    public float BestSpeed { get; set; }

    /// <summary>Meters above the old record per 1 RP.</summary>
    public float AltitudeMetersPerRP { get; set; } = 2000f;
    /// <summary>Cap of altitude record RP per flight.</summary>
    public int AltitudeRecordMaxRP { get; set; } = 40;
    /// <summary>m/s above the old record per 1 RP.</summary>
    public float SpeedMpsPerRP { get; set; } = 100f;
    /// <summary>Cap of speed record RP per flight.</summary>
    public int SpeedRecordMaxRP { get; set; } = 20;

    /// <summary>RP for beating BestAltitude, then updates BestAltitude.</summary>
    public int EvaluateAltRecords(FlightResult result)
    {
        int rp = RecordRP(result.MaxAltitude - BestAltitude, AltitudeMetersPerRP, AltitudeRecordMaxRP);
        if (result.MaxAltitude > BestAltitude) BestAltitude = result.MaxAltitude;
        return rp;
    }

    /// <summary>RP for beating BestSpeed, then updates BestSpeed.</summary>
    public int EvaluateSpeedRecords(FlightResult result)
    {
        int rp = RecordRP(result.MaxSpeed - BestSpeed, SpeedMpsPerRP, SpeedRecordMaxRP);
        if (result.MaxSpeed > BestSpeed) BestSpeed = result.MaxSpeed;
        return rp;
    }

    private static int RecordRP(float improvement, float unitsPerRP, int maxRP)
    {
        if (improvement <= 0f || unitsPerRP <= 0f) return 0;
        return Mathf.Min(maxRP, Mathf.FloorToInt(improvement / unitsPerRP));
    }
}
