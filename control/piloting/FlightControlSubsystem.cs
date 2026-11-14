using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>
/// Runs the piloting chain once per physics tick, in the order of C_CONTROL.md 5:
/// input, anomalies, discrete commands, estimator, SAS, allocation, throttle.
/// </summary>
public partial class FlightControlSubsystem : Node
{
    [ExportGroup("Chain")]
    [Export] public InputHandler InputHandler { get; set; }
    [Export] public AttitudeEstimator Estimator { get; set; }
    [Export] public AttitudeController AttitudeController { get; set; }
    [Export] public PIDStabilizer PIDStabilizer { get; set; }
    [Export] public ControlAllocator Allocator { get; set; }
    [Export] public ThrottleController ThrottleController { get; set; }
    [Export] public AnomalyResponseHandler Anomalies { get; set; }

    [ExportGroup("Links")]
    [Export] public StagingManager Staging { get; set; }
    [Export] public FlightDirector Director { get; set; }

    [ExportGroup("Debug")]
    [Export] public bool DebugLog { get; set; }

    /// <summary>Input read at the last Update.</summary>
    public PilotInput LastInput { get; private set; }

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, InputHandler, nameof(InputHandler))) return;
        if (!NodeGuard.Require(this, Estimator, nameof(Estimator))) return;
        if (!NodeGuard.Require(this, AttitudeController, nameof(AttitudeController))) return;
        if (!NodeGuard.Require(this, PIDStabilizer, nameof(PIDStabilizer))) return;
        if (!NodeGuard.Require(this, Allocator, nameof(Allocator))) return;
        if (!NodeGuard.Require(this, ThrottleController, nameof(ThrottleController))) return;
        if (!NodeGuard.Require(this, Staging, nameof(Staging))) return;
        NodeGuard.Require(this, Anomalies, nameof(Anomalies));
    }

    /// <summary>Hands the runtime part tree to every child that needs it.</summary>
    public void Bind(PartTree tree)
    {
        Estimator?.Bind(tree);
        Allocator?.Bind(tree);
        ThrottleController?.Bind(tree);
        Anomalies?.Bind(tree);
    }

    /// <summary>One tick of the piloting chain. lastFrame is the telemetry of the previous tick.</summary>
    public void Update(float deltaTime, TelemetryFrame lastFrame)
    {
        PilotInput input = InputHandler.Poll();
        LastInput = input;

        // TODO(C7): Anomalies.HandleInput(input);
        // TODO(C3/C4): discrete commands (stage, fairing, chute, RCS, deploy, SAS mode, abort) per 5 step 3.

        Estimator.CurrentAltitudeM = lastFrame.AltitudeM;
        Estimator.Update(lastFrame.PitchDeg, lastFrame.AngularRateDegPerSec, deltaTime);
        AttitudeController.CurrentFlightPathAngleDeg = lastFrame.FlightPathAngleDeg;
        float u = AttitudeController.ComputeDemand(input, deltaTime);
        Allocator.Allocate(u, lastFrame.DynamicPressure);
        ThrottleController.Update(input, lastFrame.GForce, deltaTime);
    }

    /// <summary>VehicleRoot.MassPropertiesChanged handler.</summary>
    public void OnMassPropertiesChanged()
    {
        PIDStabilizer?.Reset();
        Allocator?.OnMassPropertiesChanged();
    }
}
