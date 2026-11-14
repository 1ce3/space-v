using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Physics;

/// <summary>Single entry point of the physics system. VehicleRoot (C) calls ExecutePhysicsStep every tick and applies the result to its rigid body.</summary>
[GlobalClass]
public partial class PhysicsCoordinator : Node
{
    /// <summary>Shared physics tuning (data/settings/physics_settings.tres).</summary>
    [Export] public PhysicsSettings Settings { get; set; }
    /// <summary>Seed of the hazard dice, to replay a bug.</summary>
    [Export] public ulong HazardSeed { get; set; } = 12345;
    /// <summary>Print a line every tick (slow).</summary>
    [Export] public bool DebugLog { get; set; }

    /// <summary>Breaks from stress, heat and impact. Raises BreakTriggered.</summary>
    public FailureDetector Failures { get; private set; }
    /// <summary>Cuts broken parts and spawns debris.</summary>
    public StructuralBreakHandler BreakHandler { get; private set; }
    /// <summary>Joint loads; VehicleRoot calls it after ExecutePhysicsStep.</summary>
    public StructuralStressEvaluator StressEvaluator { get; private set; }
    /// <summary>In-flight incidents.</summary>
    public HazardMonitor Hazards { get; private set; }
    /// <summary>A parachute was opened too fast and tore: (chute, q in Pa, Mach). Hook a HUD warning here.</summary>
    public event System.Action<ParachutePart, float, float> ParachuteTorn;
    /// <summary>Body of the current segment, or null before Initialize.</summary>
    public CelestialBodyDefinition CurrentBody { get; private set; }

