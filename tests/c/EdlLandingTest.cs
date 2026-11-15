using Godot;
using RocketGame.Building;
using RocketGame.Control;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Tests.C;

/// <summary>
/// C6 EDL autopilot: the Mars stack from segment 2, flipped heat shield first, then fairing off, chute open,
/// heat shield dropped with the lander engine armed, and a vertical-speed throttle loop down to the ground.
/// Run through GameFlowManager so a successful touchdown shows the Victory screen.
/// </summary>
public partial class EdlLandingTest : Node
{
    [Export] public PackedScene FlightScene { get; set; }
    [Export] public MissionDefinition FallbackMission { get; set; }
    /// <summary>Stages fired before the jump so only the lander stage (drop shield, arm engine) is left.</summary>
    [Export] public int PreActivatedStages { get; set; } = 4;
    /// <summary>Chute opens below this radar altitude (and inside its Mach/q limits), like Mars 2020 at ~11 km.</summary>
    [Export] public float ChuteAltitudeM { get; set; } = 11000f;
    /// <summary>Radar altitude where the heat shield is dropped and the lander engine armed.</summary>
    [Export] public float PoweredDescentAltitudeM { get; set; } = 2000f;
    /// <summary>Descent rate wanted at the ground.</summary>
    [Export] public float TouchdownSpeedMps { get; set; } = 1.5f;
    /// <summary>Braking the profile plans for: wanted rate = sqrt(2 * a * height).</summary>
    [Export] public float PlannedBrakingMps2 { get; set; } = 1.5f;
    /// <summary>Throttle change per second per m/s of rate error.</summary>
    [Export] public float ThrottleGain { get; set; } = 0.08f;

    private FlightScene _scene;
    private VehicleRoot _vehicle;
    private int _step;
    private float _time, _logTimer;

    public override void _Ready() => Callable.From(Begin).CallDeferred();

    private void Begin()
    {
        GD.Print("[Test C6] EdlLandingTest start");
        AssembledRocket rocket = TestRocketFactory.CreateMarsStack();
        GameFlowManager flow = GameFlowManager.Instance;
        if (rocket == null || flow == null)
        {
            GD.PushError("[Test C6] needs PartCatalog and GameFlowManager autoloads");
            return;
        }
        MissionDefinition mission = ResearchCampaignEngine.Instance?.CurrentMission ?? FallbackMission;
        _scene = flow.StartRun(new MissionRun(mission, rocket));
        if (_scene == null) return;
        for (int i = 0; i < PreActivatedStages; i++) _scene.Vehicle.Staging.ActivateNextStage();
        flow.DebugJumpToSegment(2);
        _vehicle = _scene.Vehicle;
        _scene.Director.MissionEnded += OnMissionEnded;
        FlipHeatShieldForward();
        _step = 1;
    }

    /// <summary>What the player does first: turn 180 degrees about the CoM so the heat shield leads.</summary>
    private void FlipHeatShieldForward()
    {
        Vector3 com = _vehicle.GetWorldCenterOfMass();
        var turn = new Transform3D(new Basis(Vector3.Back, Mathf.Pi), Vector3.Zero);
        Transform3D t = _vehicle.GlobalTransform;
        t = new Transform3D(turn.Basis * t.Basis, com + turn.Basis * (t.Origin - com));
        _vehicle.GlobalTransform = t;
        PhysicsServer3D.BodySetState(_vehicle.GetRid(), PhysicsServer3D.BodyState.Transform, t);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_step == 0 || _vehicle == null) return;
        float dt = (float)delta;
        _time += dt;
        TelemetryFrame f = _vehicle.LastFrame;
        StagingManager staging = _vehicle.Staging;
        FlightControlSubsystem control = _vehicle.FlightControl;

        _logTimer -= dt;
        if (_logTimer <= 0f)
        {
            _logTimer = 5f;
            GD.Print($"[Test C6] t {_time:0} s  alt {f.RadarAltitudeM:0} m  vy {f.VerticalSpeedMps:0.0}  vx {f.HorizontalSpeedMps:0}  " +
                     $"Mach {f.MachNumber:0.00}  q {f.DynamicPressure:0} Pa  pitch {f.PitchDeg:0}  thr {f.Throttle:0.00}  phase {_scene.Director.Phase}");
        }

        ParachutePart chute = _vehicle.PartTree.GetParts<ParachutePart>().Find(c => c.IsAttached);
        switch (_step)
        {
            case 1 when _time > 0.5f:
                control.ToggleSas(); // hold the flipped attitude through the hot part
                _step = 2;
                break;
            case 2:
                ParachutePartDefinition def = chute?.ChuteDefinition;
                if (def == null || f.RadarAltitudeM > ChuteAltitudeM) break;
                if (f.MachNumber >= def.MaxDeployMach || f.DynamicPressure >= def.MaxDeployDynamicPressure) break;
                GD.Print($"[Test C6] chute window: Mach {f.MachNumber:0.00}, q {f.DynamicPressure:0} Pa, alt {f.RadarAltitudeM:0} m");
                staging.JettisonFairing();
                _step = 3;
                break;
            case 3:
                bool opened = staging.ToggleParachute();
                GD.Print($"[Test C6] chute deploy: {opened}");
                if (control.AttitudeController.Mode != SasMode.Off) control.ToggleSas(); // let the chute hang the vehicle
                _step = 4;
                break;
            case 4:
                if (f.RadarAltitudeM > PoweredDescentAltitudeM) break;
                GD.Print($"[Test C6] powered descent at {f.RadarAltitudeM:0} m, vy {f.VerticalSpeedMps:0.0}, pitch {f.PitchDeg:0}");
                staging.ActivateNextStage(); // drop the heat shield, arm the lander engine
                control.ThrottleController.SetThrottle(0.3f);
                control.ToggleSas();
                _step = 5;
                break;
            case 5:
                float wanted = -Mathf.Max(TouchdownSpeedMps, Mathf.Sqrt(2f * PlannedBrakingMps2 * Mathf.Max(f.RadarAltitudeM, 0f)));
                float throttle = control.ThrottleController.Throttle + ThrottleGain * (wanted - f.VerticalSpeedMps) * dt;
                control.ThrottleController.SetThrottle(Mathf.Clamp(throttle, 0.05f, 1f));
                break;
        }
    }

    private void OnMissionEnded(FlightResult result)
    {
        GD.Print($"[Test C6] mission ended: {result.Outcome}, cause {result.FailureCause} {result.FailedModelId}, " +
                 $"landing speed {result.LandingSpeedMps:0.0} m/s, segments {result.SegmentsCompleted}, time {result.FlightTime:0} s");
        GD.Print($"[Test C6] {(result.Outcome == MissionOutcome.Success ? "PASS" : "FAIL")} landed on Mars");
        _step = 0;
    }
}
