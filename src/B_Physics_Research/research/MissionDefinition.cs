using Godot;

namespace RocketGame.Research;

/// <summary>The campaign mission: payload and the ordered list of segments.</summary>
[GlobalClass]
public partial class MissionDefinition : Resource
{
    /// <summary>Snake_case id, same as the .tres file name.</summary>
    [Export] public string MissionId { get; set; } = "";
    /// <summary>Name shown to the player.</summary>
    [Export] public string DisplayName { get; set; } = "";
    /// <summary>Robot mass carried in the command pod (kg).</summary>
    [Export] public float PayloadMassKg { get; set; } = 300f;
    /// <summary>Optional robot model shown in the pod. May be null.</summary>
    [Export] public PackedScene PayloadScene { get; set; }
    /// <summary>Segments in flight order.</summary>
    [Export] public Godot.Collections.Array<MissionSegmentDefinition> Segments { get; set; } = new();
}
