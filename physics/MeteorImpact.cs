using RocketGame.Building;

namespace RocketGame.Physics;

/// <summary>Phase 2 (PHASE2_PLAN.md 5.6, owner B, task B12): consequences of a meteor hitting a part.</summary>
public static class MeteorImpact
{
    /// <summary>
    /// A meteor hit part at speedMps relative speed. Below the effective impact tolerance nothing happens; above it a tank leaks,
    /// the pod gets a HullBreach, an engine breaks; above 3x the part breaks. Stub from C9: does nothing, returns false.
    /// </summary>
    public static bool Resolve(RocketPart part, float speedMps) => false;
}
