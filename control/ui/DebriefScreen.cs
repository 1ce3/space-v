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
                MissionOutcome.Success => ("Nhiệm vụ thành công", SuccessColor),
                MissionOutcome.Aborted => ("Đã huỷ chuyến bay", AbortColor),
                _ => ("Chuyến bay thất bại", FailureColor),
            };
            TitleLabel.Text = title;
            TitleLabel.AddThemeColorOverride("font_color", color);
        }

        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"Chặng hoàn thành: {result.SegmentsCompleted} / {TotalSegments}");
        if (result.FailureCause != FailureCause.None)
        {
            summary.Append($"Nguyên nhân: {CauseText(result.FailureCause)}");
            if (!string.IsNullOrEmpty(result.FailedModelId)) summary.Append($" ({result.FailedModelId})");
            summary.AppendLine();
        }
        summary.AppendLine();
        summary.AppendLine($"Độ cao lớn nhất   {result.MaxAltitude / 1000f:0.0} km");
        summary.AppendLine($"Tốc độ lớn nhất   {result.MaxSpeed:0} m/s");
        summary.AppendLine($"Max Q             {result.MaxQ / 1000f:0.0} kPa");
        summary.AppendLine($"G lớn nhất        {result.MaxG:0.0}");
        summary.AppendLine($"Thời gian bay     {FormatTime(result.FlightTime)}");
        if (result.CruiseDaysSurvived > 0f) summary.AppendLine($"Ngày hành trình   {result.CruiseDaysSurvived:0}");
        if (result.Outcome == MissionOutcome.Success) summary.AppendLine($"Tốc độ chạm đất   {result.LandingSpeedMps:0.0} m/s");
        SummaryLabel.Text = summary.ToString().TrimEnd();

        if (RewardLabel != null)
        {
            if (reward == null)
            {
                RewardLabel.Text = "Không có chiến dịch nghiên cứu (chạy thử).";
            }
            else
            {
                int total = ResearchCampaignEngine.Instance?.CurrentRP ?? reward.Total;
                RewardLabel.Text =
                    $"Điểm nghiên cứu nhận được\n" +
                    $"  Chặng hoàn thành   +{reward.SegmentRP}\n" +
                    $"  Kỷ lục             +{reward.MilestoneRP}\n" +
                    $"  Bài học mới        +{reward.NoveltyRP}\n" +
                    $"  Tổng               +{reward.Total} RP   (hiện có {total} RP)";
            }
        }
        ContinueButton?.GrabFocus();
    }

    /// <summary>Vietnamese text of a failure cause.</summary>
    public static string CauseText(FailureCause cause) => cause switch
    {
        FailureCause.Shear => "gãy do lực cắt",
        FailureCause.Bending => "gãy do uốn",
        FailureCause.JointOverload => "khớp nối quá tải",
        FailureCause.Thermal => "quá nhiệt",
        FailureCause.Collision => "va chạm",
        FailureCause.TargetNotReached => "không đạt quỹ đạo",
        FailureCause.InsufficientDeltaV => "không đủ delta-v",
        FailureCause.TippedOver => "lật khi đáp",
        FailureCause.Aborted => "người chơi huỷ",
        _ => "không rõ",
    };

    private static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{s / 60}:{s % 60:00}";
    }

    private void OnContinuePressed() => EmitSignal(SignalName.ContinueRequested);
}
