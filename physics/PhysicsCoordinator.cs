using System.Collections.Generic;
using Godot;
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
    /// <summary>Body of the current segment, or null before Initialize.</summary>
    public CelestialBodyDefinition CurrentBody { get; private set; }

    private IAtmosphereModel _atmosphere;
    private AerodynamicSolver _aerodynamics;
    private OrbitalGravitySolver _gravity;
    private PropulsionSolver _propulsion;
    private ThermalSolver _thermal;
    private PowerSolver _power;
    private readonly List<PartLoad> _partLoads = new();
    private readonly List<FlightEvent> _events = new();

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Settings, nameof(Settings))) return;
        BreakHandler = new StructuralBreakHandler(Settings);
        Failures = new FailureDetector(BreakHandler);
        StressEvaluator = new StructuralStressEvaluator();
        Hazards = new HazardMonitor(Settings, HazardSeed);
        _aerodynamics = new AerodynamicSolver(Settings);
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
        Hazards.Configure(segment);
        GD.Print($"[Physics] Initialized for {segment.SegmentId} on {CurrentBody.BodyId}");
    }

    /// <summary>Forces, torque and flight data for one tick (guide B 4.3).</summary>
    public PhysicsStepOutput ExecutePhysicsStep(PhysicsStepInput input)
    {
        _partLoads.Clear();
        _events.Clear();
        var output = new PhysicsStepOutput { PartLoads = _partLoads, Events = _events };
        if (_atmosphere == null || input.Mass.TotalMass <= 0f) return output;

        Failures.BeginTick();
        float rot = input.BodyRotationRad;
        float speed = input.LinearVelocity.Length();
        output.AltitudeMeters = _gravity.GetAltitude(input.Position);
        output.Air = _atmosphere.Sample(output.AltitudeMeters);

        // TODO(B2): shielding (step 2).
        ThrustStepResult thrust = _propulsion.Solve(input.Parts, output.Air.Pressure, input.Mass.CenterOfMass, input.DeltaTime);
        Vector2 airVelBody = FlightMath.BodyToWorld(-input.LinearVelocity, -rot);
        AeroResult aero = _aerodynamics.Solve(input.Parts, airVelBody, output.Air, input.Mass.CenterOfMass);
        output.GravityForce = _gravity.CalculateGravityForce(input.Position, input.Mass.TotalMass);
        // TODO(B2): reaction wheel and RCS torque (step 6).

        output.HeatFlux = _thermal.CalculateHeatFlux(output.Air.Density, speed);
        output.Thermal = _thermal.Step(input.Parts, output.HeatFlux, input.DeltaTime);
        _events.AddRange(Hazards.Evaluate(input, output));

        output.NetForce = FlightMath.BodyToWorld(thrust.ThrustForceVector + aero.Force, rot) + output.GravityForce;
        output.NetTorqueZ = thrust.TorqueZ + aero.TorqueZ;
        output.ConsumedMass = thrust.MassBurnedKg + thrust.MassLeakedKg;
        output.DynamicPressure = _aerodynamics.CalculateDynamicPressure(output.Air.Density, speed);
        output.MachNumber = output.Air.SpeedOfSound > 0f ? speed / output.Air.SpeedOfSound : 0f;
        output.CurrentGForce = (output.NetForce - output.GravityForce).Length() / (input.Mass.TotalMass * FlightMath.G0);

        if (DebugLog)
        {
            GD.Print($"[Physics] alt {output.AltitudeMeters:F0} m, q {output.DynamicPressure:F0} Pa, F {output.NetForce}");
        }
        return output;
    }
}
