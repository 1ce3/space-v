using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Fixed fin.</summary>
public partial class AeroSurfacePart : RocketPart
{
    public AeroSurfacePartDefinition AeroDefinition => Definition as AeroSurfacePartDefinition;
}
