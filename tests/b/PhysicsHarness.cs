using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Physics;
using RocketGame.Research;

namespace RocketGame.Tests.B;

/// <summary>
/// Sandbox for task B2: integrates a hand-built rocket in 2D (semi-implicit Euler, 60 Hz) with the real PhysicsCoordinator
/// and prints altitude, speed, mass and q every second. Then checks that fins turn the nose back toward the velocity.
/// Definitions come from the export slots (drag data/parts/*.tres in); an empty slot falls back to TestRocketFactory.GetPart (guide A section 7).
/// Parts are runtime nodes, so they get their data through Initialize(def) (AGENTS 4.7), never through exports.
/// </summary>
public partial class PhysicsHarness : Node
{
    /// <summary>Physics node under test.</summary>
    [Export] public PhysicsCoordinator Coordinator { get; set; }
    /// <summary>Segment that sets the body (seg_earth_ascent.tres).</summary>
    [Export] public MissionSegmentDefinition Segment { get; set; }
    /// <summary>Integration step (s).</summary>
    [Export] public float StepSeconds { get; set; } = 1f / 60f;
    /// <summary>Stop after this much simulated time (s).</summary>
    [Export] public float MaxSimSeconds { get; set; } = 400f;
    /// <summary>Altitude where the pitch program starts (m).</summary>
    [Export] public float PitchStartAltitudeM { get; set; } = 1000f;
    /// <summary>Altitude where the pitch program reaches FinalPitchDeg (m).</summary>
    [Export] public float PitchEndAltitudeM { get; set; } = 40000f;
    /// <summary>Attitude at PitchEndAltitudeM (deg, 90 = vertical).</summary>
    [Export] public float FinalPitchDeg { get; set; } = 50f;
    /// <summary>Robot mass in the pod (kg).</summary>
    [Export] public float PayloadMassKg { get; set; } = 300f;
    /// <summary>Command pod definition (pod_mk1.tres).</summary>
    [ExportGroup("Part definitions (empty = guide defaults)")]
    [Export] public CommandPodPartDefinition Pod { get; set; }
    /// <summary>Fuel tank definition (tank_s1.tres).</summary>
    [Export] public FuelTankPartDefinition Tank { get; set; }
    /// <summary>Liquid engine definition (engine_lv1.tres).</summary>
    [Export] public LiquidEnginePartDefinition Engine { get; set; }
    /// <summary>Fin definition (fin_f1.tres), used by the fin check.</summary>
    [Export] public AeroSurfacePartDefinition Fin { get; set; }

    /// <summary>Quit after the test so the log is flushed.</summary>
    [Export] public bool QuitWhenDone { get; set; } = true;

