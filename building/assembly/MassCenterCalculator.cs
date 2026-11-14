using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Mass, CoM and moment of inertia of a set of parts (vehicle-local frame).</summary>
public static class MassCenterCalculator
{
    /// <summary>All three properties at once.</summary>
    public static MassProperties Calculate(IReadOnlyList<RocketPart> parts)
    {
        float total = 0f;
        foreach (RocketPart part in parts) total += part.GetCurrentMass();
        Vector2 com = CalculateCoM(parts);
        return new MassProperties
        {
            TotalMass = total,
            CenterOfMass = com,
            MomentOfInertia = CalculateMomentOfInertia(parts, com),
        };
    }

    /// <summary>Mass-weighted average of GetLocalPosition() (m).</summary>
    public static Vector2 CalculateCoM(IReadOnlyList<RocketPart> parts)
    {
        // TODO(A3)
        return Vector2.Zero;
    }

    /// <summary>Sum of solid-box inertia m(w^2+h^2)/12 plus m*d^2 about com (kg*m^2).</summary>
    public static float CalculateMomentOfInertia(IReadOnlyList<RocketPart> parts, Vector2 com)
    {
        // TODO(A3)
        return 0f;
    }
}
