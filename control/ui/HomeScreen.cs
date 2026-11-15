using Godot;

namespace RocketGame.Control;

/// <summary>
/// Main menu, built in code over the 3D launch site (MenuBackground): game name, Continue / New game, Options,
/// Credits, Quit. Continue goes to the home hub (or the tutorial on the first run). Shows the win panel once after the ending.
/// </summary>
public partial class HomeScreen : Godot.Control
{
    /// <summary>Continue (or Start on the first run).</summary>
    [Signal] public delegate void PlayRequestedEventHandler();
    /// <summary>Start over: wipe the research save and the story flags, then the tutorial.</summary>
    [Signal] public delegate void NewGameRequestedEventHandler();
    [Signal] public delegate void TutorialRequestedEventHandler();
    [Signal] public delegate void CreditsRequestedEventHandler();
    [Signal] public delegate void QuitRequestedEventHandler();

    /// <summary>Text of the win panel; null = no panel.</summary>
    public string WinSummary { get; set; }

    private Godot.Control _menu;
    private Godot.Control _options;
    private Godot.Control _confirm;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MenuBackground.AddTo(this);

        // Game name, top center.
        var top = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        AddChild(top);
        top.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        top.OffsetTop = 48;
        top.GrowHorizontal = GrowDirection.Both;
        var title = UiStyle.MakeLabel(StoryScript.CompanyName, 64);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeConstantOverride("outline_size", 8);
        title.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.6f));
        top.AddChild(title);
        var subtitle = UiStyle.MakeLabel("From Earth to Mars", 20, UiStyle.Muted);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        top.AddChild(subtitle);

        // Buttons, left column.
        PanelContainer panel = UiStyle.Panel(22);
        AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterLeft);
        panel.OffsetLeft = 64;
        panel.OffsetRight = 64 + 320;
        panel.GrowVertical = GrowDirection.Both;
        _menu = panel;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        panel.AddChild(box);

        bool started = StoryProgress.TutorialDone;
        box.AddChild(UiStyle.MakeButton(started ? "Continue" : "Start", () => EmitSignal(SignalName.PlayRequested), true, 22, 52));
        if (started) box.AddChild(UiStyle.MakeButton("New game", ShowConfirmNewGame));
        box.AddChild(UiStyle.MakeButton("Options", ShowOptions));
        box.AddChild(UiStyle.MakeButton("Credits", () => EmitSignal(SignalName.CreditsRequested)));
        box.AddChild(UiStyle.MakeButton("Quit", () => EmitSignal(SignalName.QuitRequested)));

        if (WinSummary != null) ShowWinPanel();
    }

    /// <summary>Kept for CutscenePlayer and older callers.</summary>
    public static T AddFull<T>(Godot.Control parent, T child) where T : Godot.Control => UiStyle.AddFull(parent, child);

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel")) return;
        if (_options != null) CloseOverlay(ref _options);
        else if (_confirm != null) CloseOverlay(ref _confirm);
        else return;
        GetViewport().SetInputAsHandled();
    }

    // ------------------------------------------------------------------ options

    private void ShowOptions()
    {
        if (_options != null) return;
        VBoxContainer v = OpenOverlay("Options", out _options);

        v.AddChild(UiStyle.MakeLabel("Master volume", 16, UiStyle.Muted));
        var volume = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = GameSettings.MasterVolume, CustomMinimumSize = new Vector2(360, 28) };
        volume.ValueChanged += value => { GameSettings.MasterVolume = (float)value; GameSettings.Apply(); };
        v.AddChild(volume);

        var fullscreen = new CheckButton { Text = "Fullscreen", ButtonPressed = GameSettings.Fullscreen, FocusMode = FocusModeEnum.None };
        fullscreen.AddThemeFontSizeOverride("font_size", 18);
        fullscreen.Toggled += on => { GameSettings.Fullscreen = on; GameSettings.Apply(); };
        v.AddChild(fullscreen);

        if (StoryProgress.TutorialDone)
            v.AddChild(UiStyle.MakeButton("Replay tutorial", () => EmitSignal(SignalName.TutorialRequested)));

        v.AddChild(UiStyle.MakeButton("Done", () => { GameSettings.Save(); CloseOverlay(ref _options); }, true));
    }

    // ------------------------------------------------------------------ new game

    private void ShowConfirmNewGame()
    {
        if (_confirm != null) return;
        VBoxContainer v = OpenOverlay("Start a new game?", out _confirm);
        v.AddChild(UiStyle.MakeLabel("All research points, unlocked technology and the saved rocket design will be lost. The tutorial plays again.", 16, UiStyle.Muted, true));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        Button cancel = UiStyle.MakeButton("Cancel", () => CloseOverlay(ref _confirm));
        Button yes = UiStyle.MakeButton("New game", () => EmitSignal(SignalName.NewGameRequested), true);
        cancel.SizeFlagsHorizontal = yes.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(cancel);
        row.AddChild(yes);
        v.AddChild(row);
    }

    // ------------------------------------------------------------------ win panel

    private void ShowWinPanel()
    {
        VBoxContainer v = OpenOverlay("VICTORY!", out Godot.Control overlay, 40);
        v.AddChild(UiStyle.MakeLabel(WinSummary, 17, null, true));
        v.AddChild(UiStyle.MakeButton("Close", () => overlay.QueueFree(), true));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Dimmed full-screen layer with a centered panel; returns the panel's content box.</summary>
    private VBoxContainer OpenOverlay(string title, out Godot.Control overlay, int titleSize = 28)
    {
        var layer = UiStyle.AddFull(this, new Godot.Control());
        UiStyle.AddFull(layer, new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f) });
        var center = UiStyle.AddFull(layer, new CenterContainer());
        PanelContainer panel = UiStyle.Panel(26);
        panel.CustomMinimumSize = new Vector2(440, 0);
        center.AddChild(panel);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 14);
        panel.AddChild(v);
        var t = UiStyle.MakeLabel(title, titleSize);
        t.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(t);
        overlay = layer;
        return v;
    }

    private static void CloseOverlay(ref Godot.Control overlay)
    {
        overlay?.QueueFree();
        overlay = null;
    }
}
