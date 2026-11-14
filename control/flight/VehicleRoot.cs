using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Physics;
using RocketGame.Presentation;

namespace RocketGame.Control;

/// <summary>
/// The flying rocket. Owns the part nodes, calls PhysicsCoordinator every tick in _IntegrateForces
/// and applies the result to the rigid body (C_CONTROL.md 4.3). Same node for all 3 segments.
/// </summary>
public partial class VehicleRoot : RigidBody3D
{
    /// <summary>Emitted after mass, CoM or inertia changed. Read MassProps for the new values.</summary>
    [Signal] public delegate void MassPropertiesChangedEventHandler();

    [ExportGroup("Links")]
    [Export] public PhysicsCoordinator Coordinator { get; set; }
    [Export] public FlightDirector Director { get; set; }
    [Export] public FlightControlSubsystem FlightControl { get; set; }
    [Export] public StagingManager Staging { get; set; }
    [Export] public BodyEnvironmentVisual Environment { get; set; }
    [Export] public FlightSettings Settings { get; set; }

    [ExportGroup("Debug")]
    [Export] public bool DebugLog { get; set; }

    /// <summary>Runtime part tree of the rocket being flown.</summary>
    public PartTree PartTree { get; private set; }
    /// <summary>Mass properties in the vehicle-local frame. Named MassProps because RigidBody3D already has Mass.</summary>
    public MassProperties MassProps { get; private set; }
    /// <summary>Run being flown; its BlackBox logs every tick.</summary>
    public MissionRun Run { get; set; }
    /// <summary>True while held on the launch pad (before the first lift-off).</summary>
    public bool IsOnPad => _onPad;
    /// <summary>True during cruise: the body does not move.</summary>
    public bool IsSimulationFrozen => _frozen;
    /// <summary>Telemetry of the last physics tick.</summary>
    public TelemetryFrame LastFrame => _lastFrame;

    private readonly Dictionary<RocketPart, CollisionShape3D> _shapes = new();
    private readonly List<(RocketPart part, float impulseNs)> _pendingSevers = new();
    private TelemetryFrame _lastFrame;
    private bool _onPad;
    private bool _frozen;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Coordinator, nameof(Coordinator))) return;
        if (!NodeGuard.Require(this, Director, nameof(Director))) return;
        if (!NodeGuard.Require(this, FlightControl, nameof(FlightControl))) return;
        if (!NodeGuard.Require(this, Staging, nameof(Staging))) return;
        if (!NodeGuard.Require(this, Settings, nameof(Settings))) return;
        ConfigureBody();
    }

    /// <summary>Builds the flying rocket from the build screen result (segment 1 start).</summary>
    public void OnDesignConfirmed(AssembledRocket rocket)
    {
        // TODO(C2): add part nodes, one CollisionShape3D per part (layer/mask 0), set Mass/CenterOfMass/Inertia,
        //           Staging.Bind(...), FlightControl.Bind(...), connect PartTree.PartSevered, place on pad, _onPad = true.
    }

    /// <summary>Places the same vehicle at the Mars entry point (segment 3 start).</summary>
    public void RestoreFromRun(MissionRun run)
    {
        // TODO(C6): Coordinator.Initialize(run.CurrentSegment); entry position/velocity/attitude; SetSimulationFrozen(false).
    }

    /// <summary>Freezes the body during cruise (decision #1).</summary>
    public void SetSimulationFrozen(bool frozen)
    {
        _frozen = frozen;
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        // TODO(C3): tick order of C_CONTROL.md 4.3 (freeze, read state, FlightControl.Update, ExecutePhysicsStep,
        //           pad hold, apply force/torque, stress and thermal checks, mass, ground contact, telemetry).
    }

    /// <summary>Recomputes MassProps from the active parts and pushes them to the rigid body.</summary>
    public void RecalculatePhysicalProperties()
    {
        // TODO(C2): MassProps = MassCenterCalculator.Calculate(PartTree.ActiveParts); Mass, CenterOfMass, Inertia.
        EmitSignal(SignalName.MassPropertiesChanged);
    }

    /// <summary>Telemetry for the current state.</summary>
    public TelemetryFrame GetTelemetry()
    {
        // TODO(C3): fill every field from the body state and the last PhysicsStepOutput.
        return _lastFrame;
    }

    /// <summary>PartTree.PartSevered handler. Only queues the cut; nodes are changed in ProcessPendingSevers.</summary>
    public void OnPartSevered(RocketPart part, SeverCause cause, float impulseNs)
    {
        _pendingSevers.Add((part, impulseNs));
        CallDeferred(MethodName.ProcessPendingSevers);
    }

    /// <summary>Removes shapes of cut subtrees, spawns debris and recomputes mass. Runs outside the physics callback.</summary>
    public void ProcessPendingSevers()
    {
        // TODO(C3): for each pending cut: GetSubtree, remove shapes, Coordinator.BreakHandler.SpawnDebrisRigidBody(...),
        //           opposite impulse on this body, then RecalculatePhysicalProperties().
        _pendingSevers.Clear();
    }

    private void ConfigureBody()
    {
        AxisLockLinearZ = true;
        AxisLockAngularX = true;
        AxisLockAngularY = true;
        GravityScale = 0f;
        CenterOfMassMode = CenterOfMassModeEnum.Custom;
        CanSleep = false;
        LinearDampMode = DampMode.Replace;
        AngularDampMode = DampMode.Replace;
        LinearDamp = 0f;
        AngularDamp = 0f;
        ContactMonitor = false;
        CustomIntegrator = false;
    }
}
