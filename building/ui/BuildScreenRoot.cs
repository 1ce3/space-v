using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Root of scenes/build_screen.tscn. GameFlowManager reads Building to connect DesignConfirmed.</summary>
public partial class BuildScreenRoot : Node3D
{
	/// <summary>The player pressed "Nghiên cứu".</summary>
	[Signal] public delegate void ResearchRequestedEventHandler();
	/// <summary>The player pressed "Back": go to the home hub.</summary>
	[Signal] public delegate void HomeRequestedEventHandler();

	[Export] public BuildingSystem Building { get; set; }

	public override void _Ready()
	{
		NodeGuard.Require(this, Building, nameof(Building));
	}

	/// <summary>Called by the back button. Saves the design first so the hub and the next visit show it.</summary>
	public void RequestHome()
	{
		Building?.SaveDesign();
		EmitSignal(SignalName.HomeRequested);
	}

	/// <summary>Called by the research button. Saves the design first so it is back when the player returns.</summary>
	public void RequestResearch()
	{
		Building?.SaveDesign();
		EmitSignal(SignalName.ResearchRequested);
	}
}
