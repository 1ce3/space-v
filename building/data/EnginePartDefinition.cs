using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Common data of liquid engines and solid boosters.</summary>
public abstract partial class EnginePartDefinition : PartDefinition
{
    /// <summary>Thrust in vacuum at full throttle (N).</summary>
    [ExportGroup("Engine")]
    [Export] public float MaxThrustVac { get; set; } = 100_000f;
    /// <summary>Specific impulse in vacuum (s).</summary>
    [Export] public float IspVacuum { get; set; } = 300f;
    /// <summary>Specific impulse at sea level (s).</summary>
    [Export] public float IspSeaLevel { get; set; } = 270f;
    /// <summary>Max gimbal deflection (deg); 0 = fixed nozzle.</summary>
    [Export(PropertyHint.Range, "0,15,0.5")] public float GimbalRangeDeg { get; set; }

    /// <summary>Thrust at sea level at full throttle (N).</summary>
    public float GetMaxThrustSeaLevel() => IspVacuum > 0f ? MaxThrustVac * IspSeaLevel / IspVacuum : 0f;

    /// <summary>Propellant flow at full throttle (kg/s).</summary>
    public float GetMassFlowRate() => IspVacuum > 0f ? MaxThrustVac / (IspVacuum * FlightMath.G0) : 0f;

}
