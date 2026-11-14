using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Radioisotope heater unit: constant heat, no power.</summary>
public class RadioisotopeHeaterModule : ThermalModule
{
    /// <summary>Constant heat output (W).</summary>
    public float EmitConstantDecayHeat() => ThermalDefinition?.HeaterOutputW ?? 0f;
}
