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
        Hud.ShowMessage("Press \"Transfer burn\" (or Z). You can stage (Space) and open the solar panels (G) first.");
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
            Hud.ShowMessage("Solar panels are closed and the battery will run out on the way. Press G to open them, then burn; press burn again to skip.");
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
        Hud.ShowMessage("Transfer burn done. On the way to Mars...");
        if (DebugLog) GD.Print($"[Control] Transfer burn done, delta-v left {_sim.GetRemainingDeltaV():0} m/s");
    }

    /// <summary>Advances simulated time by deltaTime real seconds.</summary>
    public void Advance(float deltaTime)
    {
        if (!IsActive || !HasBurned || IsPaused || _sim == null) return;
        _dayAccumulator += deltaTime * DaysPerSecond * TimeScale;
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

            if (_run.PartTree?.RootPart is CommandPodPart pod && pod.State == ComponentState.Broken)
            {
                Hud.ShowMessage("Command pod lost to temperature");
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
            if (open + closed > 0) Hud.ShowMessage(open > 0 ? $"{open} solar panel(s) open." : "Solar panels folded.");
        }
        if (input.ThrottleFull) RequestTransferBurn();
        if (input.AbortPressed) EndMission(FailureCause.Aborted, null, MissionOutcome.Aborted);
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
        if (step.Events == null) return;
        foreach (FlightEvent e in step.Events)
        {
            Hud.ShowMessage($"Day {step.Day}: {e.Type}");
            _sim.Acknowledge(e.Type);
            if (DebugLog) GD.Print($"[Control] Cruise event day {step.Day}: {e.Type}");
        }
    }

    /// <summary>Solar arrays on attached pods that are (or are not) deployed.</summary>
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
        FlightEventType.PowerBrownout => "brownout, battery empty (open the solar panels with G)",
        FlightEventType.ComponentOverheat => "component overheating",
        FlightEventType.DebrisWarning => "debris warning",
        FlightEventType.SeuGlitch => "radiation glitch in the computer",
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
