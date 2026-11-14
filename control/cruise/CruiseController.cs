using Godot;
using RocketGame.Core;
using RocketGame.Physics;

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

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Vehicle, nameof(Vehicle))) return;
        if (!NodeGuard.Require(this, Hud, nameof(Hud))) return;
        if (!NodeGuard.Require(this, InputHandler, nameof(InputHandler))) return;
        if (Settings != null) DaysPerSecond = Settings.CruiseDaysPerSecond;
        SetProcess(false);
    }

    /// <summary>Starts segment 2 on the same vehicle.</summary>
    public void StartCruise(MissionRun run)
    {
        _run = run;
        IsActive = true;
        HasBurned = false;
        IsPaused = false;
        _dayAccumulator = 0f;
        // TODO(C6): Vehicle.SetSimulationFrozen(true); hide FlightHud, show Hud; _sim = new CruiseSimulator();
        //           _sim.Begin(run.PartTree, run.CurrentSegment, origin body, destination body); SetProcess(true).
    }

    /// <summary>Player asked for the transfer burn. Fails the run with InsufficientDeltaV if it cannot be done.</summary>
    public void RequestTransferBurn()
    {
        // TODO(C6): _sim.ExecuteTransferBurn(); false -> EndMission(InsufficientDeltaV).
    }

    /// <summary>Advances simulated time by deltaTime real seconds.</summary>
    public void Advance(float deltaTime)
    {
        // TODO(C6): accumulate deltaTime * DaysPerSecond * TimeScale; StepDay() per whole day; events, end checks.
    }

    /// <summary>Stops or resumes the day clock.</summary>
    public void SetPaused(bool paused)
    {
        IsPaused = paused;
    }

    public override void _Process(double delta)
    {
        if (!IsActive || IsPaused) return;
        Advance((float)delta);
    }
}
