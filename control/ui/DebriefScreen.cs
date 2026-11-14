using Godot;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Control;

/// <summary>Root of scenes/debrief.tscn: shows the FlightResult and the RP earned. Emits, never calls Control.</summary>
public partial class DebriefScreen : Godot.Control
{
    [Signal] public delegate void ContinueRequestedEventHandler();

    [ExportGroup("Widgets")]
    [Export] public Label TitleLabel { get; set; }
    [Export] public Label SummaryLabel { get; set; }
    [Export] public Label RewardLabel { get; set; }
    [Export] public Button ContinueButton { get; set; }

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, SummaryLabel, nameof(SummaryLabel))) return;
        if (ContinueButton != null) ContinueButton.Pressed += OnContinuePressed;
    }

    public override void _ExitTree()
    {
        if (ContinueButton != null) ContinueButton.Pressed -= OnContinuePressed;
    }

    /// <summary>Fills the screen. reward may be null before B4 is merged.</summary>
    public void Show(FlightResult result, RewardBreakdown reward)
    {
        // TODO(C5): outcome, failure cause and part, records, segments, RP lines.
    }

    private void OnContinuePressed() => EmitSignal(SignalName.ContinueRequested);
}