    private readonly List<RocketPart> _parts = new();

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Coordinator, nameof(Coordinator))) return;
        if (!NodeGuard.Require(this, Segment, nameof(Segment))) return;
        CallDeferred(nameof(RunAll));
    }

    private void RunAll()
    {
        Coordinator.Initialize(Segment);
        RunAscent();
        RunFinCheck();
        foreach (RocketPart part in _parts) part.Free();
        if (QuitWhenDone) GetTree().Quit();
    }

    // ---------- Ascent ----------

    private void RunAscent()
    {
        PartTree tree = BuildMinimalRocket(withFins: false, out LiquidEnginePart engine);
        engine.IsArmed = true;
        engine.IsIgnited = true;
        engine.SetThrottle(1f);

        MassProperties mass = ComputeMass(tree);
        Vector2 position = new Vector2(0f, mass.CenterOfMass.Y);
        Vector2 velocity = Vector2.Zero;
        float rotation = 0f;
        bool onPad = true;
        bool burnoutPrinted = false;
        float apogee = 0f;
        float nextPrint = 0f;

        GD.Print("[Physics] === B2 harness: pod + tank_s1 + engine_lv1, full throttle ===");
        GD.Print("[Physics]    t s |  alt m |  v m/s | vy m/s |  mass kg |    q Pa | Mach | burning");
        for (float t = 0f; t <= MaxSimSeconds; t += StepSeconds)
        {
            float altitude = FlightMath.Altitude(position, new Vector2(0f, -Segment.Body.Radius), Segment.Body.Radius);
            rotation = PitchProgram(altitude);
            var input = new PhysicsStepInput
            {
                Position = position, LinearVelocity = velocity, BodyRotationRad = rotation,
                PitchAngleDeg = 90f + Mathf.RadToDeg(rotation), Mass = mass, Parts = tree, DeltaTime = StepSeconds,
            };
            PhysicsStepOutput output = Coordinator.ExecutePhysicsStep(input);

            if (onPad && output.NetForce.Y <= 0f) continue;
            onPad = false;

            velocity += output.NetForce / mass.TotalMass * StepSeconds;
            position += velocity * StepSeconds;
            mass = ComputeMass(tree);
            apogee = Mathf.Max(apogee, output.AltitudeMeters);

            if (!engine.IsIgnited && !burnoutPrinted)
            {
                burnoutPrinted = true;
                GD.Print($"[Physics] Engine off at t = {t:F1} s, alt {output.AltitudeMeters:F0} m, v {velocity.Length():F0} m/s, fuel left {engine.GetAvailableFuel():F1} kg");
            }
            if (t >= nextPrint)
            {
                nextPrint += 1f;
                float vy = FlightMath.VerticalSpeed(velocity, position, new Vector2(0f, -Segment.Body.Radius));
                GD.Print($"[Physics] {t,6:F0} | {output.AltitudeMeters,6:F0} | {velocity.Length(),6:F0} | {vy,6:F0} | {mass.TotalMass,8:F0} | {output.DynamicPressure,7:F0} | {output.MachNumber,4:F2} | {engine.IsIgnited}");
            }
            if (output.AltitudeMeters < 0f)
            {
                GD.Print($"[Physics] Hit the ground at t = {t:F1} s, {velocity.Length():F0} m/s. Apogee {apogee:F0} m.");
                return;
            }
        }
        GD.Print($"[Physics] Time limit reached. Apogee {apogee:F0} m.");
    }

    private float PitchProgram(float altitude)
    {
        if (altitude <= PitchStartAltitudeM) return 0f;
        float k = Mathf.Clamp((altitude - PitchStartAltitudeM) / (PitchEndAltitudeM - PitchStartAltitudeM), 0f, 1f);
        float pitch = Mathf.Lerp(90f, FinalPitchDeg, k);
        return Mathf.DegToRad(pitch - 90f); // negative = nose toward +X (downrange)
    }

    // ---------- Fin stability ----------

    private void RunFinCheck()
    {
        PartTree tree = BuildMinimalRocket(withFins: true, out _);
        MassProperties mass = ComputeMass(tree);
        var position = new Vector2(0f, 5000f);
        var velocity = new Vector2(0f, 300f);
        GD.Print("[Physics] === B2 fin check: moving straight up at 300 m/s, 5 km, engine off ===");
        foreach (float tiltDeg in new[] { 10f, -10f })
        {
            var input = new PhysicsStepInput
            {
                Position = position, LinearVelocity = velocity, BodyRotationRad = Mathf.DegToRad(tiltDeg),
                Mass = mass, Parts = tree, DeltaTime = StepSeconds,
            };
            PhysicsStepOutput output = Coordinator.ExecutePhysicsStep(input);
            bool restoring = Mathf.Sign(output.NetTorqueZ) == -Mathf.Sign(tiltDeg);
            GD.Print($"[Physics] {(restoring ? "OK  " : "FAIL")} tilt {tiltDeg:+0;-0} deg -> torque {output.NetTorqueZ:F0} N*m ({(restoring ? "turns nose back" : "turns nose away")})");
        }
    }

    // ---------- Rocket built by hand (guide A section 7 numbers) ----------

    private PartTree BuildMinimalRocket(bool withFins, out LiquidEnginePart engine)
    {
        var tree = new PartTree();
        engine = Add(tree, new LiquidEnginePart(), Engine ?? (LiquidEnginePartDefinition)TestRocketFactory.GetPart("engine_lv1"), new Vector2I(1, 0));
        FuelTankPart tank = Add(tree, new FuelTankPart(), Tank ?? (FuelTankPartDefinition)TestRocketFactory.GetPart("tank_s1"), new Vector2I(1, 1));
        CommandPodPart pod = Add(tree, new CommandPodPart(), Pod ?? (CommandPodPartDefinition)TestRocketFactory.GetPart("pod_mk1"), new Vector2I(1, 3));
        pod.PayloadMassKg = PayloadMassKg;
        tree.RootPart = pod;
        engine.FeedTanks.Add(tank);

        if (withFins)
        {
            AeroSurfacePartDefinition finDef = Fin ?? DefaultFin();
            Add(tree, new AeroSurfacePart(), finDef, new Vector2I(0, 1));
            Add(tree, new AeroSurfacePart(), finDef, new Vector2I(2, 1));
        }
        return tree;
    }

    /// <summary>fin_f1 numbers of guide A section 7 (TestRocketFactory has no fallback for fins).</summary>
    private static AeroSurfacePartDefinition DefaultFin() => new()
    {
        ModelId = "fin_f1", PartGroup = PartGroup.AeroSurface, DryMass = 40f, LiftCoefficient = 0.8f,
        PlanformArea = 1.5f, FrontalArea = 3.1f, SideArea = 4f, DragCoefficient = 0.3f, MaxDeflectionDeg = 20f,
    };

    /// <summary>Initialize fills tanks, ignitions and propellant from def; without it the part starts empty.</summary>
    private T Add<T>(PartTree tree, T part, PartDefinition def, Vector2I cell) where T : RocketPart
    {
        part.Initialize(def);
        part.Cell = cell;
        part.Tree = tree;
        tree.ActiveParts.Add(part);
        _parts.Add(part);
        return part;
    }

    /// <summary>Mass and center of mass computed here (MassCenterCalculator is A3 work). Inertia is not needed.</summary>
    private static MassProperties ComputeMass(PartTree tree)
    {
        float total = 0f;
        Vector2 weighted = Vector2.Zero;
        foreach (RocketPart part in tree.ActiveParts)
        {
            float m = part.GetCurrentMass();
            total += m;
            weighted += part.GetLocalPosition() * m;
        }
        return new MassProperties { TotalMass = total, CenterOfMass = total > 0f ? weighted / total : Vector2.Zero };
    }
}
