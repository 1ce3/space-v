using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Abstract base for liquid engines and solid boosters.</summary>
[GlobalClass]
public abstract partial class EnginePartDefinition : PartDefinition
{
    /// <summary>Thrust in vacuum at full throttle, in N.</summary>
    [ExportGroup("Engine")]
    [Export] public float MaxThrustVac { get; set; }

    /// <summary>Specific impulse in vacuum, in s.</summary>
    [Export] public float IspVacuum { get; set; } = 300f;

    /// <summary>Specific impulse at sea level, in s.</summary>
    [Export] public float IspSeaLevel { get; set; } = 270f;

    /// <summary>Maximum gimbal deflection either side of center, in degrees.</summary>
    [Export] public float GimbalRangeDeg { get; set; }

    /// <summary>Thrust at sea level and full throttle, in N (scaled by the Isp ratio).</summary>
    public float GetMaxThrustSeaLevel()
        => IspVacuum > 0f ? MaxThrustVac * IspSeaLevel / IspVacuum : 0f;

    /// <summary>Propellant mass flow at full throttle, in kg/s.</summary>
    public float GetMassFlowRate()
        => IspVacuum > 0f ? MaxThrustVac / (IspVacuum * FlightMath.G0) : 0f;

    /// <inheritdoc />
    public override string GetFormattedTooltip()
        => $"{base.GetFormattedTooltip()}\nThrust (vac): {MaxThrustVac / 1000f:0} kN | Isp: {IspSeaLevel:0}/{IspVacuum:0} s";
}
