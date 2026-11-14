using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Physics;

/// <summary>Rolls and detects in-flight incidents. Only types in EnabledHazards fire, each once until acknowledged (guide B 4.6, P1).</summary>
public class HazardMonitor
{
    /// <summary>Peak wind shear force (N) of the current segment (P2).</summary>
    public float WindShearForceX { get; set; }
    /// <summary>Stormy weather in the current segment (P2).</summary>
    public bool IsStormy { get; set; }
    /// <summary>Hazard types allowed in the current segment.</summary>
    public FlightEventMask EnabledHazards { get; set; }

    private readonly PhysicsSettings _settings;
    private readonly RandomNumberGenerator _rng = new();
    private readonly List<FlightEvent> _events = new();
    private readonly HashSet<FlightEventType> _active = new();

    public HazardMonitor(PhysicsSettings settings, ulong seed)
    {
        _settings = settings;
        _rng.Seed = seed;
    }

    /// <summary>Reads the hazard setup of a segment and clears active hazards.</summary>
    public void Configure(MissionSegmentDefinition segment)
    {
        EnabledHazards = segment.EnabledHazards;
        IsStormy = segment.IsStormy;
        WindShearForceX = segment.WindShearForceX;
        _active.Clear();
    }

    /// <summary>Events for this tick. The returned list is reused.</summary>
    public List<FlightEvent> Evaluate(PhysicsStepInput input, PhysicsStepOutput partial)
    {
        _events.Clear();
        // TODO(B6): MaxQExceeded, GLimitExceeded, CryoLeak, DebrisWarning, ComponentOverheat, PowerBrownout.
        return _events;
    }

    /// <summary>Control has handled this hazard; it may fire again.</summary>
    public void Acknowledge(FlightEventType type) => _active.Remove(type);

    /// <summary>Lightning roll at this altitude (P2).</summary>
    public bool CheckLightningStrike(float altitudeMeters) => false;

    /// <summary>True when the rocket has not dodged the debris (P1).</summary>
    public bool CheckDebrisWarning(Vector2 rocketPos, Vector2 nearestDebrisPos) => false;

    /// <summary>Cryo leak roll for the Hydrolox / Methalox tanks in parts (P1).</summary>
    public bool CheckCryoLeak(PartTree parts) => false;

    /// <summary>Single-event upset roll at this altitude (P2).</summary>
    public bool CheckSeuGlitch(float altitudeMeters) => false;
}
