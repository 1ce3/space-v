using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;

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
    private string _targetLine = "";

    public override void _Ready()
    {
        NodeGuard.Require(this, TelemetryLabel, nameof(TelemetryLabel));
    }

    /// <summary>Shows the latest telemetry; glitched garbles the numbers (lightning).</summary>
    public void ShowTelemetry(TelemetryFrame frame, bool glitched)
    {
        if (TelemetryLabel == null) return;
        string n(float v, string fmt) => glitched ? "###" : v.ToString(fmt);
        TelemetryLabel.Text =
            $"{frame.Phase}\n" +
            $"Altitude {n(frame.AltitudeM / 1000f, "0.00")} km   (radar {n(frame.RadarAltitudeM, "0")} m)\n" +
            $"Speed    {n(frame.SpeedMps, "0")} m/s   vertical {n(frame.VerticalSpeedMps, "0")}   horizontal {n(frame.HorizontalSpeedMps, "0")}\n" +
            $"Pitch    {n(frame.PitchDeg, "0.0")}°   rate {n(frame.AngularRateDegPerSec, "0.0")}°/s\n" +
            $"Throttle {n(frame.Throttle * 100f, "0")} %   stage fuel {n(frame.StageFuelFraction * 100f, "0")} %\n" +
            $"q        {n(frame.DynamicPressure / 1000f, "0.0")} kPa   Mach {n(frame.MachNumber, "0.00")}   G {n(frame.GForce, "0.0")}\n" +
            $"Heat     {n(frame.MaxTempRatio * 100f, "0")} %   load {n(frame.MaxStressRatio * 100f, "0")} %" +
            (_targetLine.Length > 0 ? "\n\n" + _targetLine : "");
    }

    /// <summary>Orbit target progress for the Earth ascent (two text bars).</summary>
    public void ShowAscentTarget(float altitudeM, float targetAltitudeM, float horizontalSpeedMps, float targetSpeedMps)
    {
        _targetLine =
            $"ORBIT TARGET\n" +
            $"Altitude   {Bar(altitudeM / targetAltitudeM)} {altitudeM / 1000f:0} / {targetAltitudeM / 1000f:0} km\n" +
            $"Horiz. spd {Bar(horizontalSpeedMps / targetSpeedMps)} {horizontalSpeedMps:0} / {targetSpeedMps:0} m/s";
    }

    /// <summary>Landing goal for the Mars descent.</summary>
    public void ShowLandingTarget(float maxTiltDeg)
    {
        _targetLine = $"LANDING TARGET\nOpen the parachute [P] below Mach 2.5, drop the heat shield [Space],\n" +
                      $"touch down slowly (engine or airbag) with tilt under {maxTiltDeg:0}°";
    }

    /// <summary>Clears the target block.</summary>
    public void ClearTarget() => _targetLine = "";

    private static string Bar(float fraction)
    {
        const int width = 12;
        int filled = Mathf.Clamp(Mathf.RoundToInt(fraction * width), 0, width);
        return "[" + new string('#', filled) + new string('.', width - filled) + "]";
    }

    /// <summary>Shows a one-line stage summary (C3 text version).</summary>
    public void ShowStageText(string text)
    {
        if (StageLabel != null) StageLabel.Text = text;
    }

    public override void _Process(double delta)
    {
        if (_warningTimer <= 0f) return;
        _warningTimer -= (float)delta;
        if (_warningTimer <= 0f && WarningLabel != null) WarningLabel.Text = "";
    }

    /// <summary>Shows the stage list and the next stage.</summary>
    public void ShowStages(IReadOnlyList<StageGroup> stages, int currentIndex)
    {
        if (StageLabel == null) return;
        if (stages == null || stages.Count == 0)
        {
            StageLabel.Text = "No stages";
            return;
        }
        var text = new System.Text.StringBuilder("STAGES [Space]\n");
        for (int i = 0; i < stages.Count; i++)
        {
            StageGroup stage = stages[i];
            string marker = i < currentIndex ? "  done " : i == currentIndex ? "> next " : "       ";
            string action = stage.Joints.Count > 0 ? $"separate {stage.Joints.Count}" : "ignite";
            if (stage.EnginesToArm.Count > 0)
            {
                var engines = new List<string>();
                foreach (EnginePart e in stage.EnginesToArm) engines.Add(e.ModelId);
                action += (stage.Joints.Count > 0 ? ", fire " : " ") + string.Join(", ", engines);
            }
            text.Append($"{marker}{i}: {action}\n");
        }
        if (currentIndex >= stages.Count) text.Append("All stages used");
        StageLabel.Text = text.ToString().TrimEnd('\n');
    }

    /// <summary>Shows an anomaly with its key hint and timer.</summary>
    public void ShowAnomaly(ActiveAnomaly anomaly)
    {
        // TODO(C7)
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
        if (SasLabel == null) return;
        var allowed = new System.Text.StringBuilder();
        SasMode[] modes = { SasMode.StabilityAssist, SasMode.HoldPitch, SasMode.Prograde, SasMode.Retrograde };
        for (int i = 0; i < modes.Length; i++)
            if (enabledModes.Has(modes[i])) allowed.Append(allowed.Length > 0 ? " " : "").Append(i + 1);
        string list = allowed.Length > 0 ? allowed.ToString() : "none";
        SasLabel.Text = $"SAS [T]: {SasModeName(mode)}   modes available [1-4]: {list}";
    }

    /// <summary>Short name of a SAS mode.</summary>
    public static string SasModeName(SasMode mode) => mode switch
    {
        SasMode.StabilityAssist => "Stability assist",
        SasMode.HoldPitch => "Hold pitch",
        SasMode.Prograde => "Prograde",
        SasMode.Retrograde => "Retrograde",
        _ => "Off",
    };
}
