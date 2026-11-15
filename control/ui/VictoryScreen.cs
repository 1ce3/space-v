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
            "Robot đã đáp xuống sao Hỏa!\n\n" +
            $"Tốc độ chạm đất   {result.LandingSpeedMps:0.0} m/s\n" +
            $"Thời gian bay     {s / 60}:{s % 60:00}\n" +
            $"Ngày hành trình   {result.CruiseDaysSurvived:0}\n" +
            $"Độ cao lớn nhất   {result.MaxAltitude / 1000f:0.0} km";
        ContinueButton?.GrabFocus();
    }

    private void OnContinuePressed() => EmitSignal(SignalName.ContinueRequested);
    private void OnQuitPressed() => EmitSignal(SignalName.QuitRequested);
}
