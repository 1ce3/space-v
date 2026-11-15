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

    [ExportGroup("Story")]
    /// <summary>One-segment ascent mission flown in the tutorial (satellite launch).</summary>
    [Export] public MissionDefinition TutorialMission { get; set; }
    [Export] public int TutorialRewardRP { get; set; } = 50;
    /// <summary>Optional animation scenes from the art team; empty = text cards from StoryScript.</summary>
    [Export] public PackedScene SatelliteDeployScene { get; set; }
    [Export] public PackedScene DiscoveryScene { get; set; }
    [Export] public PackedScene MarsEndingScene { get; set; }
    [Export] public PackedScene CreditsScene { get; set; }

    [ExportGroup("Debug")]
    [Export] public bool DebugLog { get; set; }

    /// <summary>Current game state.</summary>
    public GameState State { get; private set; } = GameState.Build;
    /// <summary>The run in progress, or null outside Flight/Cruise/Debrief.</summary>
    public MissionRun CurrentRun { get; private set; }

    private Node _currentScreen;
    /// <summary>The screen now shown (build, research, flight, home, cutscene...).</summary>
    public Node CurrentScreen => _currentScreen;
    public bool IsTutorial { get; private set; }
    private TutorialOverlay _tutorial;
    private TutorialOverlay _researchGuide;

    private void CloseResearchGuide()
    {
        StoryProgress.ResearchGuideSeen = true;
        StoryProgress.Save();
        _researchGuide?.QueueFree();
        _researchGuide = null;
    }
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
        StoryProgress.Load();
        GameSettings.Load();
        GameSettings.Apply();
        AddChild(new PauseMenu());
        Callable.From(StartIfMainScene).CallDeferred();
    }

    /// <summary>When the game starts from main.tscn, replace it with the build screen.</summary>
    private void StartIfMainScene()
    {
        Node current = GetTree().CurrentScene;
        if (current == null || current.SceneFilePath != MainScenePath) return;
        _currentScreen = current;
        GoToHome();
    }

    /// <summary>Build screen confirmed a design: create the run and start segment 1.</summary>
    public void OnDesignConfirmed(AssembledRocket rocket)
    {
        MissionDefinition mission = IsTutorial && TutorialMission != null ? TutorialMission : ResearchCampaignEngine.Instance?.CurrentMission;
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
        if (IsTutorial)
        {
            Callable.From(() => OnTutorialFlightEnded(result)).CallDeferred();
            return;
        }
        LastReward = ResearchCampaignEngine.Instance?.ProcessMissionEnd(result);
        bool finale = result.Outcome == MissionOutcome.Success && (ResearchCampaignEngine.Instance?.IsCampaignComplete() ?? true);
        if (finale)
        {
            Callable.From(PlayEnding).CallDeferred();
            return;
        }
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
        if (LastResult != null)
            StoryProgress.SetLastLog(debrief.TitleLabel?.Text, debrief.SummaryLabel?.Text, debrief.RewardLabel?.Text, (int)LastResult.Outcome);
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
        var research = SwitchScreen<ResearchScreenRoot>(ResearchScreen, r =>
        {
            r.BuildRequested += OnBuildRequested;
            r.HomeRequested += OnHubRequested;
        });
        if (research == null)
        {
            GoToBuild();
            return;
        }
        SetState(GameState.Research);
        if (!StoryProgress.ResearchGuideSeen && _researchGuide == null)
        {
            _researchGuide = new TutorialOverlay { ResearchOnly = true };
            _researchGuide.Finished += CloseResearchGuide;
            _researchGuide.SkipRequested += CloseResearchGuide;
            AddChild(_researchGuide);
        }
    }

    private void OnBuildRequested()
    {
        _researchGuide?.QueueFree();
        _researchGuide = null;
        Callable.From(GoToBuild).CallDeferred();
    }

    /// <summary>Switch to the build screen.</summary>
    public void GoToBuild()
    {
        var build = SwitchScreen<BuildScreenRoot>(BuildScreen, b =>
        {
            if (b.Building != null) b.Building.DesignConfirmed += OnDesignConfirmed;
            else GD.PushError("[Control] GameFlowManager: BuildScreenRoot.Building is not set");
            b.ResearchRequested += OnResearchRequested;
            b.HomeRequested += OnHubRequested;
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

    // ------------------------------------------------------------------ home, tutorial, cutscenes

    public void GoToHome()
    {
        string win = StoryProgress.ShowWinOnHome ? WinText(LastResult) : null;
        StoryProgress.ShowWinOnHome = false;
        var home = new HomeScreen { WinSummary = win };
        home.PlayRequested += () => Callable.From(() =>
        {
            if (StoryProgress.TutorialDone) GoToHub();
            else StartTutorial();
        }).CallDeferred();
        home.NewGameRequested += () => Callable.From(StartNewGame).CallDeferred();
        home.TutorialRequested += () => Callable.From(StartTutorial).CallDeferred();
        home.CreditsRequested += () => Callable.From(() => PlayCutscenes(new[] { CreditsStep() }, GoToHome)).CallDeferred();
        home.QuitRequested += OnQuitRequested;
        ShowNode(home);
        SetState(GameState.Home);
    }

    /// <summary>Home hub: last flight log, research, rocket design (Continue lands here).</summary>
    public void GoToHub()
    {
        _researchGuide?.QueueFree();
        _researchGuide = null;
        var hub = new HubScreen();
        hub.ResearchRequested += () => Callable.From(GoToResearch).CallDeferred();
        hub.BuildRequested += () => Callable.From(GoToBuild).CallDeferred();
        hub.MenuRequested += () => Callable.From(GoToHome).CallDeferred();
        ShowNode(hub);
        SetState(GameState.Home);
    }

    private void OnHubRequested() => Callable.From(GoToHub).CallDeferred();

    /// <summary>New game from the menu: wipes research, story flags, the last log and the saved design, then the tutorial.</summary>
    public void StartNewGame()
    {
        ResearchCampaignEngine.Instance?.ResetSave();
        StoryProgress.Reset();
        string design = ProjectSettings.GlobalizePath(RocketDesign.SavePath);
        if (System.IO.File.Exists(design)) System.IO.File.Delete(design);
        StartTutorial();
    }

    public void StartTutorial()
    {
        IsTutorial = true;
        StoryProgress.ResearchGuideSeen = false; // replaying the tutorial shows the research guide again
        if (_tutorial == null)
        {
            _tutorial = new TutorialOverlay();
            if (TutorialMission?.Segments.Count > 0) _tutorial.TargetAltitudeM = TutorialMission.Segments[0].TargetAltitudeM;
            _tutorial.SkipRequested += () => Callable.From(EndTutorial).CallDeferred();
            AddChild(_tutorial);
        }
        GoToBuild();
    }

    /// <summary>Tutorial finished or skipped: campaign mission from now on.</summary>
    private void EndTutorial()
    {
        IsTutorial = false;
        _tutorial?.QueueFree();
        _tutorial = null;
        StoryProgress.TutorialDone = true;
        StoryProgress.Save();
    }

    private void OnTutorialFlightEnded(FlightResult result)
    {
        CurrentRun = null;
        _flightScene = null;
        if (result.Outcome != MissionOutcome.Success)
        {
            _tutorial?.OnRetry(result.FailureCause.ToString());
            GoToBuild();
            return;
        }
        EndTutorial();
        ResearchCampaignEngine.Instance?.AddRP(TutorialRewardRP);
        PlayCutscenes(new[]
        {
            (SatelliteDeployScene, (System.Collections.Generic.IReadOnlyList<StoryScript.Card>)StoryScript.SatelliteDeploy, (string)null),
            (DiscoveryScene, StoryScript.Discovery, null),
        }, GoToResearch);
    }

    private void PlayEnding()
    {
        _flightScene = null;
        CurrentRun = null;
        PlayCutscenes(new[] { (MarsEndingScene, (System.Collections.Generic.IReadOnlyList<StoryScript.Card>)StoryScript.MarsEnding, (string)null), CreditsStep() }, () =>
        {
            StoryProgress.EndingSeen = true;
            StoryProgress.ShowWinOnHome = true;
            StoryProgress.Save();
            GoToHome();
        });
    }

    private (PackedScene, System.Collections.Generic.IReadOnlyList<StoryScript.Card>, string) CreditsStep() =>
        (CreditsScene, null, StoryScript.Credits);

    /// <summary>Plays the cutscenes one after another, then calls done.</summary>
    public void PlayCutscenes((PackedScene Scene, System.Collections.Generic.IReadOnlyList<StoryScript.Card> Cards, string Credits)[] list,
        System.Action done, int index = 0)
    {
        if (index >= list.Length)
        {
            done?.Invoke();
            return;
        }
        var c = list[index];
        var player = new CutscenePlayer { AnimationScene = c.Scene, Cards = c.Scene == null ? c.Cards : null, CreditsText = c.Scene == null ? c.Credits : null };
        player.Finished += () => Callable.From(() => PlayCutscenes(list, done, index + 1)).CallDeferred();
        ShowNode(player);
        SetState(GameState.Cutscene);
    }

    private static string WinText(FlightResult r)
    {
        if (r == null) return "The robot has landed on Mars!";
        int s = Mathf.Max(0, Mathf.RoundToInt(r.FlightTime));
        return "The robot has landed on Mars and sent data back to Earth!\n\n" +
               $"Landing speed     {r.LandingSpeedMps:0.0} m/s\n" +
               $"Flight time       {s / 60}:{s % 60:00}\n" +
               $"Cruise days       {r.CruiseDaysSurvived:0}\n" +
               $"Max altitude      {r.MaxAltitude / 1000f:0.0} km";
    }

    /// <summary>Shows a screen created in code (no PackedScene).</summary>
    private void ShowNode(Node root)
    {
        GetTree().Root.AddChild(root);
        GetTree().CurrentScene = root;
        _currentScreen?.QueueFree();
        _currentScreen = root;
    }

    private void SetState(GameState state)
    {
        State = state;
        if (state is not (GameState.Flight or GameState.Cruise)) GetTree().Paused = false;
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
