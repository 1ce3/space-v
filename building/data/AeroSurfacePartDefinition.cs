using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Fin or other aerodynamic surface. Controllable when MaxDeflectionDeg is greater than zero.</summary>
[GlobalClass]
public partial class AeroSurfacePartDefinition : PartDefinition
{
    /// <summary>Lift coefficient per radian of angle of attack.</summary>
    [ExportGroup("Aero surface")]
    [Export] public float LiftCoefficient { get; set; } = 0.8f;

    /// <summary>Planform area, in m^2.</summary>
    [Export] public float PlanformArea { get; set; } = 1.5f;

    /// <summary>Largest control deflection either side of neutral, in degrees. Zero means a fixed fin.</summary>
    [Export] public float MaxDeflectionDeg { get; set; }

    /// <summary>Sets the group to AeroSurface so new definitions are valid without extra setup.</summary>
    public AeroSurfacePartDefinition() => PartGroup = PartGroup.AeroSurface;
}
