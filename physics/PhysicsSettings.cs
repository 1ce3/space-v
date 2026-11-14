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
    /// <summary>
    /// Balance switch, default true. False (guide B 4.3.2): every unshielded part adds its frontal drag (a stack of 14 parts then
    /// cannot pass about 17 km). True: only parts whose leading face
    /// (top when flying nose first, bottom when flying base first) has no other part on it add frontal drag; side drag is unchanged.
    /// </summary>
    [Export] public bool FrontalDragOnlyExposed { get; set; } = true;
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
    /// <summary>Solar absorptivity of the pod surface (0..1). 0.5 makes an uninsulated pod freeze in the second half of the cruise (guide B 5 goal).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float Absorptivity { get; set; } = 0.5f;
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
    /// <summary>Fuel lost by a leaking tank until SealLeak (kg/s). P1.</summary>
    [Export] public float CryoLeakRateKgPerSec { get; set; } = 3f;
    /// <summary>Chance per second of a debris warning above DebrisMinAltitudeM. P1.</summary>
    [Export(PropertyHint.Range, "0,1,0.0001")] public float DebrisChancePerSec { get; set; } = 0.001f;
    /// <summary>Debris warnings only roll above this altitude (m). P1.</summary>
    [Export] public float DebrisMinAltitudeM { get; set; } = 80000f;
    /// <summary>Change of horizontal speed that counts as dodging the debris (m/s). P1.</summary>
    [Export] public float DebrisDodgeSpeedMps { get; set; } = 5f;
    /// <summary>Time the player has to dodge a debris warning (s). P1.</summary>
    [Export] public float DebrisWarningSeconds { get; set; } = 8f;
    /// <summary>Chance per cruise day of a debris warning. P1.</summary>
    [Export(PropertyHint.Range, "0,1,0.0001")] public float DebrisChancePerDay { get; set; } = 0.005f;
    /// <summary>Chance per cruise day of a single-event upset. P1.</summary>
    [Export(PropertyHint.Range, "0,1,0.0001")] public float SeuChancePerDay { get; set; } = 0.003f;
    /// <summary>The command pod breaks (JointOverload) above MaxGForce times this factor. P1.</summary>
    [Export(PropertyHint.Range, "1,5,0.05")] public float PodBreakGFactor { get; set; } = 1.5f;
    /// <summary>Share of a compressive load a decoupler joint counts against its strength (tension counts fully).</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float JointCompressionFactor { get; set; } = 0.25f;

    /// <summary>
    /// Aeroshell (C6): while a heat shield leads, the parts it shields sit in its wake and get only this share of their cross-flow drag.
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float HeatShieldWakeFactor { get; set; } = 0.15f;
    /// <summary>
    /// Aeroshell (C6): restoring moment coefficient of a leading heat shield. Torque = coeff * q * shield frontal area *
    /// shield-to-CoM distance * sin(angle off base-first), turning the shield back into the flow like a real capsule.
    /// </summary>
    [Export(PropertyHint.Range, "0,5,0.05")] public float HeatShieldStabilityCoefficient { get; set; } = 1.5f;

    /// <summary>Fewest planned cruise events (PHASE2 5.2, B11).</summary>
    [ExportGroup("Cruise events")]
    [Export(PropertyHint.Range, "0,10,1")] public int CruiseEventsMin { get; set; } = 2;
    /// <summary>Most planned cruise events.</summary>
    [Export(PropertyHint.Range, "0,10,1")] public int CruiseEventsMax { get; set; } = 4;
    /// <summary>Smallest gap between two planned events (days).</summary>
    [Export(PropertyHint.Range, "0,100,1")] public int CruiseEventMinGapDays { get; set; } = 20;
    /// <summary>No event before this day.</summary>
    [Export(PropertyHint.Range, "1,100,1")] public int CruiseEventFirstDay { get; set; } = 5;
    /// <summary>No event in the last days of the cruise.</summary>
    [Export(PropertyHint.Range, "0,100,1")] public int CruiseEventLastDayMargin { get; set; } = 5;
    /// <summary>Relative weights of the planned event types.</summary>
    [Export] public float SolarFlareWeight { get; set; } = 40f;
    [Export] public float MeteorFieldWeight { get; set; } = 40f;
    [Export] public float InstrumentFailureWeight { get; set; } = 20f;
    /// <summary>Flare damage chance of an unshielded, unhardened module (5.3).</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float FlareBaseDamageChance { get; set; } = 0.5f;
    /// <summary>Flare damage chance factor for modules the player powered down after a warning.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float FlarePoweredDownFactor { get; set; } = 0.3f;
    /// <summary>Chance of an incident during the transfer burn (5.5): engine flameout or tank leak.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float TransferBurnEventChance { get; set; } = 0.3f;
    /// <summary>Extra delta-v of the relight after a transfer-burn flameout, as a share of the required delta-v.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float TransferRelightDeltaVFraction { get; set; } = 0.05f;
    /// <summary>Share of its fuel a leaking tank loses per cruise day until the leak is sealed.</summary>
    [Export(PropertyHint.Range, "0,1,0.005")] public float CruiseLeakFractionPerDay { get; set; } = 0.02f;

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
