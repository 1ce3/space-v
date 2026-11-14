using Godot;
using RocketGame.Core;
using RocketGame.Physics;

namespace RocketGame.Tests.B;

/// <summary>Sandbox for task B1: prints atmosphere tables and surface gravity of both bodies.</summary>
public partial class AtmosphereTest : Node
{
    /// <summary>Earth body definition (earth_scaled.tres).</summary>
    [Export] public CelestialBodyDefinition Earth { get; set; }
    /// <summary>Mars body definition (mars_scaled.tres).</summary>
    [Export] public CelestialBodyDefinition Mars { get; set; }
    /// <summary>Earth sample altitudes (km).</summary>
    [Export] public float[] EarthAltitudesKm { get; set; } = { 0f, 11f, 20f, 50f, 80f, 90f };
    /// <summary>Mars sample altitudes (km).</summary>
    [Export] public float[] MarsAltitudesKm { get; set; } = { 0f, 10f, 50f, 100f };
    /// <summary>Altitude at which the circular orbit speed is printed (km).</summary>
    [Export] public float OrbitAltitudeKm { get; set; } = 90f;
    /// <summary>Quit the game after printing, so the log is flushed when run headless or from tools.</summary>
    [Export] public bool QuitWhenDone { get; set; } = true;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Earth, nameof(Earth))) return;
        if (!NodeGuard.Require(this, Mars, nameof(Mars))) return;
        PrintBody(Earth, EarthAltitudesKm);
        PrintBody(Mars, MarsAltitudesKm);
        if (QuitWhenDone) GetTree().Quit();
    }

    private void PrintBody(CelestialBodyDefinition body, float[] altitudesKm)
    {
        IAtmosphereModel atmosphere = body.UsesLayerTable
            ? new USSA76AtmosphereModel()
            : new ExponentialAtmosphereModel(body);
        var gravity = new OrbitalGravitySolver();
        gravity.Configure(body);

        GD.Print($"[Physics] === {body.BodyId} ({(body.UsesLayerTable ? "USSA76" : "exponential")}) ===");
        GD.Print("[Physics]   alt km |   rho kg/m3 |        P Pa |      T K |  a m/s");
        foreach (float km in altitudesKm)
        {
            AtmosphereSample s = atmosphere.Sample(km * 1000f);
            GD.Print($"[Physics] {km,8:F1} | {s.Density,11:E3} | {s.Pressure,11:F2} | {s.Temperature,8:F2} | {s.SpeedOfSound,6:F1}");
        }

        float g = gravity.CalculateGravityForce(Vector2.Zero, 1f).Length();
        float vOrbit = gravity.CalculateOrbitalVelocity(OrbitAltitudeKm * 1000f);
        GD.Print($"[Physics]   surface g = {g:F3} m/s2, circular orbit at {OrbitAltitudeKm:F0} km = {vOrbit:F0} m/s");
    }
}
