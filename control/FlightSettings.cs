using Godot;

namespace RocketGame.Control;

/// <summary>Tunable values shared by every Control node. Saved as data/settings/flight_settings.tres.</summary>
[GlobalClass]
public partial class FlightSettings : Resource
{
    /// <summary>How fast the throttle axis moves the throttle, per second (0..1 range).</summary>
    [ExportGroup("Throttle")]
    [Export(PropertyHint.Range, "0.05,5,0.05")] public float ThrottleRatePerSec { get; set; } = 0.5f;

    /// <summary>Proportional gain on the normalized attitude error.</summary>
    [ExportGroup("PID")]
    [Export(PropertyHint.Range, "0,10,0.01")] public float Kp { get; set; } = 2.0f;
    /// <summary>Integral gain on the normalized attitude error.</summary>
    [Export(PropertyHint.Range, "0,5,0.01")] public float Ki { get; set; } = 0.1f;
    /// <summary>Derivative gain on the normalized angular rate.</summary>
    [Export(PropertyHint.Range, "0,5,0.01")] public float Kd { get; set; } = 0.8f;
    /// <summary>Attitude error that maps to a full command.</summary>
    [Export(PropertyHint.Range, "0.1,90,0.1")] public float ErrorScaleDeg { get; set; } = 10f;
    /// <summary>Angular rate that maps to a full damping command.</summary>
    [Export(PropertyHint.Range, "0.1,90,0.1")] public float RateScaleDegPerSec { get; set; } = 10f;
    /// <summary>Clamp for the PID integral term (normalized units).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float IntegralLimit { get; set; } = 0.5f;

    /// <summary>How often CoM and inertia are recomputed while burning.</summary>
    [ExportGroup("Vehicle")]
    [Export(PropertyHint.Range, "1,60,1")] public float MassRecalcHz { get; set; } = 10f;

    /// <summary>Star trackers are blinded below this altitude.</summary>
    [ExportGroup("Sensors")]
    [Export] public float StarTrackerMinAltitudeM { get; set; } = 30000f;

    /// <summary>Simulated cruise days per real second at x1 speed.</summary>
    [ExportGroup("Cruise")]
    [Export(PropertyHint.Range, "0.1,50,0.1")] public float CruiseDaysPerSecond { get; set; } = 3.5f;

    [ExportGroup("Camera")]
    [Export] public float CameraMinDistanceM { get; set; } = 30f;
    [Export] public float CameraMaxDistanceM { get; set; } = 400f;

    /// <summary>BodyId that uses EarthSkyColor.</summary>
    [ExportGroup("Sky")]
    [Export] public string EarthBodyId { get; set; } = "earth_scaled";
    [Export] public Color EarthSkyColor { get; set; } = new(0.45f, 0.65f, 0.95f);
    /// <summary>BodyId that uses MarsSkyColor.</summary>
    [Export] public string MarsBodyId { get; set; } = "mars_scaled";
    [Export] public Color MarsSkyColor { get; set; } = new(0.85f, 0.6f, 0.45f);
    /// <summary>Sky color in vacuum.</summary>
    [Export] public Color SpaceSkyColor { get; set; } = new(0.01f, 0.01f, 0.03f);

    /// <summary>Sea-level sky color for a body, or SpaceSkyColor when the BodyId is unknown.</summary>
    public Color GetSkyColor(string bodyId)
    {
        if (bodyId == EarthBodyId) return EarthSkyColor;
        if (bodyId == MarsBodyId) return MarsSkyColor;
        return SpaceSkyColor;
    }
}
