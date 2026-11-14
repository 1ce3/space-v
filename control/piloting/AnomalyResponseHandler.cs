using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Physics;

namespace RocketGame.Control;

/// <summary>Turns physics events into anomalies the player must clear with a key (C_CONTROL.md 5.6, P1).</summary>
public partial class AnomalyResponseHandler : Node
{
    [Signal] public delegate void AnomalyRaisedEventHandler(ActiveAnomaly anomaly);
    [Signal] public delegate void AnomalyResolvedEventHandler(ActiveAnomaly anomaly);

    [Export] public PhysicsCoordinator Coordinator { get; set; }
    [Export] public AttitudeController AttitudeController { get; set; }
    [Export] public ThrottleController ThrottleController { get; set; }
    [Export] public bool DebugLog { get; set; }

    /// <summary>Anomalies not yet resolved.</summary>
    public List<ActiveAnomaly> ActiveAnomalies { get; } = new();
    /// <summary>True after the player switched SCE to AUX during a lightning strike.</summary>
    public bool IsSceToAuxActive { get; private set; }
    /// <summary>True after an SEU: SAS off and throttle frozen until the player toggles SAS.</summary>
    public bool IsSafeModeActive { get; private set; }
    /// <summary>True after lightning: the HUD shows garbled telemetry.</summary>
    public bool IsTelemetryGlitched { get; private set; }

    private PartTree _tree;

    /// <summary>Gives the handler the part tree (leaking tanks, RCS, avionics power).</summary>
    public void Bind(PartTree tree)
    {
        _tree = tree;
        ActiveAnomalies.Clear();
        IsSceToAuxActive = false;
        IsSafeModeActive = false;
        IsTelemetryGlitched = false;
    }

    /// <summary>Receives PhysicsStepOutput.Events (or CruiseStepResult.Events).</summary>
    public void OnFlightEvents(IReadOnlyList<FlightEvent> events)
    {
        // TODO(C7): raise ActiveAnomaly per new event type, apply consequences.
    }

    /// <summary>Clears anomalies with the matching keys.</summary>
    public void HandleInput(PilotInput input)
    {
        // TODO(C7): SceToAux, SealLeak, RCS translate, SAS toggle; Coordinator.Hazards.Acknowledge(type).
    }

    /// <summary>Counts down anomaly timers.</summary>
    public void Tick(float deltaTime)
    {
        // TODO(C7)
    }
}
