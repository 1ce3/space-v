using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Attitude sensor or actuator: IMU, star tracker, reaction wheel or RCS thruster.</summary>
[GlobalClass]
public partial class AcsModuleDefinition : ModuleDefinition
{
    /// <summary>Which device this is.</summary>
    [ExportGroup("ACS")]
    [Export] public AcsDeviceKind Kind { get; set; } = AcsDeviceKind.Imu;

    /// <summary>Maximum torque in N*m. For RCS this is also the translational force in N.</summary>
    [Export] public float ControlAuthority { get; set; }

    /// <summary>Delay between measurement and response, in ms.</summary>
    [Export] public float ResponseLatencyMs { get; set; }

    /// <summary>Sensor accuracy, in arcseconds.</summary>
    [Export] public float SensorAccuracyArcsec { get; set; }

    /// <summary>Reaction wheel momentum capacity, in N*m*s.</summary>
    [Export] public float MaxMomentumStorageNms { get; set; }

    /// <summary>Gyro drift, in degrees per minute.</summary>
    [Export] public float DriftRateDegPerMin { get; set; }

    /// <summary>RCS monopropellant capacity, in kg.</summary>
    [Export] public float MonopropCapacityKg { get; set; }

    /// <summary>SAS modes this device enables (Off is always available).</summary>
    [Export] public SasModeMask EnabledSasModes { get; set; } = SasModeMask.None;

    /// <summary>Sets the slot type to Avionics.</summary>
    public AcsModuleDefinition() => SlotType = ModuleSlotType.Avionics;
}
