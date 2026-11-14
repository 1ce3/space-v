using Godot;

namespace RocketGame.Physics;

/// <summary>Shared tuning values of the physics system. Saved as physics_settings.tres.</summary>
[GlobalClass]
public partial class PhysicsSettings : Resource
{
    /// <summary>Drag coefficient used for the side area of every part.</summary>
    [ExportGroup("Aerodynamics")]
    [Export(PropertyHint.Range, "0,3,0.01")] public float SideDragCoefficient { get; set; } = 1.0f;
    /// <summary>Drag multiplier by Mach number (x = Mach, y = multiplier). May be null: SampleMachDrag then uses built-in defaults.</summary>
    [Export] public Curve MachDragCurve { get; set; }
    /// <summary>Fin stall angle (deg): lift magnitude is clamped at this angle of attack.</summary>
    [Export(PropertyHint.Range, "1,90,0.5")] public float FinStallDeg { get; set; } = 25f;
    /// <summary>Half-angle of the cone (deg) in which fairing or heat shield counts as facing the airflow.</summary>
    [Export(PropertyHint.Range, "0,90,0.5")] public float ShieldingConeDeg { get; set; } = 30f;
    /// <summary>Dynamic pressure warning limit (Pa).</summary>
    [Export] public float MaxQLimit { get; set; } = 40000f;

    /// <summary>Nose radius used by the Sutton-Graves heat flux (m).</summary>
    [ExportGroup("Flight heating")]
    [Export] public float NoseRadiusM { get; set; } = 1.0f;
    /// <summary>Fraction of convective heat that enters the vehicle (0..1).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float HeatPartition { get; set; } = 0.5f;
    /// <summary>Emissivity of every part for radiative cooling (0..1).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float PartEmissivity { get; set; } = 0.8f;
    /// <summary>Heat put into a burning engine at full throttle (W). P1.</summary>
    [Export] public float EngineHeatW { get; set; } = 20000f;

    /// <summary>Specific impulse of RCS monopropellant (s).</summary>
    [ExportGroup("RCS")]
    [Export] public float RcsIsp { get; set; } = 220f;
    /// <summary>Lever arm used to turn RCS torque into thruster force (m).</summary>
    [Export] public float RcsLeverArmM { get; set; } = 1.0f;

    /// <summary>Debris is freed beyond this distance from the active camera (m).</summary>
    [ExportGroup("Debris")]
    [Export] public float DebrisDespawnDistanceM { get; set; } = 3000f;

    /// <summary>Fraction of the pod PowerDrawW used during cruise (0..1).</summary>
    [ExportGroup("Cruise")]
    [Export(PropertyHint.Range, "0,1,0.01")] public float CruiseDutyFactor { get; set; } = 0.3f;
    /// <summary>Solar absorptivity of the pod surface (0..1).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float Absorptivity { get; set; } = 0.3f;
    /// <summary>Emissivity of the pod before insulation (0..1).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float BaseEmissivity { get; set; } = 0.8f;
    /// <summary>Pod area facing the Sun (m^2).</summary>
    [Export] public float PodCrossAreaM2 { get; set; } = 3.1f;
    /// <summary>Pod area that radiates heat (m^2).</summary>
    [Export] public float PodRadiatingAreaM2 { get; set; } = 12f;
    /// <summary>IMU drift per cruise day expressed as minutes of flight drift.</summary>
    [Export] public float CruiseDriftMinutesPerDay { get; set; } = 1.0f;

    /// <summary>Chance per second of a cryo leak while a Hydrolox or Methalox tank is attached. P1.</summary>
    [ExportGroup("Hazards")]
    [Export(PropertyHint.Range, "0,1,0.0001")] public float CryoLeakChancePerSec { get; set; } = 0.002f;
    /// <summary>Chance per second of a debris warning above 80 km. P1.</summary>
    [Export(PropertyHint.Range, "0,1,0.0001")] public float DebrisChancePerSec { get; set; } = 0.001f;
    /// <summary>Time the player has to dodge a debris warning (s). P1.</summary>
    [Export] public float DebrisWarningSeconds { get; set; } = 8f;
    /// <summary>Chance per cruise day of a debris warning. P1.</summary>
    [Export(PropertyHint.Range, "0,1,0.0001")] public float DebrisChancePerDay { get; set; } = 0.005f;
    /// <summary>Chance per cruise day of a single-event upset. P1.</summary>
    [Export(PropertyHint.Range, "0,1,0.0001")] public float SeuChancePerDay { get; set; } = 0.003f;

    private const float MachTransonic = 1.1f;
    private const float MachHigh = 3f;
    private const float DragSubsonic = 1.0f;
    private const float DragTransonic = 1.8f;
    private const float DragHigh = 1.2f;

    /// <summary>Drag multiplier at the given Mach number. Uses MachDragCurve, or the guide defaults when it is null.</summary>
    public float SampleMachDrag(float mach)
    {
        if (MachDragCurve != null) return MachDragCurve.Sample(mach);
        if (mach <= 0f) return DragSubsonic;
        if (mach < MachTransonic) return Mathf.Lerp(DragSubsonic, DragTransonic, mach / MachTransonic);
        if (mach < MachHigh) return Mathf.Lerp(DragTransonic, DragHigh, (mach - MachTransonic) / (MachHigh - MachTransonic));
        return DragHigh;
    }
}
