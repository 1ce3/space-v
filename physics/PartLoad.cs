using Godot;
using RocketGame.Building;

namespace RocketGame.Physics;

/// <summary>External force (thrust + aero) acting on one part, in world 2D (N).</summary>
public struct PartLoad
{
    /// <summary>The part.</summary>
    public RocketPart Part;
    /// <summary>Force on the part (N).</summary>
    public Vector2 Force;
}
