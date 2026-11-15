// AI-assisted: written with Claude (Anthropic); reviewed by The Neptunians.
using Godot;

namespace RocketGame.Control;

/// <summary>
/// Direction indicator (UI redesign): a round dial with the rocket's nose direction (pitch) and a prograde
/// marker (flight path angle). Angles in degrees from the horizon, 90 = straight up, right = east.
/// </summary>
public partial class AttitudeDial : Godot.Control
{
    [Export] public Color RingColor { get; set; } = new(1f, 1f, 1f, 0.35f);
    [Export] public Color BackColor { get; set; } = new(0.06f, 0.08f, 0.12f, 0.8f);
    [Export] public Color NoseColor { get; set; } = new(1f, 0.85f, 0.35f);
    [Export] public Color ProgradeColor { get; set; } = new(0.4f, 0.9f, 0.55f);

    private float _pitch = 90f;
    private float _prograde = 90f;
    private bool _hasPrograde;

    public AttitudeDial()
    {
        CustomMinimumSize = new Vector2(112, 112);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>pitch: nose angle; prograde: velocity angle, null when the rocket is (almost) still.</summary>
    public void SetAngles(float pitchDeg, float? progradeDeg)
    {
        _pitch = pitchDeg;
        _hasPrograde = progradeDeg.HasValue;
        if (progradeDeg.HasValue) _prograde = progradeDeg.Value;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 c = Size / 2f;
        float r = Mathf.Min(Size.X, Size.Y) / 2f - 4f;
        DrawCircle(c, r, BackColor);
        DrawArc(c, r, 0f, Mathf.Tau, 48, RingColor, 2f);
        // Horizon and vertical guides.
        DrawLine(c + new Vector2(-r, 0), c + new Vector2(r, 0), new Color(RingColor, 0.2f), 1f);
        DrawLine(c + new Vector2(0, -r), c + new Vector2(0, r), new Color(RingColor, 0.2f), 1f);
        // Ticks every 30 degrees.
        for (int d = 0; d < 360; d += 30)
        {
            Vector2 dir = Dir(d);
            DrawLine(c + dir * (r - 6f), c + dir * r, RingColor, 1.5f);
        }

        // Prograde marker: small ring with three stubs.
        if (_hasPrograde)
        {
            Vector2 p = c + Dir(_prograde) * (r - 14f);
            DrawArc(p, 5f, 0f, Mathf.Tau, 16, ProgradeColor, 2f);
            DrawLine(p + new Vector2(-5, 0), p + new Vector2(-9, 0), ProgradeColor, 2f);
            DrawLine(p + new Vector2(5, 0), p + new Vector2(9, 0), ProgradeColor, 2f);
            DrawLine(p + new Vector2(0, -5), p + new Vector2(0, -9), ProgradeColor, 2f);
        }

        // Nose arrow.
        Vector2 nose = Dir(_pitch);
        Vector2 side = new(-nose.Y, nose.X);
        Vector2 tip = c + nose * (r - 8f);
        DrawLine(c - nose * (r * 0.45f), tip, NoseColor, 3f);
        DrawColoredPolygon(new[] { tip, tip - nose * 12f + side * 6f, tip - nose * 12f - side * 6f }, NoseColor);

        Font font = GetThemeDefaultFont();
        string text = $"{_pitch:0}°";
        Vector2 size = font.GetStringSize(text, HorizontalAlignment.Left, -1, 13);
        DrawString(font, c + new Vector2(-size.X / 2f, r * 0.62f), text, HorizontalAlignment.Left, -1, 13, Colors.White);
    }

    /// <summary>Screen direction of an angle from the horizon (y down).</summary>
    private static Vector2 Dir(float deg)
    {
        float rad = Mathf.DegToRad(deg);
        return new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad));
    }
}
