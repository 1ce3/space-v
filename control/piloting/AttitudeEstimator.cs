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

    private PartTree _tree;
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
    }

    /// <summary>Advances drift and filtering by one tick.</summary>
    public void Update(float truePitchDeg, float trueRateDegPerSec, float deltaTime)
    {
        // TODO(C4): pick the best operating AttitudeSensorModule, star tracker blinding, IMU drift, Gauss noise,
        //           low-pass with ResponseLatencyMs. Temporary pass-through so C3 can fly:
        EstimatedPitchDeg = truePitchDeg;
        EstimatedRateDegPerSec = trueRateDegPerSec;
    }

    /// <summary>OR of EnabledSasModes of every operating sensor (Off is always allowed).</summary>
    public SasModeMask GetEnabledSasModes()
    {
        // TODO(C4)
        return SasModeMask.None;
    }
}
