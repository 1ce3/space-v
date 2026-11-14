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

    public override void _Ready()
    {
        NodeGuard.Require(this, TelemetryLabel, nameof(TelemetryLabel));
    }

    /// <summary>Shows the latest telemetry; glitched garbles the numbers (lightning).</summary>
    public void ShowTelemetry(TelemetryFrame frame, bool glitched)
    {
        // TODO(C3): text version.
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
        // TODO(C3)
    }

    /// <summary>Shows the SAS mode and which modes the sensors allow.</summary>
    public void ShowSasState(SasMode mode, SasModeMask enabledModes)
    {
        // TODO(C7)
    }
}
