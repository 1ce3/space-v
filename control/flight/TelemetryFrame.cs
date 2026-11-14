using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>Snapshot of the vehicle after one physics tick. Plain struct: passed by call, never through signals.</summary>
public struct TelemetryFrame
{
    /// <summary>Altitude of the CoM above the surface.</summary>
    public float AltitudeM;
    /// <summary>Altitude of the lowest part corner above the surface (landing / ground contact).</summary>
    public float RadarAltitudeM;
    public float SpeedMps;
    public float VerticalSpeedMps;
    public float HorizontalSpeedMps;
    /// <summary>Body attitude vs local horizon (FlightMath.AttitudeDeg): 90 = nose up.</summary>
    public float PitchDeg;
    /// <summary>Angular rate about +Z (positive = AttitudeDeg increasing).</summary>
    public float AngularRateDegPerSec;
    /// <summary>Velocity direction vs local horizon (FlightMath.FlightPathAngleDeg).</summary>
    public float FlightPathAngleDeg;
    /// <summary>Throttle command 0..1.</summary>
    public float Throttle;
    /// <summary>Fuel left in the active stage's feed tanks, 0..1.</summary>
    public float StageFuelFraction;
    /// <summary>Dynamic pressure q (Pa).</summary>
    public float DynamicPressure;
    public float GForce;
    public float MachNumber;
    /// <summary>Highest TempK / MaxSafeTempK over all components.</summary>
    public float MaxTempRatio;
    /// <summary>Highest structural load ratio this tick.</summary>
    public float MaxStressRatio;
    /// <summary>Stagnation heat flux (W/m^2).</summary>
    public float HeatFlux;
    /// <summary>Heat shield ablator left, 0..1 (1 when there is no heat shield).</summary>
    public float AblatorFraction;
    public int CurrentStageIndex;
    public FlightPhase Phase;
    public float ElapsedDays;
}
