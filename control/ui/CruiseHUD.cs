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

    [ExportGroup("Colors")]
    [Export] public Color OkColor { get; set; } = new(0.6f, 0.9f, 0.6f);
    [Export] public Color BadColor { get; set; } = new(0.95f, 0.45f, 0.35f);

    private bool _paused;
    private bool _fast;

    public override void _Ready()
    {
        NodeGuard.Require(this, DayLabel, nameof(DayLabel));
        // Buttons never take focus, so Space / Z still reach InputHandler.
        if (BurnButton != null)
        {
            BurnButton.FocusMode = Godot.Control.FocusModeEnum.None;
            BurnButton.Pressed += OnBurnPressed;
        }
        if (PauseButton != null)
        {
            PauseButton.FocusMode = Godot.Control.FocusModeEnum.None;
            PauseButton.Pressed += OnPausePressed;
        }
        if (SpeedButton != null)
        {
            SpeedButton.FocusMode = Godot.Control.FocusModeEnum.None;
            SpeedButton.Pressed += OnSpeedPressed;
        }
    }

    /// <summary>Back to the "before the burn" state.</summary>
    public void ResetControls()
    {
        _paused = false;
        _fast = false;
        SetBurnDone(false);
        if (PauseButton != null) PauseButton.Text = "Pause";
        if (SpeedButton != null) SpeedButton.Text = "×1";
        if (ResourcesLabel != null) ResourcesLabel.Text = "";
        if (AnomalyLabel != null) AnomalyLabel.Text = "";
    }

    /// <summary>Before the burn only the burn button works; after it, pause and speed.</summary>
    public void SetBurnDone(bool burned)
    {
        if (BurnButton != null)
        {
            BurnButton.Disabled = burned;
            BurnButton.Text = burned ? "Burned" : "Transfer burn [Z]";
        }
        if (PauseButton != null) PauseButton.Disabled = !burned;
        if (SpeedButton != null) SpeedButton.Disabled = !burned;
    }

    /// <summary>Day counter and progress bar.</summary>
    public void ShowProgress(int day, int totalDays)
    {
        if (DayLabel != null) DayLabel.Text = $"Journey to Mars: day {day} / {totalDays}";
        if (DayBar != null)
        {
            DayBar.MaxValue = Mathf.Max(totalDays, 1);
            DayBar.Value = day;
        }
    }

    /// <summary>Delta-v left versus delta-v needed for the transfer burn (m/s).</summary>
    public void ShowDeltaV(float remainingMps, float requiredMps)
    {
        if (DeltaVLabel == null) return;
        DeltaVLabel.Text = $"Delta-v left {remainingMps:0} m/s   (need {requiredMps:0} m/s)";
        DeltaVLabel.AddThemeColorOverride("font_color", remainingMps >= requiredMps ? OkColor : BadColor);
    }

    /// <summary>Power balance, battery and lowest temperature from one simulated day.</summary>
    public void ShowResources(CruiseStepResult step)
    {
        if (ResourcesLabel == null) return;
        ResourcesLabel.Text =
            $"Net power {step.PowerBalanceW:+0;-0;0} W   Battery {step.BatteryFraction * 100f:0} %   " +
            $"Lowest temperature {step.LowestTempRatio * 100f:0} % of limit" + (step.IsBrownout ? "   POWER LOST" : "");
        ResourcesLabel.AddThemeColorOverride("font_color", step.IsBrownout ? BadColor : OkColor);
    }

    /// <summary>Shows a cruise anomaly.</summary>
    public void ShowCruiseAnomaly(ActiveAnomaly anomaly)
    {
        // TODO(C7): key hint and timer.
        if (anomaly != null) ShowMessage(anomaly.Type.ToString());
    }

    /// <summary>One-line message (instructions, events).</summary>
    public void ShowMessage(string message)
    {
        if (AnomalyLabel != null) AnomalyLabel.Text = message;
    }

    private void OnBurnPressed() => EmitSignal(SignalName.TransferBurnRequested);

    private void OnPausePressed()
    {
        _paused = !_paused;
        if (PauseButton != null) PauseButton.Text = _paused ? "Resume" : "Pause";
        EmitSignal(SignalName.PauseToggled, _paused);
    }

    private void OnSpeedPressed()
    {
        _fast = !_fast;
        if (SpeedButton != null) SpeedButton.Text = _fast ? $"×{FastTimeScale:0}" : "×1";
        EmitSignal(SignalName.TimeScaleRequested, _fast ? FastTimeScale : 1f);
    }
}
