using Godot;
using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>SAS: turns the pilot input and the selected mode into one normalized command u (C_CONTROL.md 5.4).</summary>
public partial class AttitudeController : Node
{
    [Export] public AttitudeEstimator Estimator { get; set; }
    [Export] public PIDStabilizer PIDStabilizer { get; set; }
    [Export] public bool DebugLog { get; set; }

    /// <summary>Selected SAS mode.</summary>
    public SasMode Mode { get; private set; } = SasMode.Off;
    /// <summary>Attitude to hold in StabilityAssist and HoldPitch.</summary>
    public float TargetPitchDeg { get; set; } = 90f;
    /// <summary>Velocity direction vs horizon, set by FlightControlSubsystem each tick (for Prograde/Retrograde).</summary>
    public float CurrentFlightPathAngleDeg { get; set; } = 90f;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Estimator, nameof(Estimator))) return;
        NodeGuard.Require(this, PIDStabilizer, nameof(PIDStabilizer));
    }

    /// <summary>Changes mode if the sensors allow it. Off is always allowed.</summary>
    public bool SetMode(SasMode mode)
    {
        if (Estimator != null && !Estimator.GetEnabledSasModes().Has(mode)) return false;
        if (mode == Mode) return true;
        Mode = mode;
        PIDStabilizer?.Reset();
        if (Estimator != null) TargetPitchDeg = Estimator.EstimatedPitchDeg;
        if (DebugLog) GD.Print($"[Control] SAS -> {mode}");
        return true;
    }

    /// <summary>Normalized command -1..1 (u > 0 asks for positive torque).</summary>
    public float ComputeDemand(PilotInput input, float deltaTime)
    {
        // TODO(C4): manual override (u = -PitchAxis; HoldPitch shifts TargetPitchDeg), per-mode targets, PID,
        //           auto Off when Estimator.HasValidAttitude is false. Temporary: manual only.
        return Mathf.Clamp(-input.PitchAxis, -1f, 1f);
    }
}
