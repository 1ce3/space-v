using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Research;
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

    /// <summary>Scene that starts the real game loop (opens the build screen). Other scenes (sandboxes) are left alone.</summary>
    [ExportGroup("Startup")]
    [Export(PropertyHint.File, "*.tscn")] public string MainScenePath { get; set; } = "res://scenes/main.tscn";

    /// <summary>Result of the last finished run (debrief and victory).</summary>
    public FlightResult LastResult { get; private set; }
    /// <summary>RP breakdown of the last finished run, or null.</summary>
    public RewardBreakdown LastReward { get; private set; }

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public override void _Ready()
    {
        Callable.From(StartIfMainScene).CallDeferred();
    }

    /// <summary>When the game starts from main.tscn, replace it with the build screen.</summary>
    private void StartIfMainScene()
    {
        Node current = GetTree().CurrentScene;
        if (current == null || current.SceneFilePath != MainScenePath) return;
        _currentScreen = current;
        GoToBuild();
    }

    /// <summary>Build screen confirmed a design: create the run and start segment 1.</summary>
    public void OnDesignConfirmed(AssembledRocket rocket)
    {
        MissionDefinition mission = ResearchCampaignEngine.Instance?.CurrentMission;
        if (mission == null) GD.PushWarning("[Control] GameFlowManager: no current mission, flying without one");
        var run = new MissionRun(mission, rocket);
        // Deferred: the build screen is still emitting; switch scenes after it returns.
        Callable.From(() => StartRun(run)).CallDeferred();
    }

    /// <summary>Opens the flight scene and starts the run's current segment. Used by the game and by sandboxes.</summary>
    public FlightSceneNode StartRun(MissionRun run)
    {
        CurrentRun = run;
        _flightScene = SwitchScreen<FlightSceneNode>(FlightScene, ConnectFlightScene);
        if (_flightScene == null) return null;
        SetState(GameState.Flight);
        _flightScene.Director.StartSegment(run);
        return _flightScene;
    }

    /// <summary>Connects the segment signals of a flight scene (before it enters the tree).</summary>
    public void ConnectFlightScene(FlightSceneNode scene)
    {
        if (scene?.Director != null)
        {
            scene.Director.SegmentCompleted += OnSegmentCompleted;
            scene.Director.MissionEnded += OnMissionEnded;
        }
        if (scene?.Cruise != null)
        {
            scene.Cruise.SegmentCompleted += OnSegmentCompleted;
            scene.Cruise.MissionEnded += OnMissionEnded;
        }
    }

    /// <summary>Flight or cruise finished a segment successfully: advance and start the next one.</summary>
    public void OnSegmentCompleted(MissionRun run)
    {
        // Signals come from inside the physics step: change state after it.
        Callable.From(() => AdvanceRun(run)).CallDeferred();
    }

    private void AdvanceRun(MissionRun run)
    {
        if (run == null || run != CurrentRun || _flightScene == null || !IsInstanceValid(_flightScene)) return;
        run.Advance();
        if (DebugLog) GD.Print($"[Control] Run advanced to segment {run.SegmentIndex} ({run.CurrentSegment?.SegmentId ?? "end"})");

        if (run.IsFinished || run.CurrentSegment == null)
        {
            // Defensive: the last segment ends through MissionEnded, not SegmentCompleted.
            OnMissionEnded(run.BlackBox.BuildResult(MissionOutcome.Success));
            return;
        }
        if (run.CurrentKind == SegmentKind.Cruise)
        {
            SetState(GameState.Cruise);
            _flightScene.Cruise.StartCruise(run);
        }
        else
        {
            SetState(GameState.Flight);
            _flightScene.Director.StartSegment(run);
        }
    }

    /// <summary>Debug (sandboxes): skips the run forward to a later segment on the vehicle already built.</summary>
    public void DebugJumpToSegment(int segmentIndex)
    {
        MissionRun run = CurrentRun;
        if (run == null || _flightScene == null) return;
        while (run.SegmentIndex < segmentIndex && !run.IsFinished) run.Advance();
        if (run.CurrentSegment == null) return;
        GD.Print($"[Control] Debug jump to segment {run.SegmentIndex} ({run.CurrentSegment.SegmentId})");
        if (run.CurrentKind == SegmentKind.Cruise)
        {
            SetState(GameState.Cruise);
            _flightScene.Cruise.StartCruise(run);
        }
        else
        {
            SetState(GameState.Flight);
            _flightScene.Director.StartSegment(run);
        }
    }

    /// <summary>The run ended (success or failure): process RP and show the debrief.</summary>
    public void OnMissionEnded(FlightResult result)
    {
        if (result == null) return;
        LastResult = result;
        LastReward = ResearchCampaignEngine.Instance?.ProcessMissionEnd(result);
        Callable.From(ShowDebrief).CallDeferred();
    }

    private void ShowDebrief()
    {
        _flightScene = null;
        var debrief = SwitchScreen<DebriefScreen>(DebriefScene, d => d.ContinueRequested += OnDebriefContinue);
        if (debrief == null)
        {
            OnDebriefContinue();
            return;
        }
        debrief.TotalSegments = CurrentRun?.Mission?.Segments.Count ?? 3;
        debrief.Show(LastResult, LastReward);
        SetState(GameState.Debrief);
    }

    private void OnDebriefContinue()
    {
        CurrentRun = null;
        bool won = ResearchCampaignEngine.Instance?.IsCampaignComplete() ?? LastResult?.Outcome == MissionOutcome.Success;
        if (won && LastResult?.Outcome == MissionOutcome.Success) GoToVictory();
        else GoToResearch();
    }

    /// <summary>Switch to the research screen.</summary>
    public void GoToResearch()
    {
        var research = SwitchScreen<ResearchScreenRoot>(ResearchScreen, r => r.BuildRequested += OnBuildRequested);
        if (research == null)
        {
            GoToBuild();
            return;
        }
        SetState(GameState.Research);
    }

    private void OnBuildRequested() => Callable.From(GoToBuild).CallDeferred();

    /// <summary>Switch to the build screen.</summary>
    public void GoToBuild()
    {
        var build = SwitchScreen<BuildScreenRoot>(BuildScreen, b =>
        {
            if (b.Building != null) b.Building.DesignConfirmed += OnDesignConfirmed;
            else GD.PushError("[Control] GameFlowManager: BuildScreenRoot.Building is not set");
            b.ResearchRequested += OnResearchRequested;
        });
        if (build != null) SetState(GameState.Build);
    }

    private void OnResearchRequested() => Callable.From(GoToResearch).CallDeferred();

    /// <summary>Switch to the victory screen.</summary>
    public void GoToVictory()
    {
        var victory = SwitchScreen<VictoryScreen>(VictoryScene, v =>
        {
            v.ContinueRequested += OnVictoryContinue;
            v.QuitRequested += OnQuitRequested;
        });
        if (victory == null)
        {
            GoToResearch();
            return;
        }
        victory.Show(LastResult);
        SetState(GameState.Victory);
    }

    private void OnVictoryContinue() => Callable.From(GoToResearch).CallDeferred();
    private void OnQuitRequested() => GetTree().Quit();

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
