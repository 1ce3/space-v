using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Command pod: carries the robot payload and the module slots.</summary>
[GlobalClass]
public partial class CommandPodPartDefinition : PartDefinition
{
    [ExportGroup("CommandPod")]
    [Export] public int AvionicsSlots { get; set; } = 1;
    [Export] public int ThermalSlots { get; set; } = 0;
    [Export] public int PowerSlots { get; set; } = 1;
    /// <summary>Phase 2: size of the module inventory grid (columns, rows). Replaces the three slot counts in A10.</summary>
    [Export] public Vector2I InventorySize { get; set; } = new(2, 2);
    /// <summary>G limit for the crew-less payload.</summary>
    [Export] public float MaxGForce { get; set; } = 8f;

    public CommandPodPartDefinition()
    {
        PartGroup = PartGroup.CommandPod;
    }

    /// <summary>Number of slots of a type.</summary>
    public int GetSlotCount(ModuleSlotType slot) => slot switch
    {
        ModuleSlotType.Avionics => AvionicsSlots,
        ModuleSlotType.Thermal => ThermalSlots,
        _ => PowerSlots,
    };

    public override string GetFormattedTooltip() => base.GetFormattedTooltip() + $"\nSlots: {AvionicsSlots} avionics, {ThermalSlots} thermal, {PowerSlots} power\nMax load: {N(MaxGForce, "0.#")} G";
}
