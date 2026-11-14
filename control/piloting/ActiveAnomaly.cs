using Godot;
using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>An anomaly waiting for the player. RefCounted so it can travel through signals.</summary>
[GlobalClass]
public partial class ActiveAnomaly : RefCounted
{
    public FlightEventType Type { get; set; }
    /// <summary>Seconds left before the consequence (0 = warning only, no timer).</summary>
    public float TimeRemaining { get; set; }
    /// <summary>ModelId of the part or module involved, or empty.</summary>
    public string SourceId { get; set; } = "";

    public ActiveAnomaly() { }

    public ActiveAnomaly(FlightEventType type, float timeRemaining, string sourceId)
    {
        Type = type;
        TimeRemaining = timeRemaining;
        SourceId = sourceId ?? "";
    }
}
