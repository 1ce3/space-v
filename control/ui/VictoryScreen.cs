using Godot;
using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>Root of scenes/victory.tscn: shown after a successful landing on Mars.</summary>
public partial class VictoryScreen : Godot.Control
{
    [Signal] public delegate void ContinueRequestedEventHandler();
    [Signal] public delegate void QuitRequestedEventHandler();

    [ExportGroup("Widgets")]
    [Export] public Label SummaryLabel { get; set; }
    [Export] public Button ContinueButton { get; set; }
    [Export] public Button QuitButton { get; set; }

    public override void _Ready()
    {
        if (ContinueButton != null) ContinueButton.Pressed += OnContinuePressed;
        if (QuitButton != null) QuitButton.Pressed += OnQuitPressed;
    }

    public override void _ExitTree()
    {
        if (ContinueButton != null) ContinueButton.Pressed -= OnContinuePressed;
        if (QuitButton != null) QuitButton.Pressed -= OnQuitPressed;
    }

    /// <summary>Fills the screen with the winning run.</summary>
    public void Show(FlightResult result)
    {
        if (SummaryLabel == null || result == null) return;
        int s = Mathf.Max(0, Mathf.RoundToInt(result.FlightTime));
        SummaryLabel.Text =
            "The robot has landed on Mars!\n\n" +
            $"Landing speed     {result.LandingSpeedMps:0.0} m/s\n" +
            $"Flight time       {s / 60}:{s % 60:00}\n" +
            $"Cruise days       {result.CruiseDaysSurvived:0}\n" +
            $"Max altitude      {result.MaxAltitude / 1000f:0.0} km";
        ContinueButton?.GrabFocus();
    }

    private void OnContinuePressed() => EmitSignal(SignalName.ContinueRequested);
    private void OnQuitPressed() => EmitSignal(SignalName.QuitRequested);
}
