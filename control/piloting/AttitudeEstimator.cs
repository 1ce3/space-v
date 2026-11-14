using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>
/// Attitude as the avionics see it: true attitude + IMU drift + noise, low-pass filtered (C_CONTROL.md 5.3).
/// With no working sensor, HasValidAttitude is false and the last estimate is held.
/// </summary>
public partial class AttitudeEstimator : Node
{
    [Export] public FlightSettings Settings { get; set; }
    [Export] public bool DebugLog { get; set; }

    /// <summary>Estimated attitude (same convention as FlightMath.AttitudeDeg).</summary>
    public float EstimatedPitchDeg { get; private set; } = 90f;
    /// <summary>Estimated angular rate.</summary>
    public float EstimatedRateDegPerSec { get; private set; }
    /// <summary>True when at least one attitude sensor can operate.</summary>
    public bool HasValidAttitude { get; private set; }
    /// <summary>Altitude used for star tracker blinding; set by FlightControlSubsystem each tick.</summary>
    public float CurrentAltitudeM { get; set; }
    /// <summary>Sensor used at the last update, or null.</summary>
    public AttitudeSensorModule ActiveSensor { get; private set; }

    private PartTree _tree;
    private SasModeMask _enabledModes = SasModeMask.None;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        NodeGuard.Require(this, Settings, nameof(Settings));
    }

    /// <summary>Gives the estimator the part tree to find sensors in the command pod.</summary>
    public void Bind(PartTree tree)
    {
        _tree = tree;
        HasValidAttitude = false;
        ActiveSensor = null;
        _enabledModes = SasModeMask.None;
        EstimatedPitchDeg = 90f;
        EstimatedRateDegPerSec = 0f;
    }

    /// <summary>Advances drift and filtering by one tick.</summary>
    public void Update(float truePitchDeg, float trueRateDegPerSec, float deltaTime)
    {
        AttitudeSensorModule best = null;
        SasModeMask modes = SasModeMask.None;
        bool starFix = false;
        float blindAltitude = Settings?.StarTrackerMinAltitudeM ?? 0f;

        if (_tree != null)
        {
            foreach (CommandPodPart pod in _tree.GetParts<CommandPodPart>())
            {
                if (!pod.IsAttached) continue;
                foreach (AttitudeSensorModule sensor in pod.GetModules<AttitudeSensorModule>())
                {
                    if (sensor is StarTrackerModule tracker) tracker.IsBlinded = CurrentAltitudeM < blindAltitude;
                    if (!sensor.CanOperate()) continue;
                    if (sensor is GyroscopeImuModule imu) imu.AccumulatedDriftDeg += imu.DriftRateDegPerMin / 60f * deltaTime;
                    if (sensor is StarTrackerModule) starFix = true;
                    modes |= sensor.EnabledSasModes;
                    if (best == null || sensor.SensorAccuracyArcsec < best.SensorAccuracyArcsec) best = sensor;
                }
            }

            // A working star tracker recalibrates every IMU.
            if (starFix)
                foreach (CommandPodPart pod in _tree.GetParts<CommandPodPart>())
                    foreach (GyroscopeImuModule imu in pod.GetModules<GyroscopeImuModule>())
                        imu.CalibrateWithStarTracker();
        }

        _enabledModes = modes;
        bool wasValid = HasValidAttitude;
        HasValidAttitude = best != null;
        if (best != ActiveSensor && DebugLog) GD.Print($"[Control] Attitude sensor -> {best?.ModelId ?? "none"}");
        ActiveSensor = best;
        if (best == null) return; // hold the last estimate

        float sigmaDeg = best.SensorAccuracyArcsec / 3600f;
        float measured = best.ReadPitch(truePitchDeg) + (sigmaDeg > 0f ? _rng.Randfn(0f, sigmaDeg) : 0f);
        float tau = (best.AcsDefinition?.ResponseLatencyMs ?? 0f) / 1000f;
        float alpha = tau > 0f ? 1f - Mathf.Exp(-deltaTime / tau) : 1f;

        if (!wasValid)
        {
            EstimatedPitchDeg = FlightMath.WrapDeg(measured);
            EstimatedRateDegPerSec = trueRateDegPerSec;
            return;
        }
        EstimatedPitchDeg = FlightMath.WrapDeg(EstimatedPitchDeg + alpha * FlightMath.WrapDeg(measured - EstimatedPitchDeg));
        EstimatedRateDegPerSec += alpha * (trueRateDegPerSec - EstimatedRateDegPerSec);
    }

    /// <summary>OR of EnabledSasModes of every operating sensor (Off is always allowed).</summary>
    public SasModeMask GetEnabledSasModes()
    {
        return _enabledModes;
    }
}
