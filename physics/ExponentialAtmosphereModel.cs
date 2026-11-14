using Godot;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>Isothermal exponential atmosphere (used for Mars). Values come from a CelestialBodyDefinition.</summary>
public class ExponentialAtmosphereModel : IAtmosphereModel
{
    /// <summary>Density at altitude 0 (kg/m^3).</summary>
    public float SeaLevelDensity { get; }
    /// <summary>Pressure at altitude 0 (Pa).</summary>
    public float SeaLevelPressure { get; }
    /// <summary>Scale height (m).</summary>
    public float ScaleHeight { get; }
    /// <summary>Constant air temperature (K).</summary>
    public float TemperatureK { get; }
    /// <summary>Constant speed of sound (m/s).</summary>
    public float SpeedOfSoundMps { get; }
    /// <summary>Above this altitude the model returns vacuum (m).</summary>
    public float AtmosphereTopM { get; }

    public ExponentialAtmosphereModel(CelestialBodyDefinition body)
    {
        SeaLevelDensity = body.SeaLevelDensity;
        SeaLevelPressure = body.SeaLevelPressure;
        ScaleHeight = body.ScaleHeight;
        TemperatureK = body.SurfaceTemperatureK;
        SpeedOfSoundMps = body.SpeedOfSoundMps;
        AtmosphereTopM = body.AtmosphereTopM;
        if (ScaleHeight <= 0f)
        {
            GD.PushError($"[Physics] Body '{body.BodyId}' has ScaleHeight <= 0; atmosphere will be vacuum.");
        }
    }

    /// <summary>Air state at the given altitude (m). Below 0 is treated as 0.</summary>
    public AtmosphereSample Sample(float altitudeMeters)
    {
        if (ScaleHeight <= 0f || altitudeMeters > AtmosphereTopM) return AtmosphereSample.Vacuum;
        float h = Mathf.Max(altitudeMeters, 0f);
        float factor = Mathf.Exp(-h / ScaleHeight);
        return new AtmosphereSample
        {
            Density = SeaLevelDensity * factor,
            Pressure = SeaLevelPressure * factor,
            Temperature = TemperatureK,
            SpeedOfSound = SpeedOfSoundMps,
        };
    }
}
