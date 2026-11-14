using Godot;
using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>Root of scenes/flight.tscn. Gives GameFlowManager typed access to the segment controllers.</summary>
public partial class FlightScene : Node3D
{
    [Export] public FlightDirector Director { get; set; }
    [Export] public CruiseController Cruise { get; set; }
    [Export] public VehicleRoot Vehicle { get; set; }

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Director, nameof(Director))) return;
        if (!NodeGuard.Require(this, Cruise, nameof(Cruise))) return;
        NodeGuard.Require(this, Vehicle, nameof(Vehicle));
    }
}
