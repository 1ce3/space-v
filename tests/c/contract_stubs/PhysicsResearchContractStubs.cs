// TEMPORARY. Minimal copies of Dev B's contract types so branch C builds before B's skeleton is merged.
// Compiled only while RocketGame.csproj defines C_STUB_B. When B's skeleton reaches main, C removes
// C_STUB_B from the csproj (and later deletes this file). Never extend these: the real types are B's.
#if C_STUB_B
using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Physics
{
    public partial class CelestialBodyDefinition : Resource
    {
        public string BodyId { get; set; } = "";
    }

    public partial class PhysicsCoordinator : Node
    {
    }

    public struct CruiseStepResult
    {
        public int Day;
    }

    public class CruiseSimulator
    {
    }
}

namespace RocketGame.Research
{
    public partial class MissionSegmentDefinition : Resource
    {
        public SegmentKind Kind { get; set; }
    }

    public partial class MissionDefinition : Resource
    {
        public string MissionId { get; set; } = "";
        public Godot.Collections.Array<MissionSegmentDefinition> Segments { get; set; } = new();
    }

    public class RewardBreakdown
    {
        public int Total { get; set; }
    }
}
#endif
