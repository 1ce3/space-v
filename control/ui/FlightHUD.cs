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
            $"Độ cao   {n(frame.AltitudeM / 1000f, "0.00")} km   (radar {n(frame.RadarAltitudeM, "0")} m)\n" +
            $"Tốc độ   {n(frame.SpeedMps, "0")} m/s   đứng {n(frame.VerticalSpeedMps, "0")}   ngang {n(frame.HorizontalSpeedMps, "0")}\n" +
            $"Góc      {n(frame.PitchDeg, "0.0")}°   quay {n(frame.AngularRateDegPerSec, "0.0")}°/s\n" +
            $"Ga       {n(frame.Throttle * 100f, "0")} %   nhiên liệu {n(frame.StageFuelFraction * 100f, "0")} %\n" +
            $"q        {n(frame.DynamicPressure / 1000f, "0.0")} kPa   Mach {n(frame.MachNumber, "0.00")}   G {n(frame.GForce, "0.0")}\n" +
            $"Nhiệt    {n(frame.MaxTempRatio * 100f, "0")} %   tải {n(frame.MaxStressRatio * 100f, "0")} %";
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
        // TODO(C7)
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
        // TODO(C7)
    }
}
