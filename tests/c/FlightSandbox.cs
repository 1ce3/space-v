using Godot;
using RocketGame.Building;
using RocketGame.Control;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Tests.C;

/// <summary>
/// Flight sandbox: run tests/c/flight_sandbox.tscn (F6). Loads a TestRocketFactory rocket straight into
/// scenes/flight.tscn (no build screen) and starts segment 1 on the pad. With UseGameFlow the run goes
/// through GameFlowManager, so segment changes, debrief and research work as in the game.
/// </summary>
public partial class FlightSandbox : Node
{
    /// <summary>scenes/flight.tscn.</summary>
    [Export] public PackedScene FlightScene { get; set; }
    /// <summary>Mission used when the ResearchCampaignEngine autoload has none.</summary>
    [Export] public MissionDefinition FallbackMission { get; set; }
    /// <summary>false = CreateMinimal (3 parts), true = CreateMarsStack (5 stages).</summary>
    [Export] public bool UseMarsStack { get; set; }
    /// <summary>Run through GameFlowManager (segments, debrief). Off = the flight scene alone, as in C3.</summary>
    [Export] public bool UseGameFlow { get; set; } = true;

    /// <summary>Debug: start at this segment (0 ascent, 1 cruise, 2 Mars EDL). Needs UseGameFlow.</summary>
    [ExportGroup("Debug start")]
    [Export(PropertyHint.Range, "0,2,1")] public int StartSegmentIndex { get; set; }
    /// <summary>Stages fired on the pad before jumping (Mars stack: 4 leaves the lander with its heat shield).</summary>
    [Export(PropertyHint.Range, "0,10,1")] public int PreActivatedStages { get; set; }
    /// <summary>Start with SAS on (StabilityAssist) when the sensors allow it.</summary>
    [Export] public bool StartWithSas { get; set; }

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
        // The campaign mission may be the one-segment tutorial: a later start segment needs the full mission.
        if (StartSegmentIndex > 0 && FallbackMission != null && (mission?.Segments?.Count ?? 0) <= StartSegmentIndex) mission = FallbackMission;
        var run = new MissionRun(mission, rocket);
        FlightScene scene;
        GameFlowManager flow = GameFlowManager.Instance;
        if (UseGameFlow && flow != null)
        {
            scene = flow.StartRun(run);
            if (scene == null) return;
            for (int i = 0; i < PreActivatedStages; i++) scene.Vehicle.Staging.ActivateNextStage();
            if (StartSegmentIndex > 0) flow.DebugJumpToSegment(StartSegmentIndex);
        }
        else
        {
            scene = FlightScene.Instantiate<FlightScene>();
            AddChild(scene);
            scene.Director.StartSegment(run);
        }
        // The estimator needs a few ticks to find its sensors before SAS can switch on.
        if (StartWithSas) GetTree().CreateTimer(0.5).Timeout += () => scene.Vehicle.FlightControl.ToggleSas();

        VehicleRoot v = scene.Vehicle;
        GD.Print($"[Control] Sandbox: {(UseMarsStack ? "Mars stack" : "minimal rocket")}, {v.PartTree.ActiveParts.Count} parts, " +
                 $"{v.MassProps.TotalMass:0} kg, CoM local {v.MassProps.CenterOfMass}, segment {run.SegmentIndex}, phase {scene.Director.Phase}");
    }
}
