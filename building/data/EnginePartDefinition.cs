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
    /// <summary>
    /// Thrust of the real engine (kN, vacuum), shown in the tooltip only. The game flies with MaxThrustVac, which is
    /// compressed for the small rockets of this game (140 kN x sqrt(real / 1000 kN)). 0 = fictional engine.
    /// </summary>
    [Export] public float RealThrustVacKN { get; set; }
    /// <summary>Max gimbal deflection (deg); 0 = fixed nozzle.</summary>
    [Export(PropertyHint.Range, "0,15,0.5")] public float GimbalRangeDeg { get; set; }

    /// <summary>Thrust at sea level at full throttle (N).</summary>
    public float GetMaxThrustSeaLevel() => IspVacuum > 0f ? MaxThrustVac * IspSeaLevel / IspVacuum : 0f;

    /// <summary>Propellant flow at full throttle (kg/s).</summary>
    public float GetMassFlowRate() => IspVacuum > 0f ? MaxThrustVac / (IspVacuum * FlightMath.G0) : 0f;

    public override string GetFormattedTooltip() => base.GetFormattedTooltip() + $"\nThrust: {N(MaxThrustVac / 1000f, "0")} kN vac, {N(GetMaxThrustSeaLevel() / 1000f, "0")} kN sea level" + (RealThrustVacKN > 0f ? $"\nReal engine: {N(RealThrustVacKN, "#,0")} kN vac (scaled down for the game)" : "") + $"\nIsp: {N(IspVacuum, "0")} s vac, {N(IspSeaLevel, "0")} s sea level" + (GimbalRangeDeg > 0f ? $"\nGimbal: ±{N(GimbalRangeDeg, "0.#")}°" : "");
}
