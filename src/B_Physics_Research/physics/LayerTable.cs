namespace RocketGame.Physics;

/// <summary>Base values of one USSA76 layer.</summary>
public readonly struct LayerTable
{
    /// <summary>Altitude of the layer base (m).</summary>
    public readonly float BaseAltitudeM;
    /// <summary>Temperature at the layer base (K).</summary>
    public readonly float BaseTemperatureK;
    /// <summary>Temperature gradient (K/m).</summary>
    public readonly float LapseRateKPerM;
    /// <summary>Pressure at the layer base (Pa).</summary>
    public readonly float BasePressurePa;

    public LayerTable(float baseAltitudeM, float baseTemperatureK, float lapseRateKPerM, float basePressurePa)
    {
        BaseAltitudeM = baseAltitudeM;
        BaseTemperatureK = baseTemperatureK;
        LapseRateKPerM = lapseRateKPerM;
        BasePressurePa = basePressurePa;
    }
}
