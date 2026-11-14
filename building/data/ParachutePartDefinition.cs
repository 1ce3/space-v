using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Parachute: deploys only without a fairing, below q and Mach limits.</summary>
[GlobalClass]
public partial class ParachutePartDefinition : PartDefinition
{
    /// <summary>Drag area when open, Cd included (m^2).</summary>
    [ExportGroup("Parachute")]
    [Export] public float DeployedDragArea { get; set; } = 80f;
    /// <summary>Opening above this q breaks it (Pa).</summary>
    [Export] public float MaxDeployDynamicPressure { get; set; } = 1500f;
    /// <summary>Opening above this Mach breaks it.</summary>
    [Export] public float MaxDeployMach { get; set; } = 2.5f;
    /// <summary>Strength of its self-separating joint (N).</summary>
    [Export] public float AttachmentStrength { get; set; } = 150_000f;
    /// <summary>Seconds from Deploy to a fully open canopy; drag grows with the opened fraction squared.</summary>
    [Export] public float InflationTimeS { get; set; } = 3f;

    public ParachutePartDefinition()
    {
        PartGroup = PartGroup.Parachute;
    }
}
