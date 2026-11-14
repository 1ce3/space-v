using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Abstract base of devices installed in command pod slots (avionics, thermal, power).</summary>
[GlobalClass]
public abstract partial class ModuleDefinition : ComponentDefinition
{
    /// <summary>Slot type this module fits into.</summary>
    [ExportGroup("Module")]
    [Export] public ModuleSlotType SlotType { get; set; }

    /// <summary>Applies the module temperature limits (350 K max, 230 K min).</summary>
    protected ModuleDefinition()
    {
        MaxSafeTempK = 350f;
        MinSafeTempK = 230f;
    }
}
