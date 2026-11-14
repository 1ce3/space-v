using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Data of a device installed in a command pod slot.</summary>
public abstract partial class ModuleDefinition : ComponentDefinition
{
    [ExportGroup("Slot")]
    [Export] public ModuleSlotType SlotType { get; set; } = ModuleSlotType.Avionics;

    [ExportGroup("Inventory")]
    /// <summary>Cells taken in the pod inventory grid (rectangles only, no rotation).</summary>
    [Export] public Vector2I GridSize { get; set; } = Vector2I.One;

    [ExportGroup("Faults")]
    /// <summary>0..1: lowers the chance a solar flare breaks this module.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float RadiationResistance { get; set; }
    /// <summary>0..1: chance one reboot attempt brings a Broken module back.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float RebootSuccessChance { get; set; } = 0.3f;
    /// <summary>Battery energy one reboot attempt costs (Wh).</summary>
    [Export] public float RebootEnergyWh { get; set; } = 20f;

    protected ModuleDefinition()
    {
        DryMass = 15f;
        MaxSafeTempK = 350f;
        MinSafeTempK = 230f;
    }
}
