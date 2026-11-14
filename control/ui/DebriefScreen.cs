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

    [ExportGroup("Colors")]
    [Export] public Color SuccessColor { get; set; } = new(0.35f, 0.85f, 0.5f);
    [Export] public Color FailureColor { get; set; } = new(0.95f, 0.45f, 0.35f);
    [Export] public Color AbortColor { get; set; } = new(0.95f, 0.75f, 0.3f);

    /// <summary>Number of segments of the mission, for "x / N" (set by GameFlowManager; 3 by default).</summary>
    public int TotalSegments { get; set; } = 3;

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, SummaryLabel, nameof(SummaryLabel))) return;
        if (ContinueButton != null) ContinueButton.Pressed += OnContinuePressed;
    }

    public override void _ExitTree()
    {
        if (ContinueButton != null) ContinueButton.Pressed -= OnContinuePressed;
    }

    /// <summary>Fills the screen. reward may be null when no campaign runs (sandbox).</summary>
    public void Show(FlightResult result, RewardBreakdown reward)
    {
        if (result == null) return;

        if (TitleLabel != null)
        {
            (string title, Color color) = result.Outcome switch
            {
                MissionOutcome.Success => ("Mission success", SuccessColor),
                MissionOutcome.Aborted => ("Flight aborted", AbortColor),
                _ => ("Flight failed", FailureColor),
            };
            TitleLabel.Text = title;
            TitleLabel.AddThemeColorOverride("font_color", color);
        }

        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"Segments completed: {result.SegmentsCompleted} / {TotalSegments}");
        if (result.FailureCause != FailureCause.None)
        {
            summary.Append($"Cause: {CauseText(result.FailureCause)}");
            if (!string.IsNullOrEmpty(result.FailedModelId)) summary.Append($" ({result.FailedModelId})");
            summary.AppendLine();
        }
        summary.AppendLine();
        summary.AppendLine($"Max altitude      {result.MaxAltitude / 1000f:0.0} km");
        summary.AppendLine($"Max speed         {result.MaxSpeed:0} m/s");
        summary.AppendLine($"Max Q             {result.MaxQ / 1000f:0.0} kPa");
        summary.AppendLine($"Max G             {result.MaxG:0.0}");
        summary.AppendLine($"Flight time       {FormatTime(result.FlightTime)}");
        if (result.CruiseDaysSurvived > 0f) summary.AppendLine($"Cruise days       {result.CruiseDaysSurvived:0}");
        if (result.Outcome == MissionOutcome.Success) summary.AppendLine($"Touchdown speed   {result.LandingSpeedMps:0.0} m/s");
        SummaryLabel.Text = summary.ToString().TrimEnd();

        if (RewardLabel != null)
        {
            if (reward == null)
            {
                RewardLabel.Text = "No research campaign (test run).";
            }
            else
            {
                int total = ResearchCampaignEngine.Instance?.CurrentRP ?? reward.Total;
                RewardLabel.Text =
                    $"Research points earned\n" +
                    $"  Segments           +{reward.SegmentRP}\n" +
                    $"  Records            +{reward.MilestoneRP}\n" +
                    $"  New lessons        +{reward.NoveltyRP}\n" +
                    $"  Total              +{reward.Total} RP   (you have {total} RP)";
            }
        }
        ContinueButton?.GrabFocus();
    }

    /// <summary>Vietnamese text of a failure cause.</summary>
    public static string CauseText(FailureCause cause) => cause switch
    {
        FailureCause.Shear => "broke under shear",
        FailureCause.Bending => "broke under bending",
        FailureCause.JointOverload => "joint overload",
        FailureCause.Thermal => "overheated",
        FailureCause.Collision => "collision",
        FailureCause.TargetNotReached => "orbit not reached",
        FailureCause.InsufficientDeltaV => "not enough delta-v",
        FailureCause.TippedOver => "tipped over on landing",
        FailureCause.Aborted => "aborted by the player",
        _ => "unknown",
    };

    private static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{s / 60}:{s % 60:00}";
    }

    private void OnContinuePressed() => EmitSignal(SignalName.ContinueRequested);
}
