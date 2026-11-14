using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>Air state as a function of altitude above the planet surface.</summary>
public interface IAtmosphereModel
{
    /// <summary>Air state at the given altitude (m). Returns AtmosphereSample.Vacuum above the atmosphere.</summary>
    AtmosphereSample Sample(float altitudeMeters);
}
