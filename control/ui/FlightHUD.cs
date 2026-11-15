using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Control;

/// <summary>Flight HUD. Read-only view: never changes game state (C3 text version, C7 full version).</summary>
public partial class FlightHUD : CanvasLayer
{
    [ExportGroup("Widgets")]
    [Export] public Label TelemetryLabel { get; set; }
    [Export] public Label StageLabel { get; set; }
    [Export] public Label SasLabel { get; set; }
    [Export] public Label WarningLabel { get; set; }
    [Export] public Label AnomalyLabel { get; set; }

    /// <summary>How long a warning stays on screen (s).</summary>
    [ExportGroup("Timing")]
    [Export] public float WarningDurationSec { get; set; } = 3f;

    private float _warningTimer;

    // UI redesign: mission progress bar, help menu, tool icons and the direction indicator, built in code.
    private MissionProgressBar _progress;
    private AttitudeDial _dial;
    private Godot.Control _helpPanel;
    private readonly Dictionary<string, (PanelContainer Panel, Label Value)> _tools = new();
    private TelemetryFrame _lastFrame;
    private bool _hasFrame;
    private SasMode _sasMode;

    public override void _Ready()
    {
        NodeGuard.Require(this, TelemetryLabel, nameof(TelemetryLabel));
        BuildRedesign();
    }

    private void BuildRedesign()
    {
        // Info block keeps its text, now on a translucent panel.
        if (TelemetryLabel != null) TelemetryLabel.AddThemeStyleboxOverride("normal", UiStyle.Box(UiStyle.PanelColor, 10, 10));
        if (StageLabel != null) StageLabel.AddThemeStyleboxOverride("normal", UiStyle.Box(UiStyle.PanelColor, 10, 10));
        // Warnings move under the progress bar.
        foreach (Label l in new[] { WarningLabel, AnomalyLabel })
        {
            if (l == null) continue;
            l.OffsetTop += 70;
            l.OffsetBottom += 70;
        }
        // The SAS line is replaced by the SAS tool icon.
        if (SasLabel != null) SasLabel.Visible = false;

        var root = new Godot.Control { MouseFilter = Godot.Control.MouseFilterEnum.Ignore };
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Godot.Control.LayoutPreset.FullRect);

        // Flight progress bar, top center.
        _progress = new MissionProgressBar();
        root.AddChild(_progress);
        _progress.SetAnchorsAndOffsetsPreset(Godot.Control.LayoutPreset.CenterTop);
        _progress.OffsetLeft = -146; // between the info panel (left) and the stage list (right)
        _progress.OffsetRight = 249;
        _progress.OffsetTop = 6;
        _progress.OffsetBottom = 64;

        // Help button (right edge, under the stage list) and the keybind panel.
        Button help = UiStyle.MakeButton("?", ToggleHelp, false, 20, 40);
        help.TooltipText = "Keys (F1)";
        root.AddChild(help);
        help.SetAnchorsAndOffsetsPreset(Godot.Control.LayoutPreset.TopRight);
        help.OffsetLeft = -56;
        help.OffsetRight = -16;
        help.OffsetTop = 132;
        help.OffsetBottom = 172;
        _helpPanel = BuildHelpPanel();
        root.AddChild(_helpPanel);
        _helpPanel.SetAnchorsAndOffsetsPreset(Godot.Control.LayoutPreset.TopRight);
        _helpPanel.OffsetLeft = -560;
        _helpPanel.OffsetRight = -64;
        _helpPanel.OffsetTop = 132;
        _helpPanel.Visible = false;

