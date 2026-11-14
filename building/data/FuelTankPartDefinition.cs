using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Propellant tank. Engines of the matching propellant type draw from it.</summary>
[GlobalClass]
public partial class FuelTankPartDefinition : PartDefinition
{
    /// <summary>Propellant this tank holds.</summary>
    [ExportGroup("Fuel tank")]
    [Export] public FuelType PropellantType { get; set; } = FuelType.Kerolox;

    /// <summary>Propellant mass when full, in kg.</summary>
    [Export] public float FuelCapacity { get; set; }

    /// <summary>Propellant density, in kg/m^3.</summary>
    [Export] public float FuelDensity { get; set; } = 1000f;

    /// <summary>Sets the group to CoreTank so new definitions are valid without extra setup.</summary>
    public FuelTankPartDefinition() => PartGroup = PartGroup.CoreTank;

    /// <inheritdoc />
    public override string GetFormattedTooltip()
        => $"{base.GetFormattedTooltip()}\n{PropellantType}: {FuelCapacity:0} kg";
}
