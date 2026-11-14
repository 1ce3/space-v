using Godot;
using RocketGame.Building;
using RocketGame.Control;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Tests.C;

/// <summary>
/// C2/C3 sandbox: run tests/c/flight_sandbox.tscn (F6). Loads a TestRocketFactory rocket straight into
/// scenes/flight.tscn (no build screen) and starts segment 1 on the pad.
/// </summary>
public partial class FlightSandbox : Node
{
    /// <summary>scenes/flight.tscn.</summary>
    [Export] public PackedScene FlightScene { get; set; }
    /// <summary>Mission used when the ResearchCampaignEngine autoload has none.</summary>
    [Export] public MissionDefinition FallbackMission { get; set; }
    /// <summary>false = CreateMinimal (3 parts), true = CreateMarsStack (5 stages).</summary>
    [Export] public bool UseMarsStack { get; set; }

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, FlightScene, nameof(FlightScene))) return;
        Callable.From(Start).CallDeferred();
    }

    private void Start()
    {
        AssembledRocket rocket = UseMarsStack ? TestRocketFactory.CreateMarsStack() : TestRocketFactory.CreateMinimal();
        if (rocket == null)
        {
            GD.PushError("[Control] FlightSandbox: no rocket");
            return;
        }

        MissionDefinition mission = ResearchCampaignEngine.Instance?.CurrentMission ?? FallbackMission;
        var run = new MissionRun(mission, rocket);
        var scene = FlightScene.Instantiate<FlightScene>();
        AddChild(scene);
        scene.Director.StartSegment(run);

        VehicleRoot v = scene.Vehicle;
        GD.Print($"[Control] Sandbox: {(UseMarsStack ? "Mars stack" : "minimal rocket")}, {v.PartTree.ActiveParts.Count} parts, " +
                 $"{v.MassProps.TotalMass:0} kg, body mass {v.Mass:0} kg, CoM local {v.MassProps.CenterOfMass}, " +
                 $"CoM world {v.GetWorldCenterOfMass()}, on pad {v.IsOnPad}, phase {scene.Director.Phase}");
    }
}
