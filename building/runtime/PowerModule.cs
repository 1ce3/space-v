using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Battery, solar array or RTG.</summary>
public abstract class PowerModule : PodModule
{
    public PowerModuleDefinition PowerDefinition => Definition as PowerModuleDefinition;

    /// <summary>Electric output now (W).</summary>
    public virtual float GetOutputW(float dynamicPressure, float solarFluxWm2) => 0f;
}
