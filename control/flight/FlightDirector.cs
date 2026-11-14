using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Physics;
using RocketGame.Presentation;
using RocketGame.Research;

namespace RocketGame.Control;

/// <summary>
/// Referee of a flown segment: flight phase, segment success, failure and landing (C_CONTROL.md 4.4).
/// Emits MissionEnded exactly once per run.
/// </summary>
public partial class FlightDirector : Node
{
    [Signal] public delegate void PhaseChangedEventHandler(FlightPhase phase);
    [Signal] public delegate void SegmentCompletedEventHandler(MissionRun run);
    [Signal] public delegate void MissionEndedEventHandler(FlightResult result);

    [ExportGroup("Links")]
    [Export] public VehicleRoot Vehicle { get; set; }
    [Export] public PhysicsCoordinator Coordinator { get; set; }
    [Export] public InputHandler InputHandler { get; set; }
    [Export] public FlightHUD Hud { get; set; }
    [Export] public FlightCameraRig CameraRig { get; set; }
    [Export] public BodyEnvironmentVisual Environment { get; set; }
    /// <summary>Optional (P2).</summary>
    [Export] public FlightVfxDirector Vfx { get; set; }
    /// <summary>Optional (P2).</summary>
    [Export] public FlightAudioDirector Audio { get; set; }
    [Export] public FlightSettings Settings { get; set; }

    [ExportGroup("Debug")]
    [Export] public bool DebugLog { get; set; }

    /// <summary>Current flight phase.</summary>
    public FlightPhase Phase { get; private set; } = FlightPhase.PreLaunch;
    /// <summary>Run being flown.</summary>
    public MissionRun Run { get; private set; }
    /// <summary>Flight recorder of the run.</summary>
    public BlackBoxTelemetryLogger BlackBox => Run?.BlackBox;

    private bool _hasEnded;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Vehicle, nameof(Vehicle))) return;
        if (!NodeGuard.Require(this, Coordinator, nameof(Coordinator))) return;
        if (!NodeGuard.Require(this, InputHandler, nameof(InputHandler))) return;
        if (!NodeGuard.Require(this, Hud, nameof(Hud))) return;
        if (!NodeGuard.Require(this, CameraRig, nameof(CameraRig))) return;
        NodeGuard.Require(this, Settings, nameof(Settings));
    }

    /// <summary>Starts a flown segment (EarthAscent or MarsEdl) of the run.</summary>
    public void StartSegment(MissionRun run)
    {
        Run = run;
        _hasEnded = false;
        if (run == null) return;
        MissionSegmentDefinition segment = run.CurrentSegment;
        if (segment == null)
        {
            GD.PushError("[Control] FlightDirector.StartSegment: run has no current segment");
            return;
        }

        Vehicle.Run = run;
        switch (segment.Kind)
        {
            case SegmentKind.EarthAscent:
                Coordinator.Initialize(segment);
                Environment?.SetBody(segment.Body);
                Vehicle.OnDesignConfirmed(run.Rocket);
                CameraRig.Follow(Vehicle);
                InputHandler.SetInputEnabled(true);
                Phase = FlightPhase.Entry; // force the PhaseChanged emit below
                SetPhase(FlightPhase.PreLaunch);
                break;
            case SegmentKind.MarsEdl:
                // TODO(C6): Vehicle.RestoreFromRun(run), Entry, InputHandler.SetInputEnabled(true).
                break;
            default:
                GD.PushError($"[Control] FlightDirector.StartSegment: {segment.Kind} is not a flown segment");
                break;
        }
        if (DebugLog) GD.Print($"[Control] Segment {run.SegmentIndex} ({segment.SegmentId}) started");
    }

    /// <summary>Called by VehicleRoot after every physics tick.</summary>
    public void OnTick(TelemetryFrame frame)
    {
        // TODO(C5): phase transitions, segment 1 success, out-of-thrust failure.
    }

    /// <summary>Called for every cut of the part tree.</summary>
    public void OnPartSevered(RocketPart part, SeverCause cause)
    {
        // TODO(C5): pod lost -> TippedOver/Collision handling per 4.4.
    }

    /// <summary>FailureDetector.BreakTriggered handler: records the failure and ends the run.</summary>
    public void OnBreakTriggered(RocketPart part, FailureCause cause)
    {
        // TODO(C5): BlackBox.RecordFailure(cause, part); EndMission(MissionOutcome.Failed).
    }

    /// <summary>Called by VehicleRoot when the lowest part reaches altitude 0.</summary>
    public void OnGroundContact(RocketPart part, float contactSpeed)
    {
        // TODO(C5/C6): MonitorImpact, landing check (MaxLandingTiltDeg, pod present) -> Landed or failure.
    }

    /// <summary>Player abort (flight_abort).</summary>
    public void Abort()
    {
        // TODO(C5): BlackBox.RecordFailure(FailureCause.Aborted, null); EndMission(MissionOutcome.Aborted).
    }

    private void SetPhase(FlightPhase phase)
    {
        if (Phase == phase) return;
        Phase = phase;
        if (DebugLog) GD.Print($"[Control] FlightPhase -> {phase}");
        EmitSignal(SignalName.PhaseChanged, (int)phase);
    }

    /// <summary>Locks input, builds the result and emits MissionEnded once.</summary>
    private void EndMission(MissionOutcome outcome)
    {
        if (_hasEnded || Run == null) return;
        _hasEnded = true;
        InputHandler?.SetInputEnabled(false);
        SetPhase(outcome == MissionOutcome.Success ? FlightPhase.Landed : FlightPhase.Failed);
        EmitSignal(SignalName.MissionEnded, Run.BlackBox.BuildResult(outcome));
    }
}
