using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Core tank holding one liquid propellant.</summary>
[GlobalClass]
public partial class FuelTankPartDefinition : PartDefinition
{
    [ExportGroup("FuelTank")]
    [Export] public FuelType PropellantType { get; set; } = FuelType.Kerolox;
    /// <summary>Full propellant mass (kg).</summary>
    [Export] public float FuelCapacity { get; set; } = 1000f;
    /// <summary>Propellant density (kg/m^3).</summary>
    [Export] public float FuelDensity { get; set; } = 1000f;

    public FuelTankPartDefinition()
    {
        PartGroup = PartGroup.CoreTank;
    }
}
