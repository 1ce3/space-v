using Godot;
using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>SAS: turns the pilot input and the selected mode into one normalized command u (C_CONTROL.md 5.4).</summary>
public partial class AttitudeController : Node
{
    /// <summary>Emitted whenever the mode changes, including the automatic fall back to Off.</summary>
    [Signal] public delegate void ModeChangedEventHandler(SasMode mode);

    [Export] public AttitudeEstimator Estimator { get; set; }
    [Export] public PIDStabilizer PIDStabilizer { get; set; }

    /// <summary>Pitch input below this is treated as "no key".</summary>
    [ExportGroup("Tuning")]
    [Export(PropertyHint.Range, "0,0.5,0.01")] public float InputDeadzone { get; set; } = 0.05f;
    /// <summary>How fast A/D move the target in HoldPitch (deg/s).</summary>
    [Export(PropertyHint.Range, "1,90,1")] public float HoldPitchShiftDegPerSec { get; set; } = 15f;
    /// <summary>StabilityAssist latches the attitude once the rate falls below this after a key release (deg/s).</summary>
    [Export(PropertyHint.Range, "0.1,10,0.1")] public float SettleRateDegPerSec { get; set; } = 1f;

    [ExportGroup("Debug")]
    [Export] public bool DebugLog { get; set; }

    /// <summary>Selected SAS mode.</summary>
    public SasMode Mode { get; private set; } = SasMode.Off;
    /// <summary>Attitude to hold in StabilityAssist and HoldPitch.</summary>
    public float TargetPitchDeg { get; set; } = 90f;
    /// <summary>Velocity direction vs horizon, set by FlightControlSubsystem each tick (for Prograde/Retrograde).</summary>
    public float CurrentFlightPathAngleDeg { get; set; } = 90f;
    /// <summary>Mode chosen before the last switch to Off (T turns it back on).</summary>
    public SasMode LastActiveMode { get; private set; } = SasMode.StabilityAssist;

    /// <summary>StabilityAssist after a key release: damp the rotation first, then latch the attitude.</summary>
    private bool _settling;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Estimator, nameof(Estimator))) return;
        NodeGuard.Require(this, PIDStabilizer, nameof(PIDStabilizer));
    }

    /// <summary>Changes mode if the sensors allow it. Off is always allowed.</summary>
    public bool SetMode(SasMode mode)
    {
        if (Estimator != null && mode != SasMode.Off && (!Estimator.HasValidAttitude || !Estimator.GetEnabledSasModes().Has(mode)))
            return false;
        if (mode == Mode) return true;
        ApplyMode(mode);
        return true;
    }

    /// <summary>Back to Off and the default target (new vehicle).</summary>
    public void ResetState()
    {
        Mode = SasMode.Off;
        LastActiveMode = SasMode.StabilityAssist;
        TargetPitchDeg = 90f;
        _settling = false;
        PIDStabilizer?.Reset();
    }

    /// <summary>Normalized command -1..1 (u > 0 asks for positive torque, i.e. attitude increasing).</summary>
    public float ComputeDemand(PilotInput input, float deltaTime)
    {
        // Sensors lost (or the mode is no longer allowed): drop to Off.
        if (Mode != SasMode.Off && Estimator != null &&
            (!Estimator.HasValidAttitude || !Estimator.GetEnabledSasModes().Has(Mode)))
        {
            if (DebugLog) GD.Print("[Control] SAS lost its sensors -> Off");
            ApplyMode(SasMode.Off);
        }

        bool pitching = Mathf.Abs(input.PitchAxis) > InputDeadzone;
        if (pitching)
        {
            if (Mode == SasMode.HoldPitch)
            {
                TargetPitchDeg = FlightMath.WrapDeg(TargetPitchDeg - input.PitchAxis * HoldPitchShiftDegPerSec * deltaTime);
            }
            else
            {
                _settling = Mode == SasMode.StabilityAssist;
                PIDStabilizer?.Reset();
                return Mathf.Clamp(-input.PitchAxis, -1f, 1f);
            }
        }

        if (Mode == SasMode.Off || Estimator == null || PIDStabilizer == null) return 0f;

        float current = Estimator.EstimatedPitchDeg;
        float rate = Estimator.EstimatedRateDegPerSec;
        float target;
        switch (Mode)
        {
            case SasMode.StabilityAssist:
                if (_settling)
                {
                    if (Mathf.Abs(rate) > SettleRateDegPerSec) return PIDStabilizer.Compute(0f, rate, deltaTime);
                    _settling = false;
                    TargetPitchDeg = current;
                }
                target = TargetPitchDeg;
                break;
            case SasMode.HoldPitch:
                target = TargetPitchDeg;
                break;
            case SasMode.Prograde:
                target = CurrentFlightPathAngleDeg;
                break;
            case SasMode.Retrograde:
                target = FlightMath.WrapDeg(CurrentFlightPathAngleDeg + 180f);
                break;
            default:
                return 0f;
        }
        return PIDStabilizer.Compute(FlightMath.WrapDeg(target - current), rate, deltaTime);
    }

    private void ApplyMode(SasMode mode)
    {
        if (Mode != SasMode.Off) LastActiveMode = Mode;
        Mode = mode;
        _settling = false;
        PIDStabilizer?.Reset();
        if (Estimator != null) TargetPitchDeg = Estimator.EstimatedPitchDeg;
        if (DebugLog) GD.Print($"[Control] SAS -> {mode}");
        EmitSignal(SignalName.ModeChanged, (int)mode);
    }
}
