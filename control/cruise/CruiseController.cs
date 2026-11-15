using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Physics;
using RocketGame.Research;

namespace RocketGame.Control;

/// <summary>
/// Segment 2: transfer burn, then fast-forward day by day with CruiseSimulator (C_CONTROL.md 6).
/// Lives in flight.tscn; the vehicle is frozen meanwhile.
/// </summary>
public partial class CruiseController : Node
{
    [Signal] public delegate void SegmentCompletedEventHandler(MissionRun run);
    [Signal] public delegate void MissionEndedEventHandler(FlightResult result);

    [ExportGroup("Links")]
    [Export] public VehicleRoot Vehicle { get; set; }
    [Export] public CruiseHUD Hud { get; set; }
    [Export] public FlightHUD FlightHud { get; set; }
    /// <summary>Named InputHandler (not Input) so it does not hide Godot.Input.</summary>
    [Export] public InputHandler InputHandler { get; set; }
    [Export] public StagingManager Staging { get; set; }
    [Export] public AnomalyResponseHandler Anomalies { get; set; }
    [Export] public FlightSettings Settings { get; set; }

    /// <summary>Simulated days per real second at x1. Defaults to Settings.CruiseDaysPerSecond in _Ready.</summary>
    [ExportGroup("Time")]
    [Export(PropertyHint.Range, "0.1,50,0.1")] public float DaysPerSecond { get; set; } = 3.5f;

    [ExportGroup("Debug")]
    [Export] public bool DebugLog { get; set; }

    /// <summary>True while time is stopped (menus, anomalies).</summary>
    public bool IsPaused { get; private set; }
    /// <summary>True between StartCruise and the end of the segment.</summary>
    public bool IsActive { get; private set; }
    /// <summary>True once the transfer burn succeeded.</summary>
    public bool HasBurned { get; private set; }
    /// <summary>Speed multiplier chosen in the HUD (1 or 4).</summary>
    public float TimeScale { get; set; } = 1f;

    private MissionRun _run;
    private CruiseSimulator _sim;
    private float _dayAccumulator;
    private bool _solarWarned;
    // Phase 2 meteor dodge: > 0 while the player must press the dodge key.
    private float _dodgeTimer;
    /// <summary>Seconds the day counter stays stopped after an event so the player can read and react.</summary>
    [Export] public float EventHoldSeconds { get; set; } = 4f;
    private float _holdTimer;
    private bool _waitForEnter;
    /// <summary>Player chose storm shelter (H) after a flare warning. B: read this when applying flare damage.</summary>
    public bool StormShelter { get; set; }
    /// <summary>Seconds left of the slowed-down time after a flare warning.</summary>
    private float _slowTimer;
    [Export] public float FlareWarningSlowSeconds { get; set; } = 8f;
    [Export(PropertyHint.Range, "0.01,1,0.01")] public float FlareWarningTimeFactor { get; set; } = 0.1f;
    private int _dodgeDir; // -1 left (A/Q), +1 right (D/E)
    private readonly RandomNumberGenerator _rng = new();

