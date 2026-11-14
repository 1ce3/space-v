using Godot;

namespace RocketGame.Core;

/// <summary>Shared 2D flight geometry. World 2D: X = downrange at the pad, Y = up at the pad, planet center at (0, -Radius).</summary>
public static class FlightMath
{
    public const float G0 = 9.80665f;
    public const float StefanBoltzmann = 5.670374e-8f;

    public static Vector2 LocalUp(Vector2 position, Vector2 planetCenter) => (position - planetCenter).Normalized();

    /// <summary>Local horizontal direction, pointing +X at the pad.</summary>
    public static Vector2 LocalEast(Vector2 position, Vector2 planetCenter)
    {
        Vector2 up = LocalUp(position, planetCenter);
        return new Vector2(up.Y, -up.X);
    }

    public static float Altitude(Vector2 position, Vector2 planetCenter, float planetRadius)
        => (position - planetCenter).Length() - planetRadius;

    public static float HorizontalSpeed(Vector2 velocity, Vector2 position, Vector2 planetCenter)
        => velocity.Dot(LocalEast(position, planetCenter));

    public static float VerticalSpeed(Vector2 velocity, Vector2 position, Vector2 planetCenter)
        => velocity.Dot(LocalUp(position, planetCenter));

    public static float WrapDeg(float deg)
    {
        deg %= 360f;
        if (deg > 180f) deg -= 360f;
        if (deg <= -180f) deg += 360f;
        return deg;
    }

    /// <summary>Body attitude vs the local horizon: 90 = nose straight up, 0 = nose toward local east. bodyRotationRad = VehicleRoot.Rotation.Z.</summary>
    public static float AttitudeDeg(float bodyRotationRad, Vector2 position, Vector2 planetCenter)
    {
        Vector2 up = LocalUp(position, planetCenter);
        float horizonUpDeg = Mathf.RadToDeg(Mathf.Atan2(up.Y, up.X));
        float bodyUpDeg = 90f + Mathf.RadToDeg(bodyRotationRad);
        return WrapDeg(90f + bodyUpDeg - horizonUpDeg);
    }

    /// <summary>Velocity direction vs the local horizon, same convention as AttitudeDeg. Returns 90 when nearly at rest.</summary>
    public static float FlightPathAngleDeg(Vector2 velocity, Vector2 position, Vector2 planetCenter)
    {
        if (velocity.LengthSquared() < 0.01f) return 90f;
        Vector2 up = LocalUp(position, planetCenter);
        float horizonUpDeg = Mathf.RadToDeg(Mathf.Atan2(up.Y, up.X));
        float velDeg = Mathf.RadToDeg(Mathf.Atan2(velocity.Y, velocity.X));
        return WrapDeg(90f + velDeg - horizonUpDeg);
    }

    /// <summary>Vehicle-local vector to world, given VehicleRoot.Rotation.Z.</summary>
    public static Vector2 BodyToWorld(Vector2 local, float bodyRotationRad) => local.Rotated(bodyRotationRad);

    /// <summary>2D cross product = torque about +Z.</summary>
    public static float Cross(Vector2 r, Vector2 f) => r.X * f.Y - r.Y * f.X;
}
