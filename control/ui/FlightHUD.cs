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
        if (abortHoldProgress > 0f) text.AppendLine($"Giữ Backspace để hủy chuyến bay... {abortHoldProgress * 100f:0} %");
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
        if (SasLabel == null) return;
        var allowed = new System.Text.StringBuilder();
        SasMode[] modes = { SasMode.StabilityAssist, SasMode.HoldPitch, SasMode.Prograde, SasMode.Retrograde };
        for (int i = 0; i < modes.Length; i++)
            if (enabledModes.Has(modes[i])) allowed.Append(allowed.Length > 0 ? " " : "").Append(i + 1);
        string list = allowed.Length > 0 ? allowed.ToString() : "không";
        SasLabel.Text = $"SAS [T]: {SasModeName(mode)}   chế độ dùng được [1-4]: {list}";
    }

    /// <summary>Short Vietnamese name of a SAS mode.</summary>
    public static string SasModeName(SasMode mode) => mode switch
    {
        SasMode.StabilityAssist => "Giữ ổn định",
        SasMode.HoldPitch => "Giữ góc",
        SasMode.Prograde => "Theo hướng bay",
        SasMode.Retrograde => "Ngược hướng bay",
        _ => "Tắt",
    };
}
