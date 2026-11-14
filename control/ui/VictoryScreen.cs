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
        // TODO(C6): landing speed, flight time, cruise days.
    }

    private void OnContinuePressed() => EmitSignal(SignalName.ContinueRequested);
    private void OnQuitPressed() => EmitSignal(SignalName.QuitRequested);
}
