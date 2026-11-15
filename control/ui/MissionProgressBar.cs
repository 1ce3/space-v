// AI-assisted: written with Claude (Anthropic); reviewed by The Neptunians.
using Godot;

namespace RocketGame.Control;

/// <summary>
/// Top-of-screen bar for the whole mission (UI redesign): Ascent, Cruise and Descent sections with icons at their
/// starts and a marker at the current progress. Drawn by hand; FlightHUD feeds it with SetProgress.
/// </summary>
public partial class MissionProgressBar : Godot.Control
{
    [Export] public Color TrackColor { get; set; } = new(1f, 1f, 1f, 0.16f);
    [Export] public Color FillColor { get; set; } = new(0.36f, 0.62f, 1f);
    [Export] public Color DoneColor { get; set; } = new(0.35f, 0.85f, 0.5f);
    [Export] public Color TextColor { get; set; } = new(0.92f, 0.94f, 0.96f);

    /// <summary>Section names, one per mission segment.</summary>
    public string[] Sections { get; set; } = { "Ascent", "Cruise", "Descent" };

    private int _segment;
    private float _fraction;

    public MissionProgressBar()
    {
        CustomMinimumSize = new Vector2(360, 58);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>segment: index of the segment flown now; fraction: 0..1 inside it.</summary>
    public void SetProgress(int segment, float fraction)
    {
        fraction = Mathf.Clamp(fraction, 0f, 1f);
        if (segment == _segment && Mathf.Abs(fraction - _fraction) < 0.002f) return;
        _segment = segment;
        _fraction = fraction;
        QueueRedraw();
    }

    public override void _Draw()
    {
        int n = Mathf.Max(1, Sections.Length);
        Font font = GetThemeDefaultFont();
        const int fontSize = 13;
        float left = 14f, right = Size.X - 14f, y = 34f, h = 8f;
        float width = right - left;
        float overall = Mathf.Clamp((_segment + _fraction) / n, 0f, 1f);

        // Track and fill.
        DrawRect(new Rect2(left, y - h / 2f, width, h), TrackColor);
        DrawRect(new Rect2(left, y - h / 2f, width * overall, h), FillColor);

        for (int i = 0; i < n; i++)
        {
            float x0 = left + width * i / n;
            float x1 = left + width * (i + 1) / n;
            bool done = i < _segment;
            bool now = i == _segment;
            // Section start tick and icon.
            DrawLine(new Vector2(x0, y - 9f), new Vector2(x0, y + 9f), done || now ? FillColor : TrackColor, 2f);
            DrawIcon(i, new Vector2(x0, y - 22f), done ? DoneColor : now ? FillColor : new Color(TextColor, 0.45f));
            // Section name under the bar.
            string label = Sections[i] + (done ? "  ✓" : "");
            Vector2 size = font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
            float cx = (x0 + x1) / 2f - size.X / 2f;
            DrawString(font, new Vector2(cx, y + 22f), label, HorizontalAlignment.Left, -1, fontSize,
                now ? TextColor : new Color(TextColor, done ? 0.75f : 0.45f));
        }
        DrawLine(new Vector2(right, y - 9f), new Vector2(right, y + 9f), overall >= 1f ? DoneColor : TrackColor, 2f);

        // Current marker.
        float mx = left + width * overall;
        DrawColoredPolygon(new[] { new Vector2(mx - 7f, y - 13f), new Vector2(mx + 7f, y - 13f), new Vector2(mx, y - 4f) }, Colors.White);
    }

    /// <summary>Small drawn icons: rocket (ascent), spacecraft (cruise), parachute (descent).</summary>
    private void DrawIcon(int index, Vector2 c, Color color)
    {
        switch (index)
        {
            case 0: // rocket
                DrawColoredPolygon(new[] { c + new Vector2(0, -9), c + new Vector2(4, -3), c + new Vector2(4, 6), c + new Vector2(-4, 6), c + new Vector2(-4, -3) }, color);
                DrawColoredPolygon(new[] { c + new Vector2(-4, 2), c + new Vector2(-8, 8), c + new Vector2(-4, 6) }, color);
                DrawColoredPolygon(new[] { c + new Vector2(4, 2), c + new Vector2(8, 8), c + new Vector2(4, 6) }, color);
                break;
            case 1: // spacecraft with solar panels
                DrawRect(new Rect2(c + new Vector2(-3, -5), new Vector2(6, 10)), color);
                DrawRect(new Rect2(c + new Vector2(-12, -2), new Vector2(7, 4)), color);
                DrawRect(new Rect2(c + new Vector2(5, -2), new Vector2(7, 4)), color);
                break;
            default: // parachute
                DrawArc(c + new Vector2(0, -2), 8f, Mathf.Pi, Mathf.Tau, 12, color, 2.5f);
                DrawLine(c + new Vector2(-8, -2), c + new Vector2(0, 8), color, 1.2f);
                DrawLine(c + new Vector2(8, -2), c + new Vector2(0, 8), color, 1.2f);
                DrawRect(new Rect2(c + new Vector2(-2, 7), new Vector2(4, 4)), color);
                break;
        }
    }
}
