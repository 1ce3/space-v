using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Passive structural part.</summary>
public partial class StructuralPart : RocketPart
{
    public StructuralPartDefinition StructuralDefinition => Definition as StructuralPartDefinition;
}
