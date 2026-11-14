using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Fin. MaxDeflectionDeg > 0 makes it a control fin.</summary>
[GlobalClass]
public partial class AeroSurfacePartDefinition : PartDefinition
{
    [ExportGroup("AeroSurface")]
    [Export] public float LiftCoefficient { get; set; } = 0.8f;
    /// <summary>Lifting area (m^2).</summary>
    [Export] public float PlanformArea { get; set; } = 1.5f;
    /// <summary>0 = fixed fin.</summary>
    [Export] public float MaxDeflectionDeg { get; set; } = 0f;

    public AeroSurfacePartDefinition()
    {
        PartGroup = PartGroup.AeroSurface;
    }

    /// <summary>True when the fin can be steered.</summary>
    public bool IsControlSurface => MaxDeflectionDeg > 0f;
}
