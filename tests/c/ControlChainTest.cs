using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Control;
using RocketGame.Core;
using RocketGame.Physics;
using RocketGame.Research;

namespace RocketGame.Tests.C;

/// <summary>
/// C4/C5 checks: run tests/c/control_chain_test.tscn (F6) and read the Output panel.
/// Part 1 checks the chain node by node; part 2 flies the minimal rocket with SAS on, kicks it into a spin
/// and checks that SAS stops the spin and holds the attitude (end-to-end torque sign).
/// </summary>
public partial class ControlChainTest : Node
{
    [Export] public PackedScene FlightScene { get; set; }
    [Export] public MissionDefinition Mission { get; set; }
    /// <summary>Spin given to the rocket in flight (deg/s).</summary>
    [Export] public float KickRateDegPerSec { get; set; } = 15f;

    private FlightScene _scene;
    private VehicleRoot _vehicle;
    private FlightControlSubsystem _control;
    private float _time;
    private int _step;
    private int _passed, _failed;
    private float _kickTime;
    private float _gimbalAfterKick;
    private float _maxRateAfterKick;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, FlightScene, nameof(FlightScene))) return;
        Callable.From(Begin).CallDeferred();
    }

    private void Begin()
    {
        GD.Print("[Test C] ControlChainTest start");
        MissionDefinition mission = ResearchCampaignEngine.Instance?.CurrentMission ?? Mission;
        var run = new MissionRun(mission, TestRocketFactory.CreateMinimal());
        _scene = FlightScene.Instantiate<FlightScene>();
        AddChild(_scene);
        _scene.Director.StartSegment(run);
        _vehicle = _scene.Vehicle;
        _control = _vehicle.FlightControl;
        CheckStatic();
    }

    /// <summary>Checks that need no flight.</summary>
    private void CheckStatic()
    {
        AttitudeEstimator est = _control.Estimator;
        AttitudeController sas = _control.AttitudeController;

        est.Update(90f, 0f, 1f / 60f);
        Check("IMU gives a valid attitude", est.HasValidAttitude);
        Check("imu_g1 allows StabilityAssist", est.GetEnabledSasModes().Has(SasMode.StabilityAssist));
        Check("imu_g1 does not allow Prograde", !est.GetEnabledSasModes().Has(SasMode.Prograde));
        Check("SetMode(Prograde) refused", !sas.SetMode(SasMode.Prograde));
        Check("SetMode(HoldPitch) refused", !sas.SetMode(SasMode.HoldPitch));
        Check("SetMode(StabilityAssist) accepted", sas.SetMode(SasMode.StabilityAssist) && sas.Mode == SasMode.StabilityAssist);

        sas.TargetPitchDeg = 90f;
        for (int i = 0; i < 30; i++) est.Update(80f, 0f, 1f / 60f);
        float u = sas.ComputeDemand(PilotInput.Empty, 1f / 60f);
        Check($"attitude below target gives u > 0 (u = {u:0.00})", u > 0f);
        var manual = new PilotInput { PitchAxis = 1f };
        Check("manual D gives u = -1", Mathf.IsEqualApprox(sas.ComputeDemand(manual, 1f / 60f), -1f));

        // Allocator signs: an ignited engine below the CoM gimbals with u.
        LiquidEnginePart engine = _vehicle.PartTree.GetParts<LiquidEnginePart>()[0];
        engine.IsArmed = true;
        engine.IsIgnited = true;
        _control.Allocator.Allocate(0.5f, 0f);
        float range = engine.EngineDefinition.GimbalRangeDeg;
        Check($"engine below CoM: gimbal = +u*range ({engine.GimbalAngleDeg:0.00} deg)", Mathf.IsEqualApprox(engine.GimbalAngleDeg, 0.5f * range));
        engine.IsIgnited = false;
        _control.Allocator.Allocate(0.5f, 0f);
        Check("engine off: gimbal 0", engine.GimbalAngleDeg == 0f);
        engine.IsArmed = false;

        // Sensor loss turns SAS off.
        var imu = ((CommandPodPart)_vehicle.PartTree.RootPart).GetModules<GyroscopeImuModule>()[0];
        imu.IsPowered = false;
        est.Update(90f, 0f, 1f / 60f);
        sas.ComputeDemand(PilotInput.Empty, 1f / 60f);
        Check("IMU unpowered: no valid attitude", !est.HasValidAttitude);
        Check("IMU unpowered: SAS back to Off", sas.Mode == SasMode.Off);
        Check("IMU unpowered: SetMode refused", !sas.SetMode(SasMode.StabilityAssist));
        imu.IsPowered = true;
        imu.AccumulatedDriftDeg = 0f;

        // Segment 1 thrust bookkeeping.
        Check("before launch: can still thrust (stage 0 left)", _scene.Director.CanStillThrust());
        Check("debrief text for TargetNotReached", DebriefScreen.CauseText(FailureCause.TargetNotReached) == "không đạt quỹ đạo");

        sas.SetMode(SasMode.Off);
        _step = 1;
        _time = 0f;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_step == 0 || _vehicle == null) return;
        _time += (float)delta;
        TelemetryFrame f = _vehicle.LastFrame;

        switch (_step)
        {
            case 1 when _time > 0.3f:
                _control.ToggleSas();
                {
                    var pod = (CommandPodPart)_vehicle.PartTree.RootPart;
                    var imu = pod.GetModules<GyroscopeImuModule>()[0];
                    GD.Print($"[Test C] state: valid {_control.Estimator.HasValidAttitude}, modes {_control.Estimator.GetEnabledSasModes()}, " +
                             $"imu powered {imu.IsPowered}, imu state {imu.State}, imu temp {imu.TempK:0} K, pod attached {pod.IsAttached}, " +
                             $"sensor {_control.Estimator.ActiveSensor?.ModelId ?? "none"}, last mode {_control.AttitudeController.LastActiveMode}");
                    var batts = pod.GetModules<BatteryModule>();
                    GD.Print($"[Test C] power: batteries {batts.Count}, stored {(batts.Count > 0 ? batts[0].StoredEnergyWh : -1f):0.0} Wh, " +
                             $"fraction {PowerSolver.BatteryFraction(pod):0.00}, battery state {(batts.Count > 0 ? batts[0].State.ToString() : "-")}, " +
                             $"modules {pod.Modules.Count}, avionics powered {imu.IsPowered}, t {_time:0.00}");
                }
                Check("T turns SAS on (StabilityAssist)", _control.AttitudeController.Mode == SasMode.StabilityAssist);
                _vehicle.Staging.ActivateNextStage();
                _control.ThrottleController.SetThrottle(1f);
                _step = 2;
                break;
            case 2 when _time > 2.5f:
                Check($"lift-off ({f.AltitudeM:0} m, {f.VerticalSpeedMps:0} m/s up)", !_vehicle.IsOnPad && f.VerticalSpeedMps > 0f);
                _vehicle.AngularVelocity = new Vector3(0f, 0f, Mathf.DegToRad(KickRateDegPerSec));
                _kickTime = _time;
                _maxRateAfterKick = 0f;
                _gimbalAfterKick = 0f;
                _step = 3;
                break;
            case 3:
                _maxRateAfterKick = Mathf.Max(_maxRateAfterKick, Mathf.Abs(f.AngularRateDegPerSec));
                // Lowest gimbal angle in the first 0.5 s after the kick (the spin is damped quickly, so one late sample is fragile).
                if (_time <= _kickTime + 0.5f)
                    _gimbalAfterKick = Mathf.Min(_gimbalAfterKick, _vehicle.PartTree.GetParts<LiquidEnginePart>()[0].GimbalAngleDeg);
                if (_time > _kickTime + 6f)
                {
                    Check($"spin +{KickRateDegPerSec:0} deg/s pushes the gimbal negative ({_gimbalAfterKick:0.00} deg)", _gimbalAfterKick < 0f);
                    Check($"SAS stopped the spin (rate now {f.AngularRateDegPerSec:0.00} deg/s)", Mathf.Abs(f.AngularRateDegPerSec) < 1.5f);
                    float tilt = Mathf.Abs(f.PitchDeg - 90f);
                    Check($"attitude held near vertical (pitch {f.PitchDeg:0.0} deg)", tilt < 30f);
                    Finish();
                }
                break;
        }
    }

    private void Finish()
    {
        _step = 0;
        _control.ThrottleController.SetThrottle(0f);
        GD.Print($"[Test C] ControlChainTest done: {_passed} passed, {_failed} failed");
    }

    private void Check(string name, bool ok)
    {
        if (ok) _passed++;
        else _failed++;
        GD.Print($"[Test C] {(ok ? "PASS" : "FAIL")} {name}");
    }
}
