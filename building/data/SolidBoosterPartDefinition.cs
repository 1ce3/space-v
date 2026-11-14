using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Solid booster: burns from ignition until empty.</summary>
[GlobalClass]
public partial class SolidBoosterPartDefinition : EnginePartDefinition
{
    /// <summary>Solid propellant mass (kg).</summary>
    [ExportGroup("SolidBooster")]
    [Export] public float PropellantMass { get; set; } = 1500f;
    /// <summary>x = burn progress 0..1, y = thrust factor.</summary>
    [Export] public Curve BurnCurve { get; set; }

    public SolidBoosterPartDefinition()
    {
        PartGroup = PartGroup.SolidBooster;
    }

    public override string GetFormattedTooltip() => base.GetFormattedTooltip() + $"\nPropellant: {N(PropellantMass, "0")} kg (cannot be shut down)";
}
