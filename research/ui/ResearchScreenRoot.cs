using Godot;

namespace RocketGame.Research;

/// <summary>Root of scenes/research_screen.tscn. Emits BuildRequested; never calls Control (decision #22). Full UI is task B4.</summary>
[GlobalClass]
public partial class ResearchScreenRoot : Godot.Control
{
    /// <summary>The player wants to go back to the build screen.</summary>
    [Signal] public delegate void BuildRequestedEventHandler();

    /// <summary>"Vào xưởng" button.</summary>
    [Export] public Button BuildButton { get; set; }

    public override void _Ready()
    {
        if (BuildButton != null) BuildButton.Pressed += OnBuildButtonPressed;
        else GD.PushWarning("[Research] ResearchScreenRoot.BuildButton is not set.");
    }

    public override void _ExitTree()
    {
        if (BuildButton != null) BuildButton.Pressed -= OnBuildButtonPressed;
    }

    private void OnBuildButtonPressed() => EmitSignal(SignalName.BuildRequested);
}
