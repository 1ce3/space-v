using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Solid rocket booster. Burns from the moment it is armed and cannot be shut down.</summary>
[GlobalClass]
public partial class SolidBoosterPartDefinition : EnginePartDefinition
{
    /// <summary>Solid propellant mass, in kg.</summary>
    [ExportGroup("Solid booster")]
    [Export] public float PropellantMass { get; set; }

    /// <summary>Thrust multiplier over the burn: x = burn progress 0..1, y = multiplier.</summary>
    [Export] public Curve BurnCurve { get; set; }

    /// <summary>Sets the group to SolidBooster so new definitions are valid without extra setup.</summary>
    public SolidBoosterPartDefinition() => PartGroup = PartGroup.SolidBooster;

    /// <inheritdoc />
    public override string GetFormattedTooltip()
        => $"{base.GetFormattedTooltip()}\nPropellant: {PropellantMass:0} kg";
}
