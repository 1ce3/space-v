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
    /// <summary>An explosion was spawned (camera shake / SFX hook): world position and strength (1 = one part).</summary>
    [Signal] public delegate void ExplosionSpawnedEventHandler(Vector3 position, float strength);

    [ExportGroup("Failure")]
    /// <summary>Seconds of physics after a failure (parts fly apart, explosion) before the result screen.</summary>
    [Export(PropertyHint.Range, "0,10,0.1")] public float FailRevealDelaySeconds { get; set; } = 2f;
    /// <summary>Optional explosion scene from the art team; empty = code placeholder.</summary>
    [Export] public PackedScene ExplosionScene { get; set; }
    [Export(PropertyHint.Range, "0.1,10,0.1")] public float ExplosionScale { get; set; } = 1.5f;
    private RocketPart _failPart;

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

    /// <summary>Seconds with no possible thrust while falling before segment 1 is declared failed.</summary>
    [ExportGroup("Rules")]
    [Export(PropertyHint.Range, "0,10,0.1")] public float OutOfThrustDelaySec { get; set; } = 2f;

    [ExportGroup("Debug")]
    [Export] public bool DebugLog { get; set; }

    /// <summary>Current flight phase.</summary>
    public FlightPhase Phase { get; private set; } = FlightPhase.PreLaunch;
    /// <summary>Run being flown.</summary>
    public MissionRun Run { get; private set; }
    /// <summary>Flight recorder of the run.</summary>
    public BlackBoxTelemetryLogger BlackBox => Run?.BlackBox;

    private bool _hasEnded;
    /// <summary>True between SegmentCompleted and the next StartSegment (segment ended well, run continues).</summary>
    private bool _segmentDone;
    private float _outOfThrustTimer;
    private bool _chuteHintShown;

    /// <summary>The EDL chute hint waits until the vehicle is below this radar altitude (Mars 2020 opened at ~11 km).</summary>
    [Export] public float ChuteHintAltitudeM { get; set; } = 12000f;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Vehicle, nameof(Vehicle))) return;
        if (!NodeGuard.Require(this, Coordinator, nameof(Coordinator))) return;
        if (!NodeGuard.Require(this, InputHandler, nameof(InputHandler))) return;
        if (!NodeGuard.Require(this, Hud, nameof(Hud))) return;
        if (!NodeGuard.Require(this, CameraRig, nameof(CameraRig))) return;
        NodeGuard.Require(this, Settings, nameof(Settings));
        Coordinator.Failures.BreakTriggered += OnBreakTriggered;
        Vehicle.Staging.StageActivated += OnStageActivated;
        Vehicle.Staging.StagingWarning += Hud.ShowWarning;
        if (Vehicle.FlightControl != null) Vehicle.FlightControl.ControlWarning += Hud.ShowWarning;
    }

    public override void _ExitTree()
    {
        if (Coordinator?.Failures != null) Coordinator.Failures.BreakTriggered -= OnBreakTriggered;
    }

    /// <summary>Starts a flown segment (EarthAscent or MarsEdl) of the run.</summary>
    public void StartSegment(MissionRun run)
    {
        Run = run;
        _hasEnded = false;
        _segmentDone = false;
        _outOfThrustTimer = 0f;
        _chuteHintShown = false;
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
                Vehicle.RestoreFromRun(run);
                CameraRig.Follow(Vehicle);
                Hud.Visible = true;
                InputHandler.SetInputEnabled(true);
                Phase = FlightPhase.PreLaunch; // force the PhaseChanged emit below
                SetPhase(FlightPhase.Entry);
                Hud.ShowWarning("Entering Mars atmosphere: turn the heat shield forward");
                break;
            default:
                GD.PushError($"[Control] FlightDirector.StartSegment: {segment.Kind} is not a flown segment");
                break;
        }
        if (DebugLog) GD.Print($"[Control] Segment {run.SegmentIndex} ({segment.SegmentId}) started");
    }

    /// <summary>Called by VehicleRoot after every physics tick.</summary>
    /// <summary>Latest telemetry (tutorial overlay reads altitude from here).</summary>
    public TelemetryFrame LastFrame { get; private set; }

    public void OnTick(TelemetryFrame frame)
    {
        LastFrame = frame;
        if (Run == null) return;
        AnomalyResponseHandler anomalies = Vehicle.FlightControl?.Anomalies;
        Hud.ShowTelemetry(frame, anomalies?.IsTelemetryGlitched ?? false);
        Hud.ShowAnomalies(anomalies?.ActiveAnomalies, Vehicle.FlightControl?.InputHandler?.AbortHoldProgress ?? 0f);
        Hud.ShowStageText(Vehicle.Staging.DescribeNextStage());
        if (anomalies?.ConsumeMessage() is string rebootLine) Hud.ShowWarning(rebootLine);
        if (Vehicle.FlightControl?.ThrottleController?.ConsumeWarning() is string relight) Hud.ShowWarning(relight);
        FlightControlSubsystem control = Vehicle.FlightControl;
        if (control?.AttitudeController != null && control.Estimator != null)
            Hud.ShowSasState(control.AttitudeController.Mode, control.Estimator.GetEnabledSasModes());
        if (_hasEnded || _segmentDone) return;

        // PoweredAscent <-> Coast by whether an engine burns.
        if (Phase == FlightPhase.PoweredAscent || Phase == FlightPhase.Coast)
            SetPhase(AnyEngineIgnited() ? FlightPhase.PoweredAscent : FlightPhase.Coast);

        Environment?.UpdateSky(frame.AltitudeM, Vehicle.LastOutput.Air.Density);
        if (Run.CurrentKind == SegmentKind.EarthAscent) CheckAscent(frame);
        else if (Run.CurrentKind == SegmentKind.MarsEdl)
        {
            if (Phase == FlightPhase.Entry && (AnyChuteDeployed() || AnyEngineIgnited())) SetPhase(FlightPhase.Descent);
            CheckChuteHint(frame);
        }
    }

    /// <summary>Once per EDL: tells the player when the stowed chute can open (B8 limits), and to drop the fairing first.</summary>
    private void CheckChuteHint(TelemetryFrame frame)
    {
        if (_chuteHintShown) return;
        ParachutePart chute = Vehicle.PartTree.GetParts<ParachutePart>().Find(c => c.IsAttached && c.ChuteState == ParachuteState.Stowed);
        ParachutePartDefinition def = chute?.ChuteDefinition;
        if (def == null) return;
        if (frame.RadarAltitudeM > ChuteHintAltitudeM) return;
        if (frame.MachNumber >= def.MaxDeployMach || frame.DynamicPressure >= def.MaxDeployDynamicPressure) return;
        _chuteHintShown = true;
        bool fairing = Vehicle.PartTree.GetParts<FairingPart>().Exists(f => f.IsAttached);
        Hud.ShowWarning(fairing ? "Parachute ready: jettison the fairing (F), then press P" : "Parachute ready: press P");
    }

    private bool AnyChuteDeployed()
    {
        foreach (ParachutePart chute in Vehicle.PartTree.GetParts<ParachutePart>())
            if (chute.IsAttached && chute.ChuteState == ParachuteState.Deployed) return true;
        return false;
    }

    /// <summary>Segment 1 rules: orbit reached, or nothing left to burn while falling.</summary>
    private void CheckAscent(TelemetryFrame frame)
    {
        MissionSegmentDefinition segment = Run.CurrentSegment;
        if (segment != null && Phase != FlightPhase.PreLaunch &&
            frame.AltitudeM >= segment.TargetAltitudeM && frame.HorizontalSpeedMps >= segment.TargetSpeedMps)
        {
            CompleteSegment(FlightPhase.OrbitReached, "Parking orbit reached!");
            return;
        }

        if (Phase == FlightPhase.PreLaunch || Vehicle.IsOnPad || frame.VerticalSpeedMps >= 0f || CanStillThrust())
        {
            _outOfThrustTimer = 0f;
            return;
        }
        _outOfThrustTimer += (float)GetPhysicsProcessDeltaTime();
        if (_outOfThrustTimer < OutOfThrustDelaySec) return;
        BlackBox.RecordFailure(FailureCause.TargetNotReached, null);
        Hud.ShowWarning("Out of thrust before reaching orbit");
        EndMission(MissionOutcome.Failed);
    }

    /// <summary>True if an engine burns, an armed liquid engine can relight, or a later stage arms an engine with propellant.</summary>
    public bool CanStillThrust()
    {
        PartTree tree = Vehicle.PartTree;
        if (tree == null) return false;
        foreach (EnginePart engine in tree.GetParts<EnginePart>())
        {
            if (!engine.IsAttached) continue;
            if (engine.IsIgnited && engine.State != ComponentState.Broken) return true;
            if (engine is LiquidEnginePart liquid && liquid.CanStillFire()) return true;
        }
        var stages = Vehicle.Staging.Stages;
        for (int i = Vehicle.Staging.CurrentStageIndex; i < stages.Count; i++)
        {
            foreach (EnginePart engine in stages[i].EnginesToArm)
            {
                if (!engine.IsAttached || engine.State == ComponentState.Broken) continue;
                if (engine is SolidBoosterPart solid && solid.RemainingPropellant > 0f) return true;
                if (engine is LiquidEnginePart liquid && liquid.RemainingIgnitions > 0 && liquid.GetAvailableFuel() > 0f) return true;
            }
        }
        return false;
    }

    /// <summary>Debug builds only: K ends the current flight segment as reached (to test cruise events quickly).</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!OS.IsDebugBuild() || Run == null || _hasEnded || _segmentDone) return;
        if (@event is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.K })
        {
            CompleteSegment(FlightPhase.OrbitReached, "[DEBUG K] Skipped segment");
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>The segment ended well: cut the throttle, lock input and hand over to GameFlowManager.</summary>
    private void CompleteSegment(FlightPhase phase, string message)
    {
        _segmentDone = true;
        InputHandler?.SetInputEnabled(false);
        Vehicle.FlightControl.ThrottleController?.SetThrottle(0f);
        foreach (EnginePart engine in Vehicle.PartTree.GetParts<EnginePart>())
            if (engine is LiquidEnginePart liquid && liquid.IsIgnited) liquid.Shutdown();
        SetPhase(phase);
        Hud.ShowWarning(message);
        if (DebugLog) GD.Print($"[Control] Segment {Run.SegmentIndex} completed ({phase}), max alt {BlackBox.MaxAltitude:0} m");
        EmitSignal(SignalName.SegmentCompleted, Run);
    }

    private void OnStageActivated(int index)
    {
        if (index == 0 && Phase == FlightPhase.PreLaunch) SetPhase(FlightPhase.PoweredAscent);
    }

    private bool AnyEngineIgnited()
    {
        foreach (EnginePart engine in Vehicle.PartTree.GetParts<EnginePart>())
            if (engine.IsIgnited) return true;
        return false;
    }

    /// <summary>Called for every cut of the part tree.</summary>
    public void OnPartSevered(RocketPart part, SeverCause cause)
    {
        // TODO(C5): pod lost -> TippedOver/Collision handling per 4.4.
    }

    /// <summary>FailureDetector.BreakTriggered handler: records the failure and ends the run.</summary>
    public void OnBreakTriggered(RocketPart part, FailureCause cause)
    {
        if (_hasEnded || _segmentDone || Run == null) return;
        BlackBox.RecordFailure(cause, part);
        _failPart = part;
        Hud.ShowWarning($"Failed: {cause} at {part?.Definition?.DisplayName ?? part?.ModelId}");
        EndMission(MissionOutcome.Failed);
    }

    /// <summary>Called by VehicleRoot when the lowest part reaches altitude 0.</summary>
    public void OnGroundContact(RocketPart part, float contactSpeed)
    {
        if (_hasEnded || _segmentDone || Run == null) return;
        if (Run.CurrentKind == SegmentKind.EarthAscent)
        {
            // Segment 1: touching the ground after lift-off always ends the run.
            _failPart = part;
            bool broke = Coordinator.Failures.MonitorImpact(part, contactSpeed); // a break also calls OnBreakTriggered
            if (!broke)
            {
                BlackBox.RecordFailure(FailureCause.TargetNotReached, part);
                EndMission(MissionOutcome.Failed);
            }
            return;
        }
        if (Run.CurrentKind != SegmentKind.MarsEdl) return;

        // Segment 3: too fast breaks the part (BreakTriggered ends the run as Collision).
        if (Coordinator.Failures.MonitorImpact(part, contactSpeed)) return;
        float tilt = Mathf.Abs(FlightMath.WrapDeg(Vehicle.LastFrame.PitchDeg - 90f));
        float maxTilt = Run.CurrentSegment?.MaxLandingTiltDeg ?? 15f;
        bool podOk = Vehicle.PartTree.RootPart is CommandPodPart pod && pod.IsAttached && pod.State != ComponentState.Broken;
        if (!podOk || tilt > maxTilt)
        {
            BlackBox.RecordFailure(FailureCause.TippedOver, part);
            Hud.ShowWarning(podOk ? $"Tipped over on landing: tilt {tilt:0}° (max {maxTilt:0}°)" : "Command pod lost");
            EndMission(MissionOutcome.Failed);
            return;
        }
        BlackBox.LandingSpeedMps = contactSpeed;
        Hud.ShowWarning($"Landed! {contactSpeed:0.0} m/s");
        Run.Advance(); // the landing completes segment 3
        EndMission(MissionOutcome.Success);
    }

    /// <summary>Player abort (pause menu → Quit flight).</summary>
    public void Abort()
    {
        if (_hasEnded || _segmentDone || Run == null) return;
        BlackBox.RecordFailure(FailureCause.Aborted, null);
        EndMission(MissionOutcome.Aborted);
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
        Vehicle.FlightControl.ThrottleController?.SetThrottle(0f);
        SetPhase(outcome == MissionOutcome.Success ? FlightPhase.Landed : FlightPhase.Failed);
        if (DebugLog) GD.Print($"[Control] Mission ended: {outcome}, cause {BlackBox.FailureCause}, max alt {BlackBox.MaxAltitude:0} m");
        FlightResult result = Run.BlackBox.BuildResult(outcome);

        if (outcome != MissionOutcome.Failed || FailRevealDelaySeconds <= 0f)
        {
            Vehicle.SetSimulationFrozen(true);
            EmitSignal(SignalName.MissionEnded, result);
            return;
        }
        // Failure: explode, let the broken parts fly for a moment, then show the result.
        bool onGround = Vehicle.LastFrame.RadarAltitudeM < 3f;
        if (onGround) Vehicle.SetSimulationFrozen(true);
        Vector3 at = FindPartPosition(_failPart) ?? Vehicle.GetWorldCenterOfMass();
        Node world = GetTree().CurrentScene ?? GetParent();
        RocketGame.Presentation.ExplosionEffect.Spawn(world, at, ExplosionScale, ExplosionScene);
        EmitSignal(SignalName.ExplosionSpawned, at, 1f);
        GetTree().CreateTimer(FailRevealDelaySeconds).Timeout += () =>
        {
            if (!IsInstanceValid(this)) return;
            Vehicle.SetSimulationFrozen(true);
            EmitSignal(SignalName.MissionEnded, result);
        };
    }

    /// <summary>World position of the visual of part (it may have become debris), or null.</summary>
    private Vector3? FindPartPosition(RocketPart part)
    {
        if (part == null) return null;
        Node scene = GetTree().CurrentScene;
        return scene == null ? null : Find(scene);

        Vector3? Find(Node n)
        {
            if (n is RocketGame.Presentation.PartVisual v && v.Part == part) return v.GlobalPosition;
            foreach (Node c in n.GetChildren())
                if (Find(c) is Vector3 p) return p;
            return null;
        }
    }
}
