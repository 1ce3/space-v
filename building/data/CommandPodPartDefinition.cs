using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Command pod: carries the robot payload and the module slots.</summary>
[GlobalClass]
public partial class CommandPodPartDefinition : PartDefinition
{
    [ExportGroup("CommandPod")]
    /// <summary>Size of the module inventory grid (columns, rows). Every module, whatever its group, goes in this grid.</summary>
    [Export] public Vector2I InventorySize { get; set; } = new(2, 2);
    /// <summary>G limit for the crew-less payload.</summary>
    [Export] public float MaxGForce { get; set; } = 8f;

    public CommandPodPartDefinition()
    {
        PartGroup = PartGroup.CommandPod;
        AcceptsMaterial = true;
    }


    public override string GetFormattedTooltip() => base.GetFormattedTooltip() + $"\nModule grid: {InventorySize.X} x {InventorySize.Y}\nMax load: {N(MaxGForce, "0.#")} G";
}