    /// <summary>Lead time (s) without a comms instrument.</summary>
    [Export] public float BaseDodgeSeconds { get; set; } = 3f;
    [Export] public float AutoDodgeEnergyWh { get; set; } = 30f;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Vehicle, nameof(Vehicle))) return;
        if (!NodeGuard.Require(this, Hud, nameof(Hud))) return;
        if (!NodeGuard.Require(this, InputHandler, nameof(InputHandler))) return;
        if (Settings != null) DaysPerSecond = Settings.CruiseDaysPerSecond;
        Hud.TransferBurnRequested += RequestTransferBurn;
        Hud.PauseToggled += SetPaused;
        Hud.TimeScaleRequested += OnTimeScaleRequested;
        SetProcess(false);
    }

    /// <summary>Starts segment 2 on the same vehicle.</summary>
    public void StartCruise(MissionRun run)
    {
        _run = run;
        IsActive = true;
        HasBurned = false;
        IsPaused = false;
        TimeScale = 1f;
        _dayAccumulator = 0f;
        _solarWarned = false;
        _waitForEnter = false;
        _holdTimer = 0f;
        _slowTimer = 0f;
        StormShelter = false;
        _dodgeTimer = 0f;

        Vehicle.SetSimulationFrozen(true);
        InputHandler.SetInputEnabled(true);
        if (FlightHud != null) FlightHud.Visible = false;
        Hud.Visible = true;

        MissionDefinition mission = run.Mission;
        CelestialBodyDefinition origin = mission != null && mission.Segments.Count > 0 ? mission.Segments[0].Body : null;
        CelestialBodyDefinition destination = mission != null && mission.Segments.Count > 2 ? mission.Segments[2].Body : origin;
        _sim = new CruiseSimulator { Settings = Vehicle.Coordinator?.Settings };
        _sim.Begin(run.PartTree, run.CurrentSegment, origin, destination);

        Hud.ResetControls();
        Hud.ShowProgress(0, TotalDays);
        RefreshDeltaV();
        Hud.ShowMessage("Press \"Transfer burn\" (or Z). You can stage (Space) and open solar panels (G) first.");
        SetProcess(true);
        if (DebugLog) GD.Print($"[Control] Cruise started: {TotalDays} days, delta-v {_sim.GetRemainingDeltaV():0} / {RequiredDeltaV:0} m/s");
    }

    /// <summary>Player asked for the transfer burn. Fails the run with InsufficientDeltaV if it cannot be done.</summary>
    public void RequestTransferBurn()
    {
        if (!IsActive || HasBurned || _sim == null) return;
        // B5: arrays only make power when deployed. Warn once; a second press burns anyway.
        if (!_solarWarned && CountSolarArrays(deployed: false) > 0)
        {
            _solarWarned = true;
            Hud.ShowMessage("Solar panels are closed, power will run out on the way. Press G to open them, then burn; press burn again to skip.");
            return;
        }
        float before = _sim.GetRemainingDeltaV();
        if (!_sim.ExecuteTransferBurn())
        {
            Hud.ShowMessage($"Not enough delta-v: {before:0} m/s left, {RequiredDeltaV:0} m/s needed");
            EndMission(FailureCause.InsufficientDeltaV);
            return;
        }
        HasBurned = true;
        Hud.SetBurnDone(true);
        RefreshDeltaV();
        Hud.ShowMessage("Transfer burn done. Flying to Mars...");
        if (DebugLog) GD.Print($"[Control] Transfer burn done, delta-v left {_sim.GetRemainingDeltaV():0} m/s");
    }

    /// <summary>Advances simulated time by deltaTime real seconds.</summary>
    public void Advance(float deltaTime)
    {
        if (!IsActive || !HasBurned || IsPaused || _sim == null || _dodgeTimer > 0f || _holdTimer > 0f || _waitForEnter) return;
        float slow = 1f;
        if (_slowTimer > 0f)
        {
            _slowTimer -= deltaTime;
            slow = FlareWarningTimeFactor;
        }
        _dayAccumulator += deltaTime * DaysPerSecond * TimeScale * slow;
        while (_dayAccumulator >= 1f && IsActive)
        {
            _dayAccumulator -= 1f;
            CruiseStepResult step = _sim.StepDay();
            _run.ElapsedDays = step.Day;
            _run.BlackBox.CruiseDaysSurvived = step.Day;
            Hud.ShowProgress(step.Day, TotalDays);
            Hud.ShowResources(step);
            Hud.ShowDeltaV(step.DeltaVRemaining, RequiredDeltaV);
            HandleEvents(step);
            if (_dodgeTimer > 0f || _holdTimer > 0f || _waitForEnter) _dayAccumulator = 0f; // stop here until the player reacts

            if (_run.PartTree?.RootPart is CommandPodPart pod && pod.State == ComponentState.Broken)
            {
                Hud.ShowMessage("Command pod failed from heat");
                EndMission(FailureCause.Thermal, pod);
                return;
            }
            if (step.Day >= TotalDays)
            {
                Finish();
                return;
            }
        }
    }

    /// <summary>Stops or resumes the day clock.</summary>
    public void SetPaused(bool paused)
    {
        IsPaused = paused;
    }

    public override void _Process(double delta)
    {
        if (!IsActive) return;
        PilotInput input = InputHandler.Poll();
        if (input.StagePressed && Staging != null)
        {
            Staging.ActivateNextStage();
            Callable.From(RefreshDeltaV).CallDeferred(); // cuts are processed deferred
        }
        if (input.DeployTogglePressed)
        {
            Vehicle.FlightControl?.ToggleDeployables();
            int open = CountSolarArrays(deployed: true), closed = CountSolarArrays(deployed: false);
            if (open + closed > 0) Hud.ShowMessage(open > 0 ? $"Opened {open} solar panel(s)." : "Solar panels folded.");
        }
        if (input.ThrottleFull) RequestTransferBurn();
        if (input.SealLeakPressed && _sim != null && _sim.HasLeak)
        {
            _sim.SealLeak();
            Hud.ShowMessage("Leak sealed.");
        }
        if (input.AbortPressed) EndMission(FailureCause.Aborted, null, MissionOutcome.Aborted);
        if (Anomalies?.ConsumeMessage() is string rebootLine) Hud.ShowMessage(rebootLine);
        if (_holdTimer > 0f) _holdTimer -= (float)delta;
        if (_dodgeTimer > 0f)
        {
            _dodgeTimer -= (float)delta;
            if (_dodgeTimer <= 0f) MeteorHit("Too late to dodge!");
        }
        if (!IsPaused) Advance((float)delta);
    }

    private int TotalDays => Mathf.Max(1, Mathf.RoundToInt(_run?.CurrentSegment?.DurationDays ?? 1f));
    private float RequiredDeltaV => _run?.CurrentSegment?.RequiredDeltaVMps ?? 0f;

    private void RefreshDeltaV()
    {
        if (_sim != null && IsActive) Hud.ShowDeltaV(_sim.GetRemainingDeltaV(), RequiredDeltaV);
    }

    /// <summary>P0: cruise events are warnings only (C7 adds the key responses), then acknowledged.</summary>
    private void HandleEvents(CruiseStepResult step)
    {
        _lastBroken.Clear();
        if (step.BrokenComponents != null) _lastBroken.AddRange(step.BrokenComponents);
        if (step.DamagedModules != null) _lastBroken.AddRange(step.DamagedModules);
        if (step.Events == null) return;
        foreach (FlightEvent e in step.Events)
        {
            _sim.Acknowledge(e.Type);
            if (HandlePhase2Event(step.Day, e))
            {
                if (e.Type == FlightEventType.SolarFlare && e.Severity > 0f) _holdTimer = EventHoldSeconds; // time to read the damage
                continue;
            }
            Hud.ShowMessage($"Day {step.Day}: {EventText(e)}");
            if (DebugLog) GD.Print($"[Control] Cruise event day {step.Day}: {e.Type}");
        }
        if (step.DamagedModules is { Count: > 0 })
            Hud.ShowMessage($"Day {step.Day}: equipment failed: {string.Join(", ", step.DamagedModules)}");
    }

    /// <summary>B11/B12: text of a cruise event, with the flare warning (Severity 0) and the damaged parts.</summary>
    public static string EventText(FlightEvent e) => e.Type switch
    {
        FlightEventType.SolarFlare when e.Severity < 1f => $"sensor {e.SourceId} warns of an incoming solar storm",
        FlightEventType.SolarFlare => "solar storm" + (string.IsNullOrEmpty(e.SourceId) ? ", no equipment failed" : ""),
        FlightEventType.InstrumentFailure => $"equipment fault {e.SourceId}",
        FlightEventType.CryoLeak => $"tank {e.SourceId} is leaking (press V to seal)",
        FlightEventType.HullBreach => "pod hull breach (press V to seal)",
        FlightEventType.EngineFlameout => $"engine {e.SourceId} cut out mid-burn, reignited",
        FlightEventType.MeteorField => "meteoroid field ahead",
        _ => EventText(e.Type),
    };

    /// <summary>Solar arrays on attached pods that are (or are not) deployed.</summary>
    // ------------------------------------------------------------------ Phase 2: flare, meteors

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsActive || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (_waitForEnter && key.PhysicalKeycode is Key.Enter or Key.KpEnter)
        {
            _waitForEnter = false;
            _holdTimer = 0f;
            Hud.ShowMessage("Continuing the journey...");
            GetViewport().SetInputAsHandled();
            return;
        }
        if (OS.IsDebugBuild() && key.PhysicalKeycode == Key.K && !HasBurned)
        {
            HasBurned = true; // debug: skip the transfer burn (no fuel used)
            Hud.SetBurnDone(true);
            Hud.ShowMessage("[DEBUG K] Skipped transfer burn");
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.PhysicalKeycode == Key.H && _sim != null && HasBurned)
        {
            StormShelter = !StormShelter;
            _sim.PowerDownForFlare(StormShelter);
            Hud.ShowMessage(StormShelter ? "Storm shelter mode: all equipment off to avoid radiation" : "Equipment back on");
            GetViewport().SetInputAsHandled();
        }
        else if (_dodgeTimer > 0f)
        {
            int dir = key.PhysicalKeycode is Key.A or Key.Q ? -1 : key.PhysicalKeycode is Key.D or Key.E ? 1 : 0;
            if (dir == 0) return;
            bool rcs = key.PhysicalKeycode is Key.Q or Key.E;
            if (rcs && Pod?.GetModules<RcsThrusterModule>().Exists(m => m.CanOperate()) != true)
            {
                Hud.ShowMessage("No working RCS, using A/D (main engine)");
                return;
            }
            _dodgeTimer = 0f;
            if (dir == _dodgeDir) Hud.ShowMessage("Dodged! The meteoroid flew past.");
            else MeteorHit("Dodged the wrong way!");
            GetViewport().SetInputAsHandled();
        }
    }

    private readonly System.Collections.Generic.List<string> _lastBroken = new();

    private CommandPodPart Pod => PowerSolver.FindPod(_run?.PartTree);

    /// <summary>True when the event is a phase 2 event and was handled here.</summary>
    private bool HandlePhase2Event(int day, FlightEvent e)
    {
        if (e.Type == FlightEventType.SolarFlare)
        {
            if (e.Severity <= 0f)
            {
                _slowTimer = FlareWarningSlowSeconds;
                Hud.ShowMessage($"Day {day}: sensors warn of an incoming SOLAR STORM! Press H to shelter.");
                return true;
            }
            StormShelter = false;
            var broke = _lastBroken;
            Hud.ShowMessage(broke.Count == 0
                ? $"Day {day}: the solar storm passed, no equipment failed."
                : $"Day {day}: the solar storm damaged {string.Join(", ", broke)}. Press B to restart.");
            return true;
        }
        if (e.Type != FlightEventType.MeteorField) return false;

        float lead = BaseDodgeSeconds;
        bool canAuto = false;
        CommandPodPart pod = Pod;
        if (pod != null)
            foreach (InstrumentModule m in pod.GetModules<InstrumentModule>())
                if (m.CanOperate() && m.Definition is InstrumentModuleDefinition d && d.Kind == InstrumentKind.Comms)
                {
                    lead = Mathf.Max(lead, d.WarningLeadSeconds);
                    canAuto |= d.AllowsAutoDodge;
                }
        bool hasRcs = pod?.GetModules<RcsThrusterModule>().Exists(m => m.CanOperate()) == true;
        if (canAuto && hasRcs && PowerSolver.TryDrawEnergy(pod, AutoDodgeEnergyWh))
        {
            Hud.ShowMessage($"Day {day}: METEOROID ZONE - antenna and RCS dodged automatically ({AutoDodgeEnergyWh:0} Wh).");
            return true;
        }
        _dodgeDir = _rng.Randf() < 0.5f ? -1 : 1;
        _dodgeTimer = lead / Mathf.Max(TimeScale, 1f) + 0.5f;
        string side = _dodgeDir < 0 ? "LEFT (A or Q)" : "RIGHT (D or E)";
        Hud.ShowMessage($"Day {day}: METEOROID incoming! Dodge {side} within {lead:0} s");
        return true;
    }

    private void MeteorHit(string prefix)
    {
        _dodgeTimer = 0f;
        var targets = new System.Collections.Generic.List<RocketPart>();
        foreach (RocketPart p in _run?.PartTree?.ActiveParts ?? new())
            if (p is FuelTankPart or CommandPodPart) targets.Add(p);
        if (targets.Count == 0) return;
        RocketPart hit = targets[_rng.RandiRange(0, targets.Count - 1)];
        string name = string.IsNullOrEmpty(hit.Definition?.DisplayName) ? hit.ModelId : hit.Definition.DisplayName;
        bool damaged = MeteorImpact.Resolve(hit, _rng.RandfRange(4f, 30f));
        Hud.ShowMessage(damaged ? $"{prefix} Meteoroid damaged {name} (B to restart broken equipment)" : $"{prefix} Meteoroid hit {name}, the hull held");
        RefreshDeltaV();
    }

    private int CountSolarArrays(bool deployed)
    {
        int count = 0;
        foreach (CommandPodPart pod in Vehicle?.PartTree?.GetParts<CommandPodPart>() ?? new())
        {
            if (!pod.IsAttached) continue;
            foreach (SolarArrayModule solar in pod.GetModules<SolarArrayModule>())
                if (solar.IsDeployed == deployed && solar.IsHealthy) count++;
        }
        return count;
    }

    /// <summary>Player-facing text for a cruise event.</summary>
    public static string EventText(FlightEventType type) => type switch
    {
        FlightEventType.PowerBrownout => "power lost, battery empty (open solar panels with G)",
        FlightEventType.ComponentOverheat => "component overheating",
        FlightEventType.DebrisWarning => "debris warning",
        FlightEventType.SeuGlitch => "radiation fault in the computer",
        _ => type.ToString(),
    };

    private void OnTimeScaleRequested(float scale) => TimeScale = Mathf.Max(scale, 0.1f);

    /// <summary>Arrived: hand the run back for segment 3.</summary>
    private void Finish()
    {
        IsActive = false;
        SetProcess(false);
        Hud.Visible = false;
        if (FlightHud != null) FlightHud.Visible = true;
        if (DebugLog) GD.Print($"[Control] Cruise completed after {_run.ElapsedDays:0} days");
        EmitSignal(SignalName.SegmentCompleted, _run);
    }

    private void EndMission(FailureCause cause, RocketPart part = null, MissionOutcome outcome = MissionOutcome.Failed)
    {
        if (!IsActive) return;
        IsActive = false;
        SetProcess(false);
        InputHandler.SetInputEnabled(false);
        _run.BlackBox.RecordFailure(cause, part);
        if (DebugLog) GD.Print($"[Control] Cruise ended: {cause}");
        EmitSignal(SignalName.MissionEnded, _run.BlackBox.BuildResult(outcome));
    }
}
