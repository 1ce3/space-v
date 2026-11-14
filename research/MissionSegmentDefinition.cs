using Godot;
using RocketGame.Core;
using RocketGame.Physics;

namespace RocketGame.Research;

/// <summary>One segment of a mission. Each Kind uses its own group of fields; the others are ignored.</summary>
[GlobalClass]
public partial class MissionSegmentDefinition : Resource
{
    /// <summary>Snake_case id, same as the .tres file name.</summary>
    [Export] public string SegmentId { get; set; } = "";
    /// <summary>Name shown to the player.</summary>
    [Export] public string DisplayName { get; set; } = "";
    /// <summary>How this segment is played.</summary>
    [Export] public SegmentKind Kind { get; set; }
    /// <summary>Body of this segment. For Cruise this is the origin body.</summary>
    [Export] public CelestialBodyDefinition Body { get; set; }
    /// <summary>RP granted when this segment is completed.</summary>
    [Export(PropertyHint.Range, "0,1000,1")] public int RewardRP { get; set; }
    /// <summary>Hazards that may happen in this segment (P1).</summary>
    [Export] public FlightEventMask EnabledHazards { get; set; }

    /// <summary>EarthAscent: altitude to reach (m).</summary>
    [ExportGroup("EarthAscent")]
    [Export] public float TargetAltitudeM { get; set; }
    /// <summary>EarthAscent: horizontal speed to reach (m/s).</summary>
    [Export] public float TargetSpeedMps { get; set; }
    /// <summary>EarthAscent: stormy weather enables lightning and wind shear (P2).</summary>
    [Export] public bool IsStormy { get; set; }
    /// <summary>EarthAscent: peak wind shear force (N) (P2).</summary>
    [Export] public float WindShearForceX { get; set; }

    /// <summary>Cruise: delta-v needed for the transfer burn (m/s).</summary>
    [ExportGroup("Cruise")]
    [Export] public float RequiredDeltaVMps { get; set; }
    /// <summary>Cruise: length of the trip (days).</summary>
    [Export] public float DurationDays { get; set; }

    /// <summary>MarsEdl: altitude where the vehicle is placed at entry (m).</summary>
    [ExportGroup("MarsEdl")]
    [Export] public float EntryAltitudeM { get; set; }
    /// <summary>MarsEdl: speed at entry (m/s).</summary>
    [Export] public float EntrySpeedMps { get; set; }
    /// <summary>MarsEdl: flight path angle at entry (deg, negative = descending).</summary>
    [Export(PropertyHint.Range, "-90,0,0.5")] public float EntryAngleDeg { get; set; }
    /// <summary>MarsEdl: largest allowed tilt from vertical at touchdown (deg).</summary>
    [Export(PropertyHint.Range, "0,90,0.5")] public float MaxLandingTiltDeg { get; set; }
}
