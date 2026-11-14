using Godot;
using RocketGame.Core;
using RocketGame.Physics;

namespace RocketGame.Control;

/// <summary>Cruise overlay: day bar, delta-v, power, battery, temperature, burn/pause/speed buttons.</summary>
public partial class CruiseHUD : CanvasLayer
{
    [Signal] public delegate void TransferBurnRequestedEventHandler();
    [Signal] public delegate void PauseToggledEventHandler(bool paused);
    [Signal] public delegate void TimeScaleRequestedEventHandler(float scale);

    [ExportGroup("Widgets")]
    [Export] public ProgressBar DayBar { get; set; }
    [Export] public Label DayLabel { get; set; }
    [Export] public Label DeltaVLabel { get; set; }
    [Export] public Label ResourcesLabel { get; set; }
    [Export] public Label AnomalyLabel { get; set; }
    [Export] public Button BurnButton { get; set; }
    [Export] public Button PauseButton { get; set; }
    [Export] public Button SpeedButton { get; set; }

    [ExportGroup("Speeds")]
    [Export] public float FastTimeScale { get; set; } = 4f;

    public override void _Ready()
    {
        NodeGuard.Require(this, DayLabel, nameof(DayLabel));
    }

    /// <summary>Day counter and progress bar.</summary>
    public void ShowProgress(int day, int totalDays)
    {
        // TODO(C6)
    }

    /// <summary>Delta-v left versus delta-v needed for the transfer burn (m/s).</summary>
    public void ShowDeltaV(float remainingMps, float requiredMps)
    {
        // TODO(C6)
    }

    /// <summary>Power balance, battery and lowest temperature from one simulated day.</summary>
    public void ShowResources(CruiseStepResult step)
    {
        // TODO(C6)
    }

    /// <summary>Shows a cruise anomaly.</summary>
    public void ShowCruiseAnomaly(ActiveAnomaly anomaly)
    {
        // TODO(C7)
    }
}
