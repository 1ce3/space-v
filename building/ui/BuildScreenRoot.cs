using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Root of scenes/build_screen.tscn. GameFlowManager reads Building to connect DesignConfirmed.</summary>
public partial class BuildScreenRoot : Node3D
{
    /// <summary>The player pressed "Nghiên cứu".</summary>
    [Signal] public delegate void ResearchRequestedEventHandler();

    [Export] public BuildingSystem Building { get; set; }

    public override void _Ready()
    {
        NodeGuard.Require(this, Building, nameof(Building));
    }

    /// <summary>Called by the research button.</summary>
    public void RequestResearch() => EmitSignal(SignalName.ResearchRequested);
}
