using Godot;
using RocketGame.Core;

namespace RocketGame.Building;


/// <summary>Attitude control device: IMU, star tracker, reaction wheel or RCS.</summary>
[GlobalClass]
public partial class AcsModuleDefinition : ModuleDefinition
{
    [ExportGroup("Acs")]
    [Export] public AcsDeviceKind Kind { get; set; } = AcsDeviceKind.Imu;
    /// <summary>Max torque (N*m); for RCS also the translation force (N).</summary>
    [Export] public float ControlAuthority { get; set; } = 0f;
    /// <summary>Sensor filter time constant (ms).</summary>
    [Export] public float ResponseLatencyMs { get; set; } = 80f;
    /// <summary>Sensor noise, 1 sigma (arcsec).</summary>
    [Export] public float SensorAccuracyArcsec { get; set; } = 1800f;
    /// <summary>Reaction wheel capacity (N*m*s).</summary>
    [Export] public float MaxMomentumStorageNms { get; set; } = 0f;
    /// <summary>IMU drift (deg/min).</summary>
    [Export] public float DriftRateDegPerMin { get; set; } = 0f;
    /// <summary>RCS propellant (kg).</summary>
    [Export] public float MonopropCapacityKg { get; set; } = 0f;
    /// <summary>SAS modes this sensor allows.</summary>
    [Export] public SasModeMask EnabledSasModes { get; set; } = SasModeMask.None;

    public AcsModuleDefinition()
    {
        SlotType = ModuleSlotType.Avionics;
    }

    /// <summary>True for IMU and star tracker.</summary>
    public bool IsSensor => Kind == AcsDeviceKind.Imu || Kind == AcsDeviceKind.StarTracker;
}
