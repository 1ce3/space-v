using System;

namespace RocketGame.Core;

public enum FuelType { Monopropellant, Kerolox, Hydrolox, Methalox }
public enum ComponentState { Idle, Active, Overheated, Frozen, Broken }
public enum PartGroup { CommandPod, Fairing, Parachute, HeatShield, CoreTank, LiquidEngine, SolidBooster, AeroSurface, Structural }
public enum Side { Top, Bottom, Left, Right }
public enum ModuleSlotType { Avionics, Thermal, Power }
public enum AcsDeviceKind { Imu, StarTracker, ReactionWheel, RcsThruster }
public enum ThermalDeviceKind { PassiveInsulation, ActiveRadiator, RadioisotopeHeater }
public enum PowerDeviceKind { Battery, SolarArray, Rtg }
public enum SasMode { Off, StabilityAssist, HoldPitch, Prograde, Retrograde }
public enum SeverCause { Jettison, Break }
public enum FailureCause { None, Shear, Bending, JointOverload, Thermal, Collision, TargetNotReached, InsufficientDeltaV, TippedOver, Aborted }
public enum FlightEventType { LightningStrike, CryoLeak, DebrisWarning, SeuGlitch, WindShear, MaxQExceeded, GLimitExceeded, EngineFlameout, ComponentOverheat, PowerBrownout }
public enum FlightPhase { PreLaunch, PoweredAscent, Coast, OrbitReached, Entry, Descent, Landed, Failed }
public enum GameState { Build, Flight, Debrief, Cruise, Victory, Research }
public enum MissionOutcome { Success, Failed, Aborted }
public enum SegmentKind { EarthAscent, Cruise, MarsEdl }
public enum ParachuteState { Stowed, Deployed, Cut }

/// <summary>Export-friendly set of PartGroup values. Bit i = PartGroup with value i.</summary>
[Flags]
public enum PartGroupMask
{
    None = 0, CommandPod = 1 << 0, Fairing = 1 << 1, Parachute = 1 << 2, HeatShield = 1 << 3,
    CoreTank = 1 << 4, LiquidEngine = 1 << 5, SolidBooster = 1 << 6, AeroSurface = 1 << 7, Structural = 1 << 8,
}

/// <summary>Export-friendly set of SasMode values (Off is always available).</summary>
[Flags]
public enum SasModeMask { None = 0, StabilityAssist = 1 << 0, HoldPitch = 1 << 1, Prograde = 1 << 2, Retrograde = 1 << 3 }

/// <summary>Export-friendly set of FlightEventType values. Bit i = FlightEventType with value i.</summary>
[Flags]
public enum FlightEventMask
{
    None = 0, LightningStrike = 1 << 0, CryoLeak = 1 << 1, DebrisWarning = 1 << 2, SeuGlitch = 1 << 3, WindShear = 1 << 4,
    MaxQExceeded = 1 << 5, GLimitExceeded = 1 << 6, EngineFlameout = 1 << 7, ComponentOverheat = 1 << 8, PowerBrownout = 1 << 9,
}

public static class MaskExtensions
{
    public static PartGroupMask ToMask(this PartGroup g) => (PartGroupMask)(1 << (int)g);
    public static bool Has(this PartGroupMask m, PartGroup g) => (m & g.ToMask()) != 0;
    public static SasModeMask ToMask(this SasMode s) => s == SasMode.Off ? SasModeMask.None : (SasModeMask)(1 << ((int)s - 1));
    public static bool Has(this SasModeMask m, SasMode s) => s == SasMode.Off || (m & s.ToMask()) != 0;
    public static FlightEventMask ToMask(this FlightEventType t) => (FlightEventMask)(1 << (int)t);
    public static bool Has(this FlightEventMask m, FlightEventType t) => (m & t.ToMask()) != 0;
}
