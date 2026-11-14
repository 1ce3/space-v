using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Inertial measurement unit: works everywhere, drifts over time.</summary>
public class GyroscopeImuModule : AttitudeSensorModule
{
    /// <summary>Drift accumulated since the last star-tracker fix (deg).</summary>
    public float AccumulatedDriftDeg { get; set; }

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        AccumulatedDriftDeg = 0f;
    }

    /// <summary>True attitude plus accumulated drift.</summary>
    public override float ReadPitch(float truePitchDeg) => truePitchDeg + AccumulatedDriftDeg;

    /// <summary>Drift rate (deg/min).</summary>
    public float DriftRateDegPerMin => AcsDefinition?.DriftRateDegPerMin ?? 0f;

    /// <summary>Clears accumulated drift.</summary>
    public void CalibrateWithStarTracker()
    {
        AccumulatedDriftDeg = 0f;
    }
}
