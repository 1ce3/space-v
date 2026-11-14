using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Insulation, radiator or heater.</summary>
public abstract class ThermalModule : PodModule
{
    public ThermalModuleDefinition ThermalDefinition => Definition as ThermalModuleDefinition;

    /// <summary>Heat this module can reject now (W).</summary>
    public virtual float GetHeatRejectionW() => 0f;

    /// <summary>Multiplier on absorbed sunlight (1 = no effect).</summary>
    public virtual float GetAbsorptionFactor() => 1f;
}
