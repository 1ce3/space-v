using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Throttleable, restartable engine that burns propellant from feed tanks.</summary>
[GlobalClass]
public partial class LiquidEnginePartDefinition : EnginePartDefinition
{
    /// <summary>Propellant the engine burns. Feed tanks must match.</summary>
    [ExportGroup("Liquid engine")]
    [Export] public FuelType PropellantType { get; set; } = FuelType.Kerolox;

    /// <summary>Lowest throttle while ignited (0..1).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float MinThrottle { get; set; } = 0.1f;

    /// <summary>Number of times the engine can be ignited.</summary>
    [Export] public int MaxIgnitions { get; set; } = 1;

    /// <summary>Sets the group to LiquidEngine so new definitions are valid without extra setup.</summary>
    public LiquidEnginePartDefinition() => PartGroup = PartGroup.LiquidEngine;
}
