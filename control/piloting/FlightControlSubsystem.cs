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
    /// <summary>Short message for the HUD (SAS refused, no RCS...).</summary>
    [Signal] public delegate void ControlWarningEventHandler(string message);

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

    private PartTree _tree;

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
        _tree = tree;
        Estimator?.Bind(tree);
        AttitudeController?.ResetState();
        Allocator?.Bind(tree);
        ThrottleController?.Bind(tree);
        Anomalies?.Bind(tree);
    }

    /// <summary>One tick of the piloting chain. lastFrame is the telemetry of the previous tick.</summary>
    public void Update(float deltaTime, TelemetryFrame lastFrame)
    {
        PilotInput input = InputHandler.Poll();
        LastInput = input;

        Anomalies?.HandleInput(input);
        Anomalies?.Tick(deltaTime);
        if (input.StagePressed) Staging.ActivateNextStage();
        if (input.FairingPressed) Staging.JettisonFairing();
        if (input.ChutePressed) Staging.ToggleParachute();
        if (input.AbortPressed) Director?.Abort();
        if (input.RcsTogglePressed) ToggleRcs();
        if (input.DeployTogglePressed) ToggleDeployables();

        // The estimator runs before SAS requests so a fresh vehicle can switch SAS on at once.
        Estimator.CurrentAltitudeM = lastFrame.AltitudeM;
        Estimator.Update(lastFrame.PitchDeg, lastFrame.AngularRateDegPerSec, deltaTime);
        if (input.SasTogglePressed) ToggleSas();
        if (input.HasSasModeRequest) RequestSasMode(input.RequestedSasMode);

        AttitudeController.CurrentFlightPathAngleDeg = lastFrame.FlightPathAngleDeg;
        float u = AttitudeController.ComputeDemand(input, deltaTime);
        Allocator.TranslateCommand = input.RcsTranslateAxis;
        Allocator.Allocate(u, lastFrame.DynamicPressure);
        ThrottleController.Update(input, lastFrame.GForce, deltaTime);
    }

    /// <summary>T: SAS off, or back to the last mode (StabilityAssist first time).</summary>
    public void ToggleSas()
    {
        if (AttitudeController.Mode != SasMode.Off)
        {
            AttitudeController.SetMode(SasMode.Off);
            return;
        }
        if (AttitudeController.SetMode(AttitudeController.LastActiveMode)) return;
        if (AttitudeController.SetMode(SasMode.StabilityAssist)) return;
        Warn(Estimator.HasValidAttitude ? "SAS: the sensors don't support this mode" : "SAS: no working attitude sensor");
    }

    /// <summary>1..4: switch to a SAS mode if the sensors allow it.</summary>
    public void RequestSasMode(SasMode mode)
    {
        if (AttitudeController.SetMode(mode)) return;
        Warn(Estimator.HasValidAttitude ? $"SAS: better sensors needed for {mode}" : "SAS: no working attitude sensor");
    }

    /// <summary>R: turns every RCS on or off together.</summary>
    public void ToggleRcs()
    {
        var all = new System.Collections.Generic.List<RcsThrusterModule>();
        foreach (CommandPodPart pod in _tree?.GetParts<CommandPodPart>() ?? new())
            if (pod.IsAttached) all.AddRange(pod.GetModules<RcsThrusterModule>());
        if (all.Count == 0)
        {
            Warn("No RCS");
            return;
        }
        bool enable = !all.Exists(r => r.IsEnabled);
        foreach (RcsThrusterModule rcs in all) rcs.IsEnabled = enable;
        Warn(enable ? "RCS on" : "RCS off");
    }

    /// <summary>G: deploys solar arrays and radiators, or stows them all if any is out.</summary>
    public void ToggleDeployables()
    {
        var solars = new System.Collections.Generic.List<SolarArrayModule>();
        var radiators = new System.Collections.Generic.List<ActiveRadiatorModule>();
        foreach (CommandPodPart pod in _tree?.GetParts<CommandPodPart>() ?? new())
        {
            if (!pod.IsAttached) continue;
            solars.AddRange(pod.GetModules<SolarArrayModule>());
            radiators.AddRange(pod.GetModules<ActiveRadiatorModule>());
        }
        if (solars.Count == 0 && radiators.Count == 0)
        {
            Warn("No solar panels or radiators to deploy");
            return;
        }
        bool deploy = !solars.Exists(s => s.IsDeployed) && !radiators.Exists(r => r.IsPanelExtended);
        foreach (SolarArrayModule solar in solars) solar.SetDeployed(deploy);
        foreach (ActiveRadiatorModule radiator in radiators) radiator.SetExtended(deploy);
        Warn(deploy ? "Deploy solar panels / radiators" : "Retract solar panels / radiators");
    }

    private void Warn(string message)
    {
        if (DebugLog) GD.Print($"[Control] {message}");
        EmitSignal(SignalName.ControlWarning, message);
    }

    /// <summary>VehicleRoot.MassPropertiesChanged handler.</summary>
    public void OnMassPropertiesChanged()
    {
        PIDStabilizer?.Reset();
        Allocator?.OnMassPropertiesChanged();
    }
}
