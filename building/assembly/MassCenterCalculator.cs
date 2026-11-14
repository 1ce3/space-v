using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Mass, CoM and moment of inertia of a set of parts (vehicle-local frame, A_BUILDING.md 5.3).</summary>
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

    /// <summary>Mass-weighted average of GetLocalPosition() (m). Zero when there is no mass.</summary>
    public static Vector2 CalculateCoM(IReadOnlyList<RocketPart> parts)
    {
        float total = 0f;
        Vector2 sum = Vector2.Zero;
        foreach (RocketPart part in parts)
        {
            float m = part.GetCurrentMass();
            total += m;
            sum += part.GetLocalPosition() * m;
        }
        return total > 0f ? sum / total : Vector2.Zero;
    }

    /// <summary>Sum of solid-box inertia m(w^2+h^2)/12 plus m*d^2 about com (kg*m^2).</summary>
    public static float CalculateMomentOfInertia(IReadOnlyList<RocketPart> parts, Vector2 com)
    {
        float cell = RocketPart.GetCellSize();
        float inertia = 0f;
        foreach (RocketPart part in parts)
        {
            float m = part.GetCurrentMass();
            float w = part.Footprint.X * cell;
            float h = part.Footprint.Y * cell;
            inertia += m * (w * w + h * h) / 12f + m * part.GetLocalPosition().DistanceSquaredTo(com);
        }
        return inertia;
    }
}
