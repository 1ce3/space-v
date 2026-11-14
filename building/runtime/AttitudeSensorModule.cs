using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>IMU or star tracker.</summary>
public abstract class AttitudeSensorModule : PodModule
{
    /// <summary>Current 1-sigma noise (deg).</summary>
    public float CurrentNoiseDeg { get; protected set; }

    public AcsModuleDefinition AcsDefinition => Definition as AcsModuleDefinition;
    /// <summary>SAS modes this sensor allows while operating.</summary>
    public SasModeMask EnabledSasModes => AcsDefinition?.EnabledSasModes ?? SasModeMask.None;
    public float SensorAccuracyArcsec => AcsDefinition?.SensorAccuracyArcsec ?? 0f;

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        CurrentNoiseDeg = SensorAccuracyArcsec / 3600f;
    }

    /// <summary>Measured attitude for a true attitude, without noise (Control's AttitudeEstimator adds noise) (deg).</summary>
    public virtual float ReadPitch(float truePitchDeg)
    {
        return truePitchDeg;
    }
}
