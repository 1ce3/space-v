using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Physics;
using RocketGame.Presentation;
using Side = RocketGame.Core.Side;

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

    /// <summary>Lower bound for the rigid body mass and inertia (Jolt rejects 0).</summary>
    private const float MinBodyMassKg = 1f;
    /// <summary>Decoupler collision plate thickness, in cells.</summary>
    private const float DecouplerThickness = 0.1f;

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
        if (rocket?.PartTree?.RootPart == null)
        {
            GD.PushError("[Control] VehicleRoot.OnDesignConfirmed: rocket has no part tree");
            return;
        }
        ClearVehicle();

        PartTree = rocket.PartTree;
        float cell = RocketPart.GetCellSize();
        foreach (RocketPart part in PartTree.ActiveParts)
        {
            // 1. Part nodes in the vehicle-local frame.
            if (part.GetParent() != null) part.GetParent().RemoveChild(part);
            AddChild(part);
            Vector2 local = part.GetLocalPosition();
            part.Position = new Vector3(local.X, local.Y, 0f);
            part.Rotation = Vector3.Zero;

            // 2. One box per part, direct child of the body (layer/mask 0: the body never collides).
            var shape = new CollisionShape3D
            {
                Name = $"Shape_{part.Name}",
                Shape = new BoxShape3D { Size = GetShapeSize(part, cell) },
                Position = part.Position,
            };
            AddChild(shape);
            _shapes[part] = shape;
        }
        CollisionLayer = 0;
        CollisionMask = 0;

        // 3. Mass, CoM, inertia.
        RecalculatePhysicalProperties();

        // 4. Controllers and cuts.
        Staging.Bind(PartTree, rocket.Stages);
        FlightControl.Bind(PartTree);
        PartTree.PartSevered += OnPartSevered;

        // 5. Lowest part bottom at altitude 0, center column (pod) at x = 0.
        PlaceOnPad();
        if (DebugLog)
            GD.Print($"[Control] Vehicle built: {PartTree.ActiveParts.Count} parts, {MassProps.TotalMass:0} kg, CoM {MassProps.CenterOfMass}");
    }

    /// <summary>Puts the vehicle upright on the pad, at rest.</summary>
    public void PlaceOnPad()
    {
        float cell = RocketPart.GetCellSize();
        float bottom = float.MaxValue;
        foreach (RocketPart part in PartTree.ActiveParts)
        {
            if (part is DecouplerPart) continue;
            bottom = Mathf.Min(bottom, part.Cell.Y * cell);
        }
        if (bottom == float.MaxValue) bottom = 0f;
        float centerX = PartTree.RootPart.GetLocalPosition().X;

        var origin = new Vector3(-centerX, -bottom, 0f);
        GlobalTransform = new Transform3D(Basis.Identity, origin);
        PhysicsServer3D.BodySetState(GetRid(), PhysicsServer3D.BodyState.Transform, GlobalTransform);
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        _onPad = true;
    }

    /// <summary>Height of the attached parts, bottom of the lowest to top of the highest (m).</summary>
    public float GetHeightM()
    {
        if (PartTree == null) return 0f;
        float cell = RocketPart.GetCellSize();
        float bottom = float.MaxValue, top = float.MinValue;
        foreach (RocketPart part in PartTree.ActiveParts)
        {
            if (part is DecouplerPart) continue;
            bottom = Mathf.Min(bottom, part.Cell.Y * cell);
            top = Mathf.Max(top, (part.Cell.Y + part.Footprint.Y) * cell);
        }
        return top > bottom ? top - bottom : 0f;
    }

    /// <summary>World position of the center of mass.</summary>
    public Vector3 GetWorldCenterOfMass() => GlobalTransform * new Vector3(MassProps.CenterOfMass.X, MassProps.CenterOfMass.Y, 0f);

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
        if (PartTree == null) return;
        MassProps = MassCenterCalculator.Calculate(PartTree.ActiveParts);
        Mass = Mathf.Max(MassProps.TotalMass, MinBodyMassKg);
        CenterOfMass = new Vector3(MassProps.CenterOfMass.X, MassProps.CenterOfMass.Y, 0f);
        float inertia = Mathf.Max(MassProps.MomentOfInertia, MinBodyMassKg);
        Inertia = new Vector3(inertia, inertia, inertia);
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

    /// <summary>Removes the parts and shapes of a previous rocket (a new run in the same scene).</summary>
    private void ClearVehicle()
    {
        if (PartTree != null) PartTree.PartSevered -= OnPartSevered;
        foreach (CollisionShape3D shape in _shapes.Values)
            if (IsInstanceValid(shape)) shape.QueueFree();
        _shapes.Clear();
        _pendingSevers.Clear();
        if (PartTree != null)
            foreach (RocketPart part in PartTree.ActiveParts)
                if (IsInstanceValid(part) && part.GetParent() == this) RemoveChild(part);
        PartTree = null;
        _lastFrame = default;
    }

    /// <summary>Box of the part footprint; decouplers are a thin plate on their edge.</summary>
    private static Vector3 GetShapeSize(RocketPart part, float cell)
    {
        if (part is DecouplerPart decoupler)
        {
            bool vertical = decoupler.Edge.Side == Side.Left || decoupler.Edge.Side == Side.Right;
            return vertical ? new Vector3(DecouplerThickness * cell, cell, cell) : new Vector3(cell, DecouplerThickness * cell, cell);
        }
        return new Vector3(part.Footprint.X * cell, part.Footprint.Y * cell, cell);
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
