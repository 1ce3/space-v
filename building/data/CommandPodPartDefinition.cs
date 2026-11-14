using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Command pod. Carries the payload and has module slots for avionics, thermal and power devices.</summary>
[GlobalClass]
public partial class CommandPodPartDefinition : PartDefinition
{
    /// <summary>Number of Avionics module slots.</summary>
    [ExportGroup("Command pod")]
    [Export] public int AvionicsSlots { get; set; }

    /// <summary>Number of Thermal module slots.</summary>
    [Export] public int ThermalSlots { get; set; }

    /// <summary>Number of Power module slots.</summary>
    [Export] public int PowerSlots { get; set; }

    /// <summary>Largest sustained acceleration the payload survives, in g.</summary>
    [Export] public float MaxGForce { get; set; } = 8f;

    /// <summary>Sets the group to CommandPod so new definitions are valid without extra setup.</summary>
    public CommandPodPartDefinition() => PartGroup = PartGroup.CommandPod;

    /// <inheritdoc />
    public override string GetFormattedTooltip()
        => $"{base.GetFormattedTooltip()}\nSlots: avionics {AvionicsSlots}, thermal {ThermalSlots}, power {PowerSlots}";
}
