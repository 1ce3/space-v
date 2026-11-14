// TEMPORARY. Minimal copies of Dev A's contract types so branch C builds before A's skeleton is merged.
// Compiled only while RocketGame.csproj defines C_STUB_A. When A's skeleton reaches main, C removes
// C_STUB_A from the csproj (and later deletes this file). Never extend these: the real types are A's.
#if C_STUB_A
using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building
{
    public abstract partial class ComponentDefinition : Resource
    {
        public string ModelId { get; set; } = "";
    }

    public abstract partial class PartDefinition : ComponentDefinition
    {
        public PartGroup PartGroup { get; set; }
    }

    public partial class RocketPart : Node3D
    {
        public PartDefinition Definition { get; set; }
    }

    public partial class PartTree : RefCounted
    {
        public List<RocketPart> ActiveParts { get; } = new();
    }

    public class StageGroup
    {
        public int StageIndex { get; set; }
    }

    public partial class AssembledRocket : RefCounted
    {
        public PartTree PartTree { get; set; }
        public List<StageGroup> Stages { get; set; } = new();
        public MassProperties MassProperties { get; set; }
    }
}
#endif
