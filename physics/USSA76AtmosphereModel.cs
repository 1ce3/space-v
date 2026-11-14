using Godot;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>US Standard Atmosphere 1976, 0 to 86 km. Vacuum above.</summary>
public class USSA76AtmosphereModel : IAtmosphereModel
{
    /// <summary>g0 * M / R* (K/m). Exponent constant of the barometric formula.</summary>
    public const float HydrostaticConstant = 0.0341632f;
    /// <summary>Specific gas constant of dry air (J/(kg*K)).</summary>
    public const float GasConstantAir = 287.053f;
    /// <summary>Ratio of specific heats of air.</summary>
    public const float HeatCapacityRatio = 1.4f;
    /// <summary>Top of the table (m). Above this the model returns vacuum.</summary>
    public const float TopAltitudeM = 86000f;

    /// <summary>The seven USSA76 layers, ordered by base altitude.</summary>
    public LayerTable[] Layers { get; } =
    {
        new(0f,      288.15f, -0.0065f, 101325f),
        new(11000f,  216.65f,  0f,      22632.1f),
        new(20000f,  216.65f,  0.001f,  5474.89f),
        new(32000f,  228.65f,  0.0028f, 868.019f),
        new(47000f,  270.65f,  0f,      110.906f),
        new(51000f,  270.65f, -0.0028f, 66.9389f),
        new(71000f,  214.65f, -0.002f,  3.95642f),
    };

    /// <summary>Air state at the given altitude (m). Below 0 is treated as 0.</summary>
    public AtmosphereSample Sample(float altitudeMeters)
    {
        if (altitudeMeters > TopAltitudeM) return AtmosphereSample.Vacuum;
        float h = Mathf.Max(altitudeMeters, 0f);

        LayerTable layer = FindLayer(h);
        float dh = h - layer.BaseAltitudeM;
        float temperature;
        float pressure;
        if (layer.LapseRateKPerM != 0f)
        {
            temperature = layer.BaseTemperatureK + layer.LapseRateKPerM * dh;
            pressure = layer.BasePressurePa
                * Mathf.Pow(layer.BaseTemperatureK / temperature, HydrostaticConstant / layer.LapseRateKPerM);
        }
        else
        {
            temperature = layer.BaseTemperatureK;
            pressure = layer.BasePressurePa * Mathf.Exp(-HydrostaticConstant * dh / layer.BaseTemperatureK);
        }

        return new AtmosphereSample
        {
            Density = pressure / (GasConstantAir * temperature),
            Pressure = pressure,
            Temperature = temperature,
            SpeedOfSound = Mathf.Sqrt(HeatCapacityRatio * GasConstantAir * temperature),
        };
    }

    private LayerTable FindLayer(float h)
    {
        for (int i = Layers.Length - 1; i > 0; i--)
        {
            if (h >= Layers[i].BaseAltitudeM) return Layers[i];
        }
        return Layers[0];
    }
}