    private IAtmosphereModel _atmosphere;
    private AerodynamicSolver _aerodynamics;
    private OrbitalGravitySolver _gravity;
    private PropulsionSolver _propulsion;
    private ThermalSolver _thermal;
    private PowerSolver _power;
    private readonly List<PartLoad> _partLoads = new();
    private readonly List<PartLoad> _bodyLoads = new();
    private readonly Dictionary<RocketPart, Vector2> _loadByPart = new();
    private readonly List<FlightEvent> _events = new();
    private readonly List<FlightEvent> _solverEvents = new();

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Settings, nameof(Settings))) return;
        BreakHandler = new StructuralBreakHandler(Settings);
        Failures = new FailureDetector(BreakHandler);
        StressEvaluator = new StructuralStressEvaluator();
        Hazards = new HazardMonitor(Settings, HazardSeed);
        StressEvaluator.PodBreakGFactor = Settings.PodBreakGFactor;
        StressEvaluator.JointCompressionFactor = Settings.JointCompressionFactor;
        _aerodynamics = new AerodynamicSolver(Settings);
        _aerodynamics.ParachuteTorn += (chute, q, mach) => ParachuteTorn?.Invoke(chute, q, mach);
        _gravity = new OrbitalGravitySolver();
        _propulsion = new PropulsionSolver();
        _thermal = new ThermalSolver(Settings);
        _power = new PowerSolver(Settings);
    }

    /// <summary>Sets atmosphere, gravity, heating and hazards from the segment's body. Call at the start of segments 1 and 3.</summary>
    public void Initialize(MissionSegmentDefinition segment)
    {
        if (segment == null || segment.Body == null)
        {
            GD.PushError("[Physics] Initialize needs a segment with a Body.");
            return;
        }
        CurrentBody = segment.Body;
        _atmosphere = CurrentBody.UsesLayerTable
            ? new USSA76AtmosphereModel()
            : new ExponentialAtmosphereModel(CurrentBody);
        _gravity.Configure(CurrentBody);
        _thermal.Configure(CurrentBody);
        _aerodynamics.Reset();
        Hazards.Configure(segment);
        GD.Print($"[Physics] Initialized for {segment.SegmentId} on {CurrentBody.BodyId}");
    }

    /// <summary>Forces, torque and flight data for one tick (guide B 4.3).</summary>
    public PhysicsStepOutput ExecutePhysicsStep(PhysicsStepInput input)
    {
        _partLoads.Clear();
        _bodyLoads.Clear();
        _loadByPart.Clear();
        _events.Clear();
        _solverEvents.Clear();
        var output = new PhysicsStepOutput { PartLoads = _partLoads, Events = _events };
        if (_atmosphere == null || input.Mass.TotalMass <= 0f) return output;

        Failures.BeginTick();
        float rot = input.BodyRotationRad;
        Vector2 com = input.Mass.CenterOfMass;
        float speed = input.LinearVelocity.Length();
        output.AltitudeMeters = _gravity.GetAltitude(input.Position);
        output.Air = _atmosphere.Sample(output.AltitudeMeters);

        // 1-2. Shielding from the flow direction.
        Vector2 velBody = FlightMath.BodyToWorld(input.LinearVelocity, -rot);
        UpdateShielding(input.Parts, velBody, speed);

        // 3-4. Thrust and aero, vehicle-local frame.
        ThrustStepResult thrust = _propulsion.Solve(input.Parts, output.Air.Pressure, com, input.DeltaTime, _bodyLoads, _solverEvents);
        AeroResult aero = _aerodynamics.Solve(input.Parts, -velBody, output.Air, com, _bodyLoads, input.DeltaTime);

        // 5. Gravity at the center of mass, world frame.
        output.GravityForce = _gravity.CalculateGravityForce(input.Position, input.Mass.TotalMass);

        // 6. Reaction wheels and RCS.
        Vector2 rcsForceBody = Vector2.Zero;
        float moduleTorque = 0f;
        float monopropUsed = 0f;
        if (input.Parts != null) SolveAttitudeModules(input.Parts, input.DeltaTime, ref moduleTorque, ref rcsForceBody, ref monopropUsed);

        // 7. Heating, power (P1), cryo leaks.
        output.HeatFlux = _thermal.CalculateHeatFlux(output.Air.Density, speed);
        Vector2 velDirBody = speed > 0.01f ? velBody / speed : Vector2.Zero;
        output.Thermal = _thermal.Step(input.Parts, output.HeatFlux, input.DeltaTime, velDirBody, com, output.Air.Temperature);
        output.Power = _power.Step(input.Parts, aero.DynamicPressure, CurrentBody?.SolarFluxWm2 ?? 0f, input.DeltaTime);
        float leaked = Hazards.ApplyLeaks(input.Parts, input.DeltaTime);

        // 8. Totals.
        output.NetForce = FlightMath.BodyToWorld(thrust.ThrustForceVector + aero.Force + rcsForceBody, rot) + output.GravityForce;
        output.NetTorqueZ = thrust.TorqueZ + aero.TorqueZ + moduleTorque;
        output.ConsumedMass = thrust.MassBurnedKg + thrust.MassLeakedKg + leaked + monopropUsed;
        output.DynamicPressure = aero.DynamicPressure;
        output.MachNumber = aero.MachNumber;
        output.CurrentGForce = (output.NetForce - output.GravityForce).Length() / (input.Mass.TotalMass * FlightMath.G0);
        CollectPartLoads(rot);

        // 9. Hazards (P1, guide B 4.6) on the finished tick; an undodged debris warning breaks the pod.
        _events.AddRange(Hazards.Evaluate(input, output, _solverEvents, output.Power));
        if (Hazards.ConsumeDebrisHit() && input.Parts?.RootPart != null)
            Failures.TriggerBreak(input.Parts.RootPart, FailureCause.Collision);

        if (DebugLog)
        {
            GD.Print($"[Physics] alt {output.AltitudeMeters:F0} m, q {output.DynamicPressure:F0} Pa, F {output.NetForce}, tau {output.NetTorqueZ:F0}");
        }
        return output;
    }

    /// <summary>Fairing shields its parts when flying nose first; heat shield when flying base first (guide B 4.3 step 2).</summary>
    private void UpdateShielding(PartTree parts, Vector2 velBody, float speed)
    {
        if (parts == null) return;
        foreach (RocketPart part in parts.ActiveParts) part.IsShielded = false;
        if (speed <= 1f) return;

        float cone = Mathf.DegToRad(Settings.ShieldingConeDeg);
        float angleFromUp = new Vector2(0f, 1f).AngleTo(velBody);
        bool noseFirst = Mathf.Abs(angleFromUp) < cone;
        bool baseFirst = Mathf.Pi - Mathf.Abs(angleFromUp) < cone;

        foreach (RocketPart part in parts.ActiveParts)
        {
            if (noseFirst && part is FairingPart fairing) ShieldAll(fairing.ShieldedParts, parts);
            else if (baseFirst && part is HeatShieldPart shield) ShieldAll(shield.ShieldedParts, parts);
        }
    }

    private static void ShieldAll(List<RocketPart> shielded, PartTree parts)
    {
        foreach (RocketPart part in shielded)
        {
            if (parts.ActiveParts.Contains(part)) part.IsShielded = true;
        }
    }

    /// <summary>Reaction wheel and RCS torque, RCS side force and monopropellant use (guide B 4.3.3). Body frame.</summary>
    private void SolveAttitudeModules(PartTree parts, float dt, ref float torque, ref Vector2 sideForce, ref float monopropUsed)
    {
        float rcsExhaustSpeed = Settings.RcsIsp * FlightMath.G0;
        foreach (CommandPodPart pod in parts.GetParts<CommandPodPart>())
        {
            foreach (ReactionWheelModule wheel in pod.GetModules<ReactionWheelModule>())
            {
                if (!wheel.CanOperate()) continue;
                torque += WheelTorque(wheel, dt);
            }
            foreach (RcsThrusterModule rcs in pod.GetModules<RcsThrusterModule>())
            {
                if (!rcs.IsEnabled || !rcs.CanOperate() || rcs.MonopropRemainingKg <= 0f) continue;
                float authority = rcs.GetMaxTorque();
                float tau = rcs.CommandedTorque * authority;
                float side = rcs.TranslateCommand * authority;
                float lever = Mathf.Max(Settings.RcsLeverArmM, 0.01f);
                float used = Mathf.Min((Mathf.Abs(tau) / lever + Mathf.Abs(side)) / rcsExhaustSpeed * dt, rcs.MonopropRemainingKg);
                rcs.MonopropRemainingKg -= used;
                monopropUsed += used;
                torque += tau;
                sideForce += new Vector2(side, 0f);
            }
        }
    }

    /// <summary>Wheel torque; momentum builds up and torque in the saturated direction is dropped.</summary>
    private static float WheelTorque(ReactionWheelModule wheel, float dt)
    {
        float max = wheel.AcsDefinition?.MaxMomentumStorageNms ?? 0f;
        float tau = wheel.CommandedTorque * wheel.GetMaxTorque();
        if (max <= 0f) return tau;
        bool sameDirection = Mathf.Sign(tau) == Mathf.Sign(wheel.StoredMomentumNms) && tau != 0f;
        if (Mathf.Abs(wheel.StoredMomentumNms) >= max && sameDirection)
        {
            wheel.IsSaturated = true;
            return 0f;
        }
        wheel.StoredMomentumNms = Mathf.Clamp(wheel.StoredMomentumNms + tau * dt, -max, max);
        wheel.IsSaturated = Mathf.Abs(wheel.StoredMomentumNms) >= max;
        return tau;
    }

    /// <summary>Thrust + aero per part, rotated to world, one entry per part.</summary>
    private void CollectPartLoads(float rot)
    {
        foreach (PartLoad load in _bodyLoads)
        {
            _loadByPart.TryGetValue(load.Part, out Vector2 sum);
            _loadByPart[load.Part] = sum + load.Force;
        }
        foreach (KeyValuePair<RocketPart, Vector2> pair in _loadByPart)
        {
            _partLoads.Add(new PartLoad { Part = pair.Key, Force = FlightMath.BodyToWorld(pair.Value, rot) });
        }
    }
}
