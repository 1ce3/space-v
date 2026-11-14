using Godot;

namespace RocketGame.Core;

/// <summary>A cell edge: the cell and which of its sides.</summary>
public readonly record struct Edge(Vector2I Cell, Side Side);

/// <summary>Mass properties in the vehicle-local frame (meters, kg, kg*m^2).</summary>
public struct MassProperties
{
    public float TotalMass;
    public Vector2 CenterOfMass;
    public float MomentOfInertia;
}

/// <summary>Air state at one altitude.</summary>
public struct AtmosphereSample
{
    public float Density;       // kg/m^3
    public float Pressure;      // Pa
    public float Temperature;   // K
    public float SpeedOfSound;  // m/s
    public static AtmosphereSample Vacuum => new() { Density = 0f, Pressure = 0f, Temperature = 3f, SpeedOfSound = 1f };
}

/// <summary>Something physics detected that control may need to react to.</summary>
public struct FlightEvent
{
    public FlightEventType Type;
    public float Severity;      // 0..1
    public string SourceId;     // ModelId of the part/module involved, or empty
}

/// <summary>The single result of one whole flight attempt (all segments). RefCounted so it can travel through signals.</summary>
[GlobalClass]
public partial class FlightResult : RefCounted
{
    public string MissionId { get; set; } = "";
    public MissionOutcome Outcome { get; set; }
    public int SegmentsCompleted { get; set; }
    public float MaxAltitude { get; set; }
    public float MaxSpeed { get; set; }
    public float MaxQ { get; set; }
    public float MaxG { get; set; }
    public float FlightTime { get; set; }
    public float CruiseDaysSurvived { get; set; }
    public float LandingSpeedMps { get; set; }
    public FailureCause FailureCause { get; set; }
    public string FailedModelId { get; set; } = "";
    /// <summary>PartGroup of the failed part, or -1. Used for the novelty key.</summary>
    public int FailedPartGroup { get; set; } = -1;
}
