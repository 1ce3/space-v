using Godot;
using RocketGame.Building;
using RocketGame.Core;
using FlightSceneNode = RocketGame.Control.FlightScene;

namespace RocketGame.Control;

/// <summary>
/// Autoload: game state machine and manual scene switching (C_CONTROL.md 4.1).
/// Scenes are instanced by hand so their signals can be connected before _Ready.
/// </summary>
public partial class GameFlowManager : Node
{
    [Signal] public delegate void StateChangedEventHandler(GameState state);

    public static GameFlowManager Instance { get; private set; }

    /// <summary>scenes/build_screen.tscn (root: BuildScreenRoot, owned by A).</summary>
    [ExportGroup("Scenes")]
    [Export] public PackedScene BuildScreen { get; set; }
    /// <summary>scenes/research_screen.tscn (root: ResearchScreenRoot, owned by B).</summary>
    [Export] public PackedScene ResearchScreen { get; set; }
    /// <summary>scenes/flight.tscn (root: FlightScene). Same scene for all 3 segments.</summary>
    [Export] public PackedScene FlightScene { get; set; }
    /// <summary>scenes/debrief.tscn (root: DebriefScreen).</summary>
    [Export] public PackedScene DebriefScene { get; set; }
    /// <summary>scenes/victory.tscn (root: VictoryScreen).</summary>
    [Export] public PackedScene VictoryScene { get; set; }

    [ExportGroup("Debug")]
    [Export] public bool DebugLog { get; set; }

    /// <summary>Current game state.</summary>
    public GameState State { get; private set; } = GameState.Build;
    /// <summary>The run in progress, or null outside Flight/Cruise/Debrief.</summary>
    public MissionRun CurrentRun { get; private set; }

    private Node _currentScreen;
    private FlightSceneNode _flightScene;

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Build screen confirmed a design: create the run and start segment 1.</summary>
    public void OnDesignConfirmed(AssembledRocket rocket)
    {
        // TODO(C2): CurrentRun = new MissionRun(ResearchCampaignEngine.Instance.CurrentMission, rocket);
        //           instance FlightScene, connect Director/Cruise signals, Director.StartSegment(CurrentRun).
    }

    /// <summary>Flight or cruise finished a segment successfully: advance and start the next one.</summary>
    public void OnSegmentCompleted(MissionRun run)
    {
        // TODO(C6): run.Advance(); EarthAscent -> Cruise.StartCruise(run); Cruise -> Director.StartSegment(run).
    }

    /// <summary>The run ended (success or failure): process RP and show the debrief.</summary>
    public void OnMissionEnded(FlightResult result)
    {
        // TODO(C5): reward = ResearchCampaignEngine.Instance.ProcessMissionEnd(result); close flight scene;
        //           show DebriefScreen.Show(result, reward).
    }

    /// <summary>Switch to the research screen.</summary>
    public void GoToResearch()
    {
        // TODO(C5): instance ResearchScreen, connect BuildRequested -> GoToBuild.
    }

    /// <summary>Switch to the build screen.</summary>
    public void GoToBuild()
    {
        // TODO(C5): instance BuildScreen, connect Building.DesignConfirmed and ResearchRequested.
    }

    /// <summary>Switch to the victory screen.</summary>
    public void GoToVictory()
    {
        // TODO(C6): instance VictoryScene.
    }

    private void SetState(GameState state)
    {
        State = state;
        if (DebugLog) GD.Print($"[Control] GameState -> {state}");
        EmitSignal(SignalName.StateChanged, (int)state);
    }

    /// <summary>Replaces the current screen with a new instance; call connect() before it enters the tree.</summary>
    private T SwitchScreen<T>(PackedScene scene, System.Action<T> connect) where T : Node
    {
        if (scene == null)
        {
            GD.PushError("[Control] GameFlowManager: missing scene export");
            return null;
        }

        var root = scene.Instantiate<T>();
        connect?.Invoke(root);
        GetTree().Root.AddChild(root);
        GetTree().CurrentScene = root;
        _currentScreen?.QueueFree();
        _currentScreen = root;
        return root;
    }
}
