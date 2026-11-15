// AI-assisted: written with Claude (Anthropic); reviewed by The Neptunians.
using Godot;

namespace RocketGame.Control;

/// <summary>
/// Shared look of the menu, hub and HUD widgets built in code: dark translucent panels over the 3D scene,
/// rounded corners, one accent color. Static helpers only (no state).
/// </summary>
public static class UiStyle
{
    public static readonly Color PanelColor = new(0.06f, 0.08f, 0.12f, 0.78f);
    public static readonly Color PanelBorder = new(1f, 1f, 1f, 0.08f);
    public static readonly Color Accent = new(0.28f, 0.55f, 0.96f);
    public static readonly Color AccentHover = new(0.36f, 0.62f, 1f);
    public static readonly Color ButtonColor = new(0.14f, 0.17f, 0.23f, 0.92f);
    public static readonly Color ButtonHover = new(0.2f, 0.24f, 0.32f, 0.95f);
    public static readonly Color Text = new(0.92f, 0.94f, 0.96f);
    public static readonly Color Muted = new(0.62f, 0.66f, 0.73f);
    public static readonly Color Good = new(0.35f, 0.85f, 0.5f);
    public static readonly Color Bad = new(0.95f, 0.45f, 0.35f);
    public static readonly Color Warn = new(0.95f, 0.75f, 0.3f);

    /// <summary>Flat rounded box.</summary>
    public static StyleBoxFlat Box(Color color, int radius = 14, int padding = 16, Color? border = null)
    {
        var box = new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = padding, ContentMarginRight = padding,
            ContentMarginTop = padding, ContentMarginBottom = padding,
        };
        if (border is Color b)
        {
            box.BorderColor = b;
            box.BorderWidthLeft = box.BorderWidthRight = box.BorderWidthTop = box.BorderWidthBottom = 1;
        }
        return box;
    }

    /// <summary>Translucent panel used for every card.</summary>
    public static PanelContainer Panel(int padding = 18)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Box(PanelColor, 16, padding, PanelBorder));
        return panel;
    }

    /// <summary>Styles a button; accent = the main action of the screen.</summary>
    public static Button StyleButton(Button button, bool accent = false, int fontSize = 18, int height = 44)
    {
        Color normal = accent ? Accent : ButtonColor;
        Color hover = accent ? AccentHover : ButtonHover;
        button.AddThemeStyleboxOverride("normal", Box(normal, 10, 12));
        button.AddThemeStyleboxOverride("hover", Box(hover, 10, 12));
        button.AddThemeStyleboxOverride("pressed", Box(hover.Darkened(0.2f), 10, 12));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("disabled", Box(new Color(normal, 0.4f), 10, 12));
        button.AddThemeColorOverride("font_color", Text);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", new Color(Text, 0.4f));
        button.AddThemeFontSizeOverride("font_size", fontSize);
        if (height > 0) button.CustomMinimumSize = new Vector2(button.CustomMinimumSize.X, height);
        return button;
    }

    /// <summary>New styled button wired to an action.</summary>
    public static Button MakeButton(string text, System.Action onPressed, bool accent = false, int fontSize = 18, int height = 44)
    {
        var button = StyleButton(new Button { Text = text, FocusMode = Godot.Control.FocusModeEnum.None }, accent, fontSize, height);
        if (onPressed != null) button.Pressed += onPressed;
        return button;
    }

    /// <summary>Label with size and color.</summary>
    public static Label MakeLabel(string text, int fontSize = 16, Color? color = null, bool wrap = false)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color ?? Text);
        if (wrap)
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill;
        }
        return label;
    }

    /// <summary>Adds child and stretches it over the whole parent.</summary>
    public static T AddFull<T>(Godot.Control parent, T child) where T : Godot.Control
    {
        parent.AddChild(child);
        child.SetAnchorsAndOffsetsPreset(Godot.Control.LayoutPreset.FullRect);
        return child;
    }
}
