using Godot;
using RocketGame.Core;

using Side = RocketGame.Core.Side;

namespace RocketGame.Building;

/// <summary>Data of a part that sits on the build grid.</summary>
public abstract partial class PartDefinition : ComponentDefinition
{
    [ExportGroup("Grid")]
    [Export] public PartGroup PartGroup { get; set; } = PartGroup.Structural;
    /// <summary>Size in cells (width, height).</summary>
    [Export] public Vector2I Footprint { get; set; } = Vector2I.One;
    /// <summary>Groups allowed to touch this part's top edge.</summary>
    [Export] public PartGroupMask AllowedTop { get; set; }
    /// <summary>Groups allowed to touch this part's bottom edge.</summary>
    [Export] public PartGroupMask AllowedBottom { get; set; }
    /// <summary>Groups allowed to touch this part's left and right edges.</summary>
    [Export] public PartGroupMask AllowedSides { get; set; }

    /// <summary>Area facing the flow when nose-first (m^2).</summary>
    [ExportGroup("Aero")]
    [Export] public float FrontalArea { get; set; } = 3.1f;
    /// <summary>Area facing the flow when side-on (m^2).</summary>
    [Export] public float SideArea { get; set; } = 4f;
    [Export] public float DragCoefficient { get; set; } = 0.3f;

    /// <summary>Max lateral load at the parent joint (N).</summary>
    [ExportGroup("Structure")]
    [Export] public float MaxShear { get; set; } = 2_000_000f;
    /// <summary>Max bending moment at the parent joint (N*m).</summary>
    [Export] public float MaxBending { get; set; } = 2_000_000f;
    /// <summary>Max ground contact speed without breaking (m/s).</summary>
    [Export] public float ImpactToleranceMps { get; set; } = 10f;

    /// <summary>Visual scene (root: PartVisual). Null = placeholder of the group.</summary>
    [ExportGroup("Visual")]
    [Export] public PackedScene Scene { get; set; }

    /// <summary>True when a part of the given group may touch the given side of this part.</summary>
    public bool Allows(Side side, PartGroup group) => side switch
    {
        Side.Top => AllowedTop.Has(group),
        Side.Bottom => AllowedBottom.Has(group),
        _ => AllowedSides.Has(group),
    };

}
