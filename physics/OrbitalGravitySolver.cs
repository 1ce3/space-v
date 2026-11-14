using System;
using Godot;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>Point-mass gravity of one planet. World 2D, planet center at (0, -PlanetRadius).</summary>
public class OrbitalGravitySolver
{
    /// <summary>Newtonian constant of gravitation (m^3/(kg*s^2)).</summary>
    public const double GravitationalConstant = 6.674e-11;

    /// <summary>Planet mass (kg).</summary>
    public float PlanetMass { get; private set; }
    /// <summary>Planet radius (m).</summary>
    public float PlanetRadius { get; private set; }
    /// <summary>Planet center in world 2D (m).</summary>
    public Vector2 PlanetCenter { get; private set; }

    private double _mu;

    /// <summary>Loads mass and radius of the body. Must be called before any other method.</summary>
    public void Configure(CelestialBodyDefinition body)
    {
        PlanetMass = body.Mass;
        PlanetRadius = body.Radius;
        PlanetCenter = new Vector2(0f, -body.Radius);
        _mu = GravitationalConstant * body.Mass;
        if (_mu <= 0.0 || body.Radius <= 0f)
        {
            GD.PushError($"[Physics] Body '{body.BodyId}' has Mass or Radius <= 0; gravity will be zero.");
        }
    }

    /// <summary>Gravity force (N) on a ship of shipMass (kg) at worldPosition (m), pointing to the planet center.</summary>
    public Vector2 CalculateGravityForce(Vector2 worldPosition, float shipMass)
    {
        double dx = worldPosition.X - PlanetCenter.X;
        double dy = worldPosition.Y - PlanetCenter.Y;
        double dist2 = dx * dx + dy * dy;
        if (_mu <= 0.0 || dist2 < 1.0) return Vector2.Zero;
        double dist = Math.Sqrt(dist2);
        double scale = -_mu * shipMass / (dist2 * dist);
        return new Vector2((float)(dx * scale), (float)(dy * scale));
    }

    /// <summary>Circular orbit speed (m/s) at the given altitude (m).</summary>
    public float CalculateOrbitalVelocity(float altitudeMeters)
    {
        double r = (double)PlanetRadius + altitudeMeters;
        if (_mu <= 0.0 || r <= 0.0) return 0f;
        return (float)Math.Sqrt(_mu / r);
    }

    /// <summary>Altitude (m) of worldPosition above the planet surface.</summary>
    public float GetAltitude(Vector2 worldPosition)
        => FlightMath.Altitude(worldPosition, PlanetCenter, PlanetRadius);
}
