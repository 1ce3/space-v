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
    /// <summary>Physics result of the last tick (HUD, debug).</summary>
    public PhysicsStepOutput LastOutput => _lastOutput;

    /// <summary>Lower bound for the rigid body mass and inertia (Jolt rejects 0).</summary>
    private const float MinBodyMassKg = 1f;
    /// <summary>Decoupler collision plate thickness, in cells.</summary>
    private const float DecouplerThickness = 0.1f;

    private readonly Dictionary<RocketPart, CollisionShape3D> _shapes = new();
    private readonly List<(RocketPart part, SeverCause cause, float impulseNs)> _pendingSevers = new();
    private TelemetryFrame _lastFrame;
    private PhysicsStepOutput _lastOutput;
    private float _lastStressRatio;
    private float _massTimer;
    private bool _massDirty;
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
        MassPropertiesChanged += FlightControl.OnMassPropertiesChanged;
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

    public override void _PhysicsProcess(double delta)
    {
        // Rigid body parameters are changed here, outside the physics callback.
        if (_massDirty && PartTree != null)
        {
            _massDirty = false;
            RecalculatePhysicalProperties();
        }
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (PartTree == null || Coordinator?.CurrentBody == null) return;

        // 1. Frozen (cruise).
        if (_frozen)
        {
            state.LinearVelocity = Vector3.Zero;
            state.AngularVelocity = Vector3.Zero;
            return;
        }

        // 2. Read the state.
        float dt = state.Step;
        Transform3D xf = state.Transform;
        Vector3 com3 = xf * CenterOfMass;
        var position = new Vector2(com3.X, com3.Y);
        var velocity = new Vector2(state.LinearVelocity.X, state.LinearVelocity.Y);
        float rotation = Mathf.Atan2(xf.Basis.X.Y, xf.Basis.X.X);
        Vector2 center = PlanetCenter;
        float attitudeDeg = FlightMath.AttitudeDeg(rotation, position, center);
        float rateDegPerSec = Mathf.RadToDeg(state.AngularVelocity.Z);

        // 3. Piloting writes commands into parts and modules.
        FlightControl.Update(dt, _lastFrame);

        // 4. Physics.
        var input = new PhysicsStepInput
        {
            Position = position,
            LinearVelocity = velocity,
            BodyRotationRad = rotation,
            PitchAngleDeg = attitudeDeg,
            AngularVelocityDegPerSec = rateDegPerSec,
            Mass = MassProps,
            Parts = PartTree,
            DeltaTime = dt,
        };
        PhysicsStepOutput output = Coordinator.ExecutePhysicsStep(input);
        _lastOutput = output;
        EngineVisual.AmbientPressurePa = output.Air.Pressure; // plume shape only

        // 5. Pad hold: stay put until the net force lifts the rocket.
        Vector2 up = FlightMath.LocalUp(position, center);
        if (_onPad)
        {
            if (output.NetForce.Dot(up) <= 0f)
            {
                state.LinearVelocity = Vector3.Zero;
                state.AngularVelocity = Vector3.Zero;
            }
            else
            {
                _onPad = false;
                if (DebugLog) GD.Print("[Control] Lift-off");
            }
        }

        // 6. Forces.
        if (!_onPad)
        {
            state.ApplyCentralForce(new Vector3(output.NetForce.X, output.NetForce.Y, 0f));
            state.ApplyTorque(new Vector3(0f, 0f, output.NetTorqueZ));
        }

        // 7. Structure and heat.
        Coordinator.StressEvaluator.EvaluateRecursiveLoad(input, output);
        StressReport report = Coordinator.StressEvaluator.CheckYieldLimits();
        _lastStressRatio = report?.MaxStressRatio ?? 0f;
        Coordinator.Failures.MonitorStressThresholds(report);
        Coordinator.Failures.MonitorThermalLimits(output.Thermal);

        // 8. Mass: drop the burned mass now, full CoM/inertia refresh at MassRecalcHz (and after every cut).
        if (output.ConsumedMass > 0f)
        {
            MassProperties mp = MassProps;
            mp.TotalMass = Mathf.Max(mp.TotalMass - output.ConsumedMass, MinBodyMassKg);
            MassProps = mp;
            _massTimer += dt;
            float period = 1f / Mathf.Max(Settings.MassRecalcHz, 1f);
            if (_massTimer >= period)
            {
                _massTimer = 0f;
                _massDirty = true;
            }
        }

        // 9. Ground contact, computed from altitude.
        float radar = GetLowestPoint(xf, center, out RocketPart lowest);
        bool descending = velocity.Dot(up) < 0f;
        if (radar <= 0f && !_onPad && descending && lowest != null)
            Director.OnGroundContact(lowest, velocity.Length());

        // 10. Telemetry and listeners.
        _lastFrame = BuildFrame(output, position, velocity, attitudeDeg, rateDegPerSec, radar, center);
        Director.OnTick(_lastFrame);
        Run?.BlackBox.LogFlightMetrics(_lastFrame, dt);
        FlightControl.Anomalies?.OnFlightEvents(output.Events);
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
        return _lastFrame;
    }

    /// <summary>PartTree.PartSevered handler. Only queues the cut; nodes are changed in ProcessPendingSevers.</summary>
    public void OnPartSevered(RocketPart part, SeverCause cause, float impulseNs)
    {
        _pendingSevers.Add((part, cause, impulseNs));
        if (_pendingSevers.Count == 1) CallDeferred(MethodName.ProcessPendingSevers);
    }

    /// <summary>Removes shapes of cut subtrees, spawns debris and recomputes mass. Runs outside the physics callback.</summary>
    public void ProcessPendingSevers()
    {
        if (_pendingSevers.Count == 0) return;
        var pending = new List<(RocketPart part, SeverCause cause, float impulseNs)>(_pendingSevers);
        _pendingSevers.Clear();
        var worldParent = GetParent<Node3D>();

        foreach ((RocketPart part, SeverCause cause, float impulseNs) in pending)
        {
            if (!IsInstanceValid(part) || part.GetParent() != this) continue;
            List<RocketPart> parts = PartTree.GetSubtree(part);
            parts.RemoveAll(p => !IsInstanceValid(p) || p.GetParent() != this);

            foreach (RocketPart p in parts)
            {
                if (!_shapes.TryGetValue(p, out CollisionShape3D shape)) continue;
                shape.QueueFree();
                _shapes.Remove(p);
            }

            RocketPart parent = part.ParentJoint?.ParentPart;
            Vector3 dir = parent != null && IsInstanceValid(parent)
                ? (part.GlobalPosition - parent.GlobalPosition).Normalized()
                : -GlobalTransform.Basis.Y;
            Vector3 impulse = dir * impulseNs;

            Coordinator.BreakHandler.SpawnDebrisRigidBody(parts, LinearVelocity, AngularVelocity, impulse,
                worldParent, Coordinator.CurrentBody, cause);
            if (impulseNs > 0f) ApplyCentralImpulse(-impulse);

            Run?.BlackBox.OnPartSevered(part, cause);
            Director.OnPartSevered(part, cause);
            if (DebugLog) GD.Print($"[Control] Cut {part.ModelId} ({cause}): {parts.Count} parts became debris");
        }
        RecalculatePhysicalProperties();
    }

    /// <summary>Planet center in world coordinates (surface at the origin).</summary>
    private Vector2 PlanetCenter => new(0f, -(Coordinator?.CurrentBody?.Radius ?? 0f));

    /// <summary>Lowest altitude over the 4 footprint corners of every grid part (radar altitude).</summary>
    private float GetLowestPoint(Transform3D xf, Vector2 center, out RocketPart lowest)
    {
        float radius = Coordinator.CurrentBody.Radius;
        float cell = RocketPart.GetCellSize();
        float best = float.MaxValue;
        lowest = null;
        foreach (RocketPart part in PartTree.ActiveParts)
        {
            if (part is DecouplerPart) continue;
            float x0 = part.Cell.X * cell, y0 = part.Cell.Y * cell;
            float x1 = x0 + part.Footprint.X * cell, y1 = y0 + part.Footprint.Y * cell;
            for (int i = 0; i < 4; i++)
            {
                var local = new Vector3(i % 2 == 0 ? x0 : x1, i < 2 ? y0 : y1, 0f);
                Vector3 w = xf * local;
                float alt = FlightMath.Altitude(new Vector2(w.X, w.Y), center, radius);
                if (alt < best)
                {
                    best = alt;
                    lowest = part;
                }
            }
        }
        return lowest == null ? 0f : best;
    }

    private TelemetryFrame BuildFrame(PhysicsStepOutput output, Vector2 position, Vector2 velocity, float attitudeDeg,
        float rateDegPerSec, float radar, Vector2 center)
    {
        return new TelemetryFrame
        {
            AltitudeM = output.AltitudeMeters,
            RadarAltitudeM = radar,
            SpeedMps = velocity.Length(),
            VerticalSpeedMps = FlightMath.VerticalSpeed(velocity, position, center),
            HorizontalSpeedMps = FlightMath.HorizontalSpeed(velocity, position, center),
            PitchDeg = attitudeDeg,
            AngularRateDegPerSec = rateDegPerSec,
            FlightPathAngleDeg = FlightMath.FlightPathAngleDeg(velocity, position, center),
            Throttle = FlightControl.ThrottleController?.Throttle ?? 0f,
            StageFuelFraction = GetActiveFuelFraction(),
            DynamicPressure = output.DynamicPressure,
            GForce = output.CurrentGForce,
            MachNumber = output.MachNumber,
            MaxTempRatio = output.Thermal.MaxTempRatio,
            MaxStressRatio = _lastStressRatio,
            HeatFlux = output.HeatFlux,
            AblatorFraction = GetAblatorFraction(),
            CurrentStageIndex = Staging.CurrentStageIndex,
            Phase = Director.Phase,
            ElapsedDays = Run?.ElapsedDays ?? 0f,
        };
    }

    /// <summary>Fuel left in the tanks of armed liquid engines and in armed boosters, 0..1.</summary>
    private float GetActiveFuelFraction()
    {
        float left = 0f, full = 0f;
        var counted = new HashSet<FuelTankPart>();
        foreach (EnginePart engine in PartTree.GetParts<EnginePart>())
        {
            if (!engine.IsArmed) continue;
            if (engine is LiquidEnginePart liquid)
            {
                foreach (FuelTankPart tank in liquid.FeedTanks)
                {
                    if (!tank.IsAttached || !counted.Add(tank)) continue;
                    left += tank.CurrentFuel;
                    full += tank.TankDefinition?.FuelCapacity ?? 0f;
                }
            }
            else if (engine is SolidBoosterPart booster)
            {
                left += booster.RemainingPropellant;
                full += booster.BoosterDefinition?.PropellantMass ?? 0f;
            }
        }
        return full > 0f ? left / full : 0f;
    }

    private float GetAblatorFraction()
    {
        List<HeatShieldPart> shields = PartTree.GetParts<HeatShieldPart>();
        return shields.Count > 0 ? shields[0].GetAblatorFraction() : 1f;
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
