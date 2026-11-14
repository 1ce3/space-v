using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Physics;

/// <summary>
/// Phase 2 (PHASE2_PLAN.md 5.2, owner B, task B11): plans 2..4 cruise events (solar flare, meteor field,
/// instrument failure) on random days and applies their damage. Stub from C9: plans nothing.
/// </summary>
public class CruiseEventDirector
{
    private readonly PhysicsSettings _settings;
    private readonly RandomNumberGenerator _rng = new();

    public CruiseEventDirector(PhysicsSettings settings, ulong seed = 0)
    {
        _settings = settings;
        if (seed != 0) _rng.Seed = seed;
    }

    /// <summary>Picks the event days for this cruise. Stub: none.</summary>
    public void Plan(PartTree parts, MissionSegmentDefinition segment) { }

    /// <summary>Events of this day (warnings and the events themselves). Stub: none.</summary>
    public List<FlightEvent> EventsForDay(int day, PartTree parts) => new();

    /// <summary>Solar flare damage (5.3); returns ModelIds of modules that broke. Stub: none.</summary>
    public List<string> ApplySolarFlare(PartTree parts, ICollection<PodModule> poweredDown) => new();
}
