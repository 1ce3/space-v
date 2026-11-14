using Godot;
using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>
/// PD+I attitude stabilizer: u = Kp*e/ErrorScale + Ki*integral - Kd*rate/RateScale, clamped to -1..1.
/// Gains come from FlightSettings so the whole team tunes one .tres.
/// </summary>
public partial class PIDStabilizer : Node
{
    [Export] public FlightSettings Settings { get; set; }

    /// <summary>Proportional gain (from Settings).</summary>
    public float Kp => Settings?.Kp ?? 0f;
    /// <summary>Integral gain (from Settings).</summary>
    public float Ki => Settings?.Ki ?? 0f;
    /// <summary>Derivative gain (from Settings).</summary>
    public float Kd => Settings?.Kd ?? 0f;

    private float _integral;

    public override void _Ready()
    {
        NodeGuard.Require(this, Settings, nameof(Settings));
    }

    /// <summary>Normalized command -1..1 from the attitude error and the angular rate.</summary>
    public float Compute(float errorDeg, float rateDegPerSec, float deltaTime)
    {
        if (Settings == null) return 0f;

        float error = errorDeg / Settings.ErrorScaleDeg;
        float rate = rateDegPerSec / Settings.RateScaleDegPerSec;
        _integral = Mathf.Clamp(_integral + error * deltaTime, -Settings.IntegralLimit, Settings.IntegralLimit);
        float u = Kp * error + Ki * _integral - Kd * rate;
        return Mathf.Clamp(u, -1f, 1f);
    }

    /// <summary>Clears the integral (on mode change and on MassPropertiesChanged).</summary>
    public void Reset()
    {
        _integral = 0f;
    }
}
