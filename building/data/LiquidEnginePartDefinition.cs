using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Throttleable, restartable liquid engine fed by tanks.</summary>
[GlobalClass]
public partial class LiquidEnginePartDefinition : EnginePartDefinition
{
    [ExportGroup("LiquidEngine")]
    [Export] public FuelType PropellantType { get; set; } = FuelType.Kerolox;
    /// <summary>Lowest non-zero throttle (0..1).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float MinThrottle { get; set; } = 0.5f;
    /// <summary>Number of ignitions available.</summary>
    [Export] public int MaxIgnitions { get; set; } = 1;

    public LiquidEnginePartDefinition()
    {
        PartGroup = PartGroup.LiquidEngine;
    }

}
