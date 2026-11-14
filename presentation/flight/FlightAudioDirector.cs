using Godot;
using RocketGame.Building;
using RocketGame.Control;
using RocketGame.Core;

namespace RocketGame.Presentation;

/// <summary>Flight audio (engine loop, separation bangs, alarms). P2; read-only.</summary>
public partial class FlightAudioDirector : Node
{
    [Export] public bool Enabled { get; set; } = true;

    public void OnPartSevered(RocketPart part, SeverCause cause) { }
    public void OnStageActivated(int stageIndex) { }
    public void OnAnomalyRaised(ActiveAnomaly anomaly) { }
    public void OnPhaseChanged(FlightPhase phase) { }
    public void UpdateFromTelemetry(TelemetryFrame frame) { }
}
