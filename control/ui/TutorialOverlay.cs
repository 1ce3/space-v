using System;
using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using FlightSceneNode = RocketGame.Control.FlightScene;

namespace RocketGame.Control;

/// <summary>
/// Step-by-step tutorial panel on top of every screen (lives under GameFlowManager). A step with a condition
/// advances by itself when the player has done it; a step without one has a "Tiếp" button.
/// </summary>
public partial class TutorialOverlay : CanvasLayer
{
    [Signal] public delegate void SkipRequestedEventHandler();

    private record Step(string Text, Func<bool> Done);

    private readonly List<Step> _steps = new();
    private int _index;
    private int _launchStep;
    private Label _text;
    private Button _next;
    private PanelContainer _panel;
    private string _prefix = "";

    public float TargetAltitudeM { get; set; } = 40000f;
    /// <summary>True: only the research screen pages; the last page closes the panel (Finished).</summary>
    public bool ResearchOnly { get; set; }
    [Signal] public delegate void FinishedEventHandler();

    public override void _Ready()
    {
        Layer = 50;
        _panel = new PanelContainer { CustomMinimumSize = new Vector2(440, 0) };
        var style = new StyleBoxFlat { BgColor = new Color(0.06f, 0.09f, 0.16f, 0.93f), BorderColor = new Color(0.3f, 0.6f, 1f) };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(10);
        style.SetContentMarginAll(14);
        _panel.AddThemeStyleboxOverride("panel", style);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        var head = new Label { Text = "TUTORIAL", Modulate = new Color(0.5f, 0.75f, 1f) };
        _text = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(410, 0) };
        _text.AddThemeFontSizeOverride("font_size", 15);
        var row = new HBoxContainer();
        _next = new Button { Text = "Next  ›", FocusMode = Godot.Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(110, 34) };
        _next.Pressed += () => Advance();
        var skip = new Button { Text = "Skip tutorial", Flat = true, FocusMode = Godot.Control.FocusModeEnum.None };
        skip.Pressed += () => EmitSignal(SignalName.SkipRequested);
        row.AddChild(skip);
        row.AddChild(new Godot.Control { SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill });
        row.AddChild(_next);
        v.AddChild(head);
        v.AddChild(_text);
        v.AddChild(row);
        _panel.AddChild(v);
        AddChild(_panel);
        PlacePanel();
        GetViewport().SizeChanged += PlacePanel;
        BuildSteps();
        Show(0);
    }

    private static GameFlowManager Flow => GameFlowManager.Instance;
    private static BuildingSystem Building => (Flow?.CurrentScreen as BuildScreenRoot)?.Building;
    private static FlightSceneNode Flight => Flow?.CurrentScreen as FlightSceneNode;

    private static bool Has<T>() where T : RocketPart
    {
        BuildingSystem b = Building;
        return b?.Grid != null && b.Grid.Parts.Exists(p => p is T);
    }

    private int _flightStep;

    /// <summary>Spaceflight-Simulator-like pages: what is on this screen, rocket parts, build rules; then the launch procedure.</summary>
    private void BuildSteps()
    {
        if (ResearchOnly)
        {
            _flightStep = int.MaxValue;
            _steps.Add(new("RESEARCH SCREEN\nEvery flight, even a failed one, earns Research Points (RP) for altitude, speed and new records. Your RP is shown at the top right.", null));
            _steps.Add(new("TABS\nEngines (split into low / mid / upper stage), Fuel, Materials, Frame & pod, Comms, Sensors...\nEach row is a TIER: higher tiers are stronger but cost more.", null));
            _steps.Add(new("RESEARCH CARDS\n• RP cost and the parts / equipment it unlocks.\n• \"Requires:\" lists nodes to buy first (possibly in another tab).\n• Blue border: can buy · green: owned · grey: not available yet.", null));
            _steps.Add(new("MAX BUILD WEIGHT\nYour assembly budget. Frame nodes raise it so you can build bigger rockets.", null));
            _steps.Add(new("ROAD TO MARS\nYou need: an upper-stage engine + Hydrolox tank (Upper Stage), a heat shield and parachute (Entry, Descent and Landing), and power for 210 days of flight (solar panels). Comms / sensor equipment helps handle failures.\n\nPress \"Go to Workshop\" to build.", null));
            return;
        }
        // ---- workshop ----
        _steps.Add(new(StoryScript.TutorialIntro[0] + "\n\nThis is the WORKSHOP. Parts are on the left, the build grid in the middle and rocket stats on the right.", null));
        _steps.Add(new("PART TYPES\n• Command pod: top of the rocket, holds equipment and the payload.\n• Fuel tanks: Kerolox, Hydrolox, Methalox...\n• Liquid engines / solid boosters (mounted on the sides).\n• Decouplers, fins, fairings, parachutes, heat shields.", null));
        _steps.Add(new("A BASIC ROCKET (top to bottom)\nFairing → Command pod → Fuel tank → Engine.\nMulti-stage rockets: put a decoupler (Tab key) between stages to drop the empty ones.", null));
        _steps.Add(new("BUILD RULES\n• Exactly 1 command pod; every part must connect to it.\n• Engines must touch a tank of the SAME fuel type.\n• Total weight must stay within budget (Weight on the right panel).\n• A high centre of mass and fins at the bottom keep the rocket stable (try the wind tunnel).", null));
        _steps.Add(new("POD INVENTORY (right panel)\nDrag equipment onto the grid: an IMU enables SAS, batteries give power, sensors and antennas help handle failures. The hull material (Material tab) changes strength / mass.", null));
        _steps.Add(new("Try: Command pod Mk1 → Kerolox tank S1 → Engine LV1, and drag IMU G1 into the inventory.\nWhen the Launch tab says the design is valid, press \"Launch\".", () => Flow?.State == Core.GameState.Flight));
        _launchStep = _steps.Count - 1;
        // ---- launch pad ----
        _flightStep = _steps.Count;
        _steps.Add(new("LAUNCH PAD\nStats on the left of the screen:\n• Altitude / Speed (vertical, horizontal)\n• Pitch: where the nose points (90° = straight up)\n• Throttle and fuel left\n• q: dynamic pressure, Mach, G: acceleration\n• Heat / load: over 100% breaks the part.", null));
        _steps.Add(new("LAUNCH STEPS\n1. T: turn on SAS (holds direction).\n2. Z: full throttle (Shift/Ctrl up/down, X cuts the engine).\n3. Space: fire the first stage, liftoff!", () => Flight?.Director != null && Flight.Director.Phase != Core.FlightPhase.PreLaunch));
        _steps.Add(new("IN FLIGHT\n• Past ~3 km: tilt gently with A/D to fly at an angle (gravity turn).\n• Watch max q (Max-Q) and heat: throttle down if too high.\n• Out of fuel: Space to drop the stage.", () => (Flight?.Director?.LastFrame.AltitudeM ?? 0f) > 3000f));
        _steps.Add(new($"Climb to {TargetAltitudeM / 1000f:0} km to put the AURORA telescope into orbit!", null));
    }

    /// <summary>Bottom-left corner, right of the build palette (250 px). Re-run when the text changes size.</summary>
    private void PlacePanel()
    {
        if (_panel == null) return;
        Vector2 view = GetViewport().GetVisibleRect().Size;
        _panel.Size = _panel.GetCombinedMinimumSize();
        _panel.Position = new Vector2(262f, view.Y - _panel.Size.Y - 12f);
    }

    private void Show(int i)
    {
        _index = Mathf.Clamp(i, 0, _steps.Count - 1);
        _text.Text = _prefix + _steps[_index].Text;
        bool last = _index == _steps.Count - 1;
        _next.Visible = _steps[_index].Done == null && (!last || ResearchOnly);
        _next.Text = last && ResearchOnly ? "Xong" : "Next  ›";
        Callable.From(PlacePanel).CallDeferred();
    }

    private void Advance()
    {
        _prefix = "";
        if (_index < _steps.Count - 1) Show(_index + 1);
        else if (ResearchOnly) EmitSignal(SignalName.Finished);
    }

    public override void _Process(double delta)
    {
        if (_index < _flightStep && Flow?.State == Core.GameState.Flight) { _prefix = ""; Show(_flightStep); return; }
        Func<bool> done = _steps.Count > 0 ? _steps[_index].Done : null;
        if (done != null && done()) Advance();
    }

    /// <summary>The tutorial flight failed: back to the launch step with a hint.</summary>
    public void OnRetry(string why)
    {
        _prefix = $"Not there yet ({why}). Adjust the rocket and try again!\n\n";
        Show(_launchStep);
    }
}
