using Godot;

namespace RocketGame.Control;

/// <summary>Home / title screen built in code. Shows the win panel once after the ending.</summary>
public partial class HomeScreen : Godot.Control
{
    [Signal] public delegate void PlayRequestedEventHandler();
    [Signal] public delegate void TutorialRequestedEventHandler();
    [Signal] public delegate void CreditsRequestedEventHandler();
    [Signal] public delegate void QuitRequestedEventHandler();

    /// <summary>Text of the win panel; null = no panel.</summary>
    public string WinSummary { get; set; }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddFull(this, new ColorRect { Color = new Color(0.03f, 0.04f, 0.08f) });
        var center = AddFull(this, new CenterContainer());
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        box.AddThemeConstantOverride("separation", 14);
        center.AddChild(box);

        var title = new Label { Text = StoryScript.CompanyName, HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 56);
        box.AddChild(title);
        box.AddChild(new Label { Text = "Từ Trái Đất tới sao Hỏa", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.6f) });
        box.AddChild(new Godot.Control { CustomMinimumSize = new Vector2(0, 20) });
        AddButton(box, StoryProgress.TutorialDone ? "Tiếp tục" : "Bắt đầu", SignalName.PlayRequested);
        if (StoryProgress.TutorialDone) AddButton(box, "Chơi lại hướng dẫn", SignalName.TutorialRequested);
        AddButton(box, "Credits", SignalName.CreditsRequested);
        AddButton(box, "Thoát", SignalName.QuitRequested);

        if (WinSummary != null) ShowWinPanel();
    }

    /// <summary>Adds child and stretches it over the whole parent.</summary>
    public static T AddFull<T>(Godot.Control parent, T child) where T : Godot.Control
    {
        parent.AddChild(child);
        child.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return child;
    }

    private void AddButton(VBoxContainer box, string text, StringName signal)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 48) };
        b.AddThemeFontSizeOverride("font_size", 20);
        b.Pressed += () => EmitSignal(signal);
        box.AddChild(b);
    }

    private void ShowWinPanel()
    {
        var dim = AddFull(this, new ColorRect { Color = new Color(0, 0, 0, 0.6f) });
        var center = AddFull(this, new CenterContainer());
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(520, 0) };
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 12);
        var t = new Label { Text = "CHIẾN THẮNG!", HorizontalAlignment = HorizontalAlignment.Center };
        t.AddThemeFontSizeOverride("font_size", 40);
        v.AddChild(t);
        v.AddChild(new Label { Text = WinSummary, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(480, 0) });
        var ok = new Button { Text = "Đóng", CustomMinimumSize = new Vector2(0, 44) };
        ok.Pressed += () => { dim.QueueFree(); center.QueueFree(); };
        v.AddChild(ok);
        panel.AddChild(v);
        center.AddChild(panel);
    }
}
