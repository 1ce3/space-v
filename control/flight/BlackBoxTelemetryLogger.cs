using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>Flight recorder for one MissionRun: keeps records and the first failure, then builds the FlightResult.</summary>
public class BlackBoxTelemetryLogger
{
    public string MissionId { get; set; } = "";
    public float MaxAltitude { get; private set; }
    public float MaxSpeed { get; private set; }
    /// <summary>Highest dynamic pressure seen (Pa).</summary>
    public float MaxQ { get; private set; }
    public float MaxG { get; private set; }
    /// <summary>Total flown time over the direct-control segments (s).</summary>
    public float FlightTime { get; private set; }
    public float CruiseDaysSurvived { get; set; }
    public float LandingSpeedMps { get; set; }
    public FailureCause FailureCause { get; private set; } = FailureCause.None;
    public string FailedModelId { get; private set; } = "";
    /// <summary>PartGroup of the failed part as int, or -1.</summary>
    public int FailedPartGroup { get; private set; } = -1;
    public int SegmentsCompleted { get; set; }

    /// <summary>Updates the records from one tick. deltaTime is the tick length (s).</summary>
    public void LogFlightMetrics(TelemetryFrame frame, float deltaTime = 0f)
    {
        MaxAltitude = Mathf.Max(MaxAltitude, frame.AltitudeM);
        MaxSpeed = Mathf.Max(MaxSpeed, frame.SpeedMps);
        MaxQ = Mathf.Max(MaxQ, frame.DynamicPressure);
        MaxG = Mathf.Max(MaxG, frame.GForce);
        FlightTime += deltaTime;
    }

    /// <summary>Called for every cut. Only a Break is recorded as a failure.</summary>
    public void OnPartSevered(RocketPart part, SeverCause cause)
    {
        if (cause != SeverCause.Break) return;
        // The FailureCause itself comes from FailureDetector.BreakTriggered via RecordFailure.
    }

    /// <summary>Records the first failure of the run; later calls are ignored.</summary>
    public void RecordFailure(FailureCause cause, RocketPart part)
    {
        if (FailureCause != FailureCause.None || cause == FailureCause.None) return;
        FailureCause = cause;
        if (part?.Definition == null) return;
        FailedModelId = part.Definition.ModelId ?? "";
        FailedPartGroup = (int)part.Definition.PartGroup;
    }

    /// <summary>Builds the single result of the run.</summary>
    public FlightResult BuildResult(MissionOutcome outcome)
    {
        return new FlightResult
        {
            MissionId = MissionId,
            Outcome = outcome,
            SegmentsCompleted = SegmentsCompleted,
            MaxAltitude = MaxAltitude,
            MaxSpeed = MaxSpeed,
            MaxQ = MaxQ,
            MaxG = MaxG,
            FlightTime = FlightTime,
            CruiseDaysSurvived = CruiseDaysSurvived,
            LandingSpeedMps = LandingSpeedMps,
            FailureCause = FailureCause,
            FailedModelId = FailedModelId,
            FailedPartGroup = FailedPartGroup,
        };
    }
}
