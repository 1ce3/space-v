using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Abstract base of every definition that occupies grid cells (or a grid edge, for decouplers).</summary>
[GlobalClass]
public abstract partial class PartDefinition : ComponentDefinition
{
    /// <summary>Category used by the neighbor rules and the build palette.</summary>
    [ExportGroup("Grid")]
    [Export] public PartGroup PartGroup { get; set; } = PartGroup.Structural;

    /// <summary>Size in grid cells (x = width, y = height).</summary>
    [Export] public Vector2I Footprint { get; set; } = Vector2I.One;

    /// <summary>Groups allowed directly above this part.</summary>
    [ExportSubgroup("Allowed neighbors")]
    [Export] public PartGroupMask AllowedTop { get; set; } = PartGroupMask.None;

    /// <summary>Groups allowed directly below this part.</summary>
    [Export] public PartGroupMask AllowedBottom { get; set; } = PartGroupMask.None;

    /// <summary>Groups allowed on the left or right of this part (shared by both sides).</summary>
    [Export] public PartGroupMask AllowedSides { get; set; } = PartGroupMask.None;

    /// <summary>Cross-section facing the airflow along the rocket axis, in m^2.</summary>
    [ExportGroup("Aerodynamics")]
    [Export] public float FrontalArea { get; set; } = 3.1f;

    /// <summary>Area facing a sideways airflow, in m^2.</summary>
    [Export] public float SideArea { get; set; } = 4f;

    /// <summary>Dimensionless drag coefficient.</summary>
    [Export] public float DragCoefficient { get; set; } = 0.3f;

    /// <summary>Largest shear force the part survives, in N.</summary>
    [ExportGroup("Structure")]
    [Export] public float MaxShear { get; set; } = 2_000_000f;

    /// <summary>Largest bending moment the part survives, in N*m.</summary>
    [Export] public float MaxBending { get; set; } = 2_000_000f;

    /// <summary>Largest impact speed the part survives, in m/s.</summary>
    [Export] public float ImpactToleranceMps { get; set; } = 10f;

    /// <summary>Visual scene instanced under the part. Falls back to the placeholder for the group when null.</summary>
    [ExportGroup("Visual")]
    [Export] public PackedScene Scene { get; set; }
}
