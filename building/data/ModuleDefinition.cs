using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Data of a device installed in a command pod slot.</summary>
public abstract partial class ModuleDefinition : ComponentDefinition
{
    [ExportGroup("Slot")]
    [Export] public ModuleSlotType SlotType { get; set; } = ModuleSlotType.Avionics;

    protected ModuleDefinition()
    {
        DryMass = 15f;
        MaxSafeTempK = 350f;
        MinSafeTempK = 230f;
    }
}