        // Bottom row: tool icons around the direction indicator.
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Godot.Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        root.AddChild(row);
        row.SetAnchorsAndOffsetsPreset(Godot.Control.LayoutPreset.CenterBottom);
        row.OffsetLeft = -380;
        row.OffsetRight = 380;
        row.OffsetTop = -124;
        row.OffsetBottom = -10;
        AddTool(row, "SAS", "T");
        AddTool(row, "RCS", "R");
        AddTool(row, "Throttle", "Shift/Ctrl");
        _dial = new AttitudeDial();
        row.AddChild(_dial);
        AddTool(row, "Stage", "Space");
        AddTool(row, "Chute", "P");
        AddTool(row, "Panels", "G");
    }

    private void AddTool(HBoxContainer row, string name, string key)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(92, 64), SizeFlagsVertical = Godot.Control.SizeFlags.ShrinkCenter };
        panel.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.PanelColor, 10, 8, UiStyle.PanelBorder));
        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 0);
        var title = UiStyle.MakeLabel($"{name}  [{key}]", 11, UiStyle.Muted);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        var value = UiStyle.MakeLabel("-", 16);
        value.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(title);
        v.AddChild(value);
        panel.AddChild(v);
        row.AddChild(panel);
        _tools[name] = (panel, value);
    }

    private void SetTool(string name, string text, bool active)
    {
        if (!_tools.TryGetValue(name, out var tool)) return;
        tool.Value.Text = text;
        tool.Value.AddThemeColorOverride("font_color", active ? UiStyle.Good : UiStyle.Text);
        tool.Panel.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.PanelColor, 10, 8, active ? new Color(UiStyle.Good, 0.7f) : UiStyle.PanelBorder));
    }

    // ------------------------------------------------------------------ help menu

    /// <summary>(action, what it does) shown in the help menu; the key comes from the InputMap.</summary>
    private static readonly (string Action, string Text)[] HelpRows =
    {
        ("flight_pitch_left", "Pitch left"), ("flight_pitch_right", "Pitch right"),
        ("flight_throttle_up", "Throttle up"), ("flight_throttle_down", "Throttle down"),
        ("flight_throttle_full", "Full throttle"), ("flight_throttle_cut", "Cut throttle"),
        ("flight_stage", "Next stage"), ("flight_fairing", "Jettison fairing"), ("flight_chute", "Open parachute"),
        ("flight_sas_toggle", "SAS on / off"), ("flight_sas_mode_1", "SAS: stability"), ("flight_sas_mode_2", "SAS: hold pitch"),
        ("flight_sas_mode_3", "SAS: prograde"), ("flight_sas_mode_4", "SAS: retrograde"),
        ("flight_rcs_toggle", "RCS on / off"), ("flight_rcs_left", "RCS left"), ("flight_rcs_right", "RCS right"),
        ("flight_deploy_toggle", "Solar panels / radiators"), ("flight_sce_to_aux", "Switch to AUX power"),
        ("flight_seal_leak", "Seal a leak"), ("ui_cancel", "Pause menu"),
        ("flight_zoom_in", "Zoom in"), ("flight_zoom_out", "Zoom out"),
    };

    private Godot.Control BuildHelpPanel()
    {
        PanelContainer panel = UiStyle.Panel(14);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 2);
        panel.AddChild(v);
        v.AddChild(UiStyle.MakeLabel("Keybind : Function", 16));
        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 16);
        v.AddChild(grid);
        foreach ((string action, string text) in HelpRows)
        {
            if (!InputMap.HasAction(action)) continue;
            grid.AddChild(UiStyle.MakeLabel(KeyText(action), 13, UiStyle.Warn));
            grid.AddChild(UiStyle.MakeLabel(text, 13));
        }
        v.AddChild(UiStyle.MakeLabel("F1 or ? closes this list", 12, UiStyle.Muted));
        return panel;
    }

    /// <summary>Readable key(s) of an input action.</summary>
    private static string KeyText(string action)
    {
        var keys = new List<string>();
        foreach (InputEvent e in InputMap.ActionGetEvents(action))
        {
            switch (e)
            {
                case InputEventKey k:
                    keys.Add(OS.GetKeycodeString(k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode));
                    break;
                case InputEventMouseButton m:
                    keys.Add(m.ButtonIndex switch { MouseButton.WheelUp => "Wheel up", MouseButton.WheelDown => "Wheel down", _ => $"Mouse {m.ButtonIndex}" });
                    break;
            }
        }
        return keys.Count > 0 ? string.Join(" / ", keys) : "-";
    }

    private void ToggleHelp()
    {
        if (_helpPanel != null) _helpPanel.Visible = !_helpPanel.Visible;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F1 } && Visible)
        {
            ToggleHelp();
            GetViewport().SetInputAsHandled();
        }
    }

    // ------------------------------------------------------------------ progress, dial, tools

    private void UpdateRedesign()
    {
        MissionRun run = GameFlowManager.Instance?.CurrentRun;
        if (_progress != null)
        {
            _progress.Visible = run?.Mission != null;
            if (run?.Mission != null) _progress.SetProgress(run.SegmentIndex, SegmentFraction(run));
        }
        if (!_hasFrame) return;
        TelemetryFrame f = _lastFrame;
        _dial?.SetAngles(f.PitchDeg, f.SpeedMps > 1f ? f.FlightPathAngleDeg : null);

        bool sasOn = _sasMode != SasMode.Off;
        SetTool("SAS", sasOn ? SasShortName(_sasMode) : "Off", sasOn);
        SetTool("Throttle", $"{f.Throttle * 100f:0} %", f.Throttle > 0.01f);
        int stages = run?.Stages?.Count ?? 0;
        SetTool("Stage", stages > 0 ? $"{Mathf.Min(f.CurrentStageIndex, stages)} / {stages}" : $"{f.CurrentStageIndex}", false);

        PartTree tree = run?.PartTree;
        bool rcsOn = false, hasRcs = false, panels = false, hasPanels = false;
        string chute = "None";
        bool chuteOpen = false;
        if (tree != null)
        {
            foreach (CommandPodPart pod in tree.GetParts<CommandPodPart>())
            {
                if (!pod.IsAttached) continue;
                foreach (RcsThrusterModule rcs in pod.GetModules<RcsThrusterModule>()) { hasRcs = true; rcsOn |= rcs.IsEnabled; }
                foreach (SolarArrayModule solar in pod.GetModules<SolarArrayModule>()) { hasPanels = true; panels |= solar.IsDeployed; }
            }
            ParachutePart p = tree.GetParts<ParachutePart>().Find(c => c.IsAttached);
            if (p != null)
            {
                chuteOpen = p.ChuteState == ParachuteState.Deployed;
                chute = p.State == ComponentState.Broken ? "Torn" : p.ChuteState.ToString();
            }
        }
        SetTool("RCS", hasRcs ? (rcsOn ? "On" : "Off") : "None", rcsOn);
        SetTool("Chute", chute, chuteOpen);
        SetTool("Panels", hasPanels ? (panels ? "Open" : "Closed") : "None", panels);
    }

    /// <summary>0..1 progress inside the current segment.</summary>
    private float SegmentFraction(MissionRun run)
    {
        MissionSegmentDefinition seg = run.CurrentSegment;
        if (seg == null) return 1f;
        TelemetryFrame f = _lastFrame;
        switch (seg.Kind)
        {
            case SegmentKind.EarthAscent:
                if (!_hasFrame) return 0f;
                float alt = seg.TargetAltitudeM > 0f ? f.AltitudeM / seg.TargetAltitudeM : 0f;
                float spd = seg.TargetSpeedMps > 0f ? f.HorizontalSpeedMps / seg.TargetSpeedMps : 0f;
                return 0.5f * Mathf.Clamp(alt, 0f, 1f) + 0.5f * Mathf.Clamp(spd, 0f, 1f);
            case SegmentKind.Cruise:
                return seg.DurationDays > 0f ? run.ElapsedDays / seg.DurationDays : 0f;
            default:
                if (!_hasFrame || seg.EntryAltitudeM <= 0f) return 0f;
                return 1f - Mathf.Clamp(f.AltitudeM / seg.EntryAltitudeM, 0f, 1f);
        }
    }

    private static string SasShortName(SasMode mode) => mode switch
    {
        SasMode.StabilityAssist => "Stable",
        SasMode.HoldPitch => "Hold",
        SasMode.Prograde => "Prograde",
        SasMode.Retrograde => "Retro",
        _ => "Off",
    };

    /// <summary>Shows the latest telemetry; glitched garbles the numbers (lightning).</summary>
    public void ShowTelemetry(TelemetryFrame frame, bool glitched)
    {
        _lastFrame = frame;
        _hasFrame = true;
        if (TelemetryLabel == null) return;
        string n(float v, string fmt) => glitched ? "###" : v.ToString(fmt);
        TelemetryLabel.Text =
            $"{frame.Phase}\n" +
            $"Altitude {n(frame.AltitudeM / 1000f, "0.00")} km   (radar {n(frame.RadarAltitudeM, "0")} m)\n" +
            $"Speed    {n(frame.SpeedMps, "0")} m/s   vert {n(frame.VerticalSpeedMps, "0")}   horiz {n(frame.HorizontalSpeedMps, "0")}\n" +
            $"Pitch    {n(frame.PitchDeg, "0.0")}°   rate {n(frame.AngularRateDegPerSec, "0.0")}°/s\n" +
            $"Throttle {n(frame.Throttle * 100f, "0")} %   fuel {n(frame.StageFuelFraction * 100f, "0")} %\n" +
            $"q        {n(frame.DynamicPressure / 1000f, "0.0")} kPa   Mach {n(frame.MachNumber, "0.00")}   G {n(frame.GForce, "0.0")}\n" +
            $"Heat     {n(frame.MaxTempRatio * 100f, "0")} %   load {n(frame.MaxStressRatio * 100f, "0")} %";
    }

    /// <summary>Shows a one-line stage summary (C3 text version).</summary>
    public void ShowStageText(string text)
    {
        if (StageLabel != null) StageLabel.Text = text;
    }

    public override void _Process(double delta)
    {
        UpdateRedesign();
        if (_warningTimer <= 0f) return;
        _warningTimer -= (float)delta;
        if (_warningTimer <= 0f && WarningLabel != null) WarningLabel.Text = "";
    }

    /// <summary>Shows the stage list and the next stage.</summary>
    public void ShowStages(IReadOnlyList<StageGroup> stages, int currentIndex)
    {
        // TODO(C7)
    }

    /// <summary>Shows an anomaly with its key hint and timer.</summary>
    public void ShowAnomaly(ActiveAnomaly anomaly)
    {
        if (AnomalyLabel != null) AnomalyLabel.Text = anomaly != null ? AnomalyResponseHandler.Describe(anomaly) : "";
    }

    /// <summary>Shows every waiting anomaly, one per line (red when it needs a key), plus the abort-hold hint.</summary>
    public void ShowAnomalies(IReadOnlyList<ActiveAnomaly> anomalies, float abortHoldProgress)
    {
        if (AnomalyLabel == null) return;
        var text = new System.Text.StringBuilder();
        bool needsKey = false;
        foreach (ActiveAnomaly a in anomalies ?? System.Array.Empty<ActiveAnomaly>())
        {
            text.AppendLine(AnomalyResponseHandler.Describe(a));
            needsKey |= !AnomalyResponseHandler.IsWarningOnly(a.Type);
        }
        AnomalyLabel.Text = text.ToString().TrimEnd();
        AnomalyLabel.AddThemeColorOverride("font_color", needsKey || abortHoldProgress > 0f ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.8f, 0.3f));
    }

    /// <summary>Shows a short warning (staging, parachute...).</summary>
    public void ShowWarning(string message)
    {
        if (WarningLabel == null) return;
        WarningLabel.Text = message;
        _warningTimer = WarningDurationSec;
    }

    /// <summary>Shows the SAS mode and which modes the sensors allow.</summary>
    public void ShowSasState(SasMode mode, SasModeMask enabledModes)
    {
        _sasMode = mode;
        if (SasLabel == null) return;
        var allowed = new System.Text.StringBuilder();
        SasMode[] modes = { SasMode.StabilityAssist, SasMode.HoldPitch, SasMode.Prograde, SasMode.Retrograde };
        for (int i = 0; i < modes.Length; i++)
            if (enabledModes.Has(modes[i])) allowed.Append(allowed.Length > 0 ? " " : "").Append(i + 1);
        string list = allowed.Length > 0 ? allowed.ToString() : "none";
        SasLabel.Text = $"SAS [T]: {SasModeName(mode)}   available modes [1-4]: {list}";
    }

    /// <summary>Short Vietnamese name of a SAS mode.</summary>
    public static string SasModeName(SasMode mode) => mode switch
    {
        SasMode.StabilityAssist => "Stability",
        SasMode.HoldPitch => "Hold pitch",
        SasMode.Prograde => "Prograde",
        SasMode.Retrograde => "Retrograde",
        _ => "Off",
    };
}
