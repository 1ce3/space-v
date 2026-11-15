using System.Collections.Generic;
using Godot;

namespace RocketGame.Control;

/// <summary>
/// Plays one cutscene, then emits Finished. With an AnimationScene (made by the art team) it plays that scene and ends
/// when its first AnimationPlayer finishes (or the scene emits a "finished" signal, or after MaxSceneSeconds).
/// Without one it shows text cards (StoryScript) with fades. Space / Enter / click: next card. Esc: skip all.
/// Credits mode scrolls a block of text.
/// </summary>
public partial class CutscenePlayer : Godot.Control
{
    [Signal] public delegate void FinishedEventHandler();

    public PackedScene AnimationScene { get; set; }
    public IReadOnlyList<StoryScript.Card> Cards { get; set; }
    public string CreditsText { get; set; }
    public float MaxSceneSeconds { get; set; } = 60f;
    public float FadeSeconds { get; set; } = 0.6f;
    public float CreditsScrollSpeed { get; set; } = 45f;

    private Label _title;
    private Label _body;
    private Label _hint;
    private VBoxContainer _cardBox;
    private Label _credits;
    private int _index = -1;
    private float _cardTime;
    private float _sceneTime;
    private Node _scene;
    private bool _done;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        HomeScreen.AddFull(this, new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore });

        if (AnimationScene != null)
        {
            _scene = AnimationScene.Instantiate();
            AddChild(_scene);
            AnimationPlayer anim = FindAnim(_scene);
            if (anim != null) anim.AnimationFinished += _ => Finish();
            if (_scene.HasSignal("finished")) _scene.Connect("finished", Callable.From(Finish));
        }
        else if (CreditsText != null)
        {
            _credits = new Label { Text = CreditsText, HorizontalAlignment = HorizontalAlignment.Center };
            _credits.AddThemeFontSizeOverride("font_size", 26);
            AddChild(_credits);
            _credits.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
            _credits.Position = new Vector2(0f, GetViewportRect().Size.Y);
        }
        else
        {
            var center = HomeScreen.AddFull(this, new CenterContainer());
            _cardBox = new VBoxContainer { CustomMinimumSize = new Vector2(760, 0) };
            _cardBox.AddThemeConstantOverride("separation", 24);
            _title = new Label { HorizontalAlignment = HorizontalAlignment.Center };
            _title.AddThemeFontSizeOverride("font_size", 40);
            _body = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _body.AddThemeFontSizeOverride("font_size", 24);
            _cardBox.AddChild(_title);
            _cardBox.AddChild(_body);
            center.AddChild(_cardBox);
            NextCard();
        }
        _hint = new Label { Text = "Space: tiếp   Esc: bỏ qua", Modulate = new Color(1f, 1f, 1f, 0.45f) };
        AddChild(_hint);
        _hint.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight, LayoutPresetMode.Minsize, 16);
    }

    public override void _Process(double delta)
    {
        if (_done) return;
        float dt = (float)delta;
        if (_scene != null)
        {
            _sceneTime += dt;
            if (_sceneTime >= MaxSceneSeconds) Finish();
            return;
        }
        if (_credits != null)
        {
            _credits.Position -= new Vector2(0f, CreditsScrollSpeed * dt);
            if (_credits.Position.Y + _credits.Size.Y < 0f) Finish();
            return;
        }
        if (Cards == null || _index >= Cards.Count) return;
        _cardTime += dt;
        float len = Cards[_index].Seconds;
        float a = Mathf.Min(Mathf.Clamp(_cardTime / FadeSeconds, 0f, 1f), Mathf.Clamp((len - _cardTime) / FadeSeconds, 0f, 1f));
        _cardBox.Modulate = new Color(1f, 1f, 1f, a);
        if (_cardTime >= len) NextCard();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_done) return;
        bool skipAll = e is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Escape };
        bool next = e is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Space or Key.Enter or Key.KpEnter }
                    || e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left };
        if (skipAll) Finish();
        else if (next)
        {
            if (_cardBox != null) NextCard();
            else if (_credits != null) CreditsScrollSpeed *= 3f;
            else Finish();
        }
        else return;
        GetViewport().SetInputAsHandled();
    }

    private void NextCard()
    {
        _index++;
        _cardTime = 0f;
        if (Cards == null || _index >= Cards.Count)
        {
            Finish();
            return;
        }
        _title.Text = Cards[_index].Title;
        _body.Text = Cards[_index].Body;
    }

    private void Finish()
    {
        if (_done) return;
        _done = true;
        EmitSignal(SignalName.Finished);
    }

    private static AnimationPlayer FindAnim(Node n)
    {
        if (n is AnimationPlayer a) return a;
        foreach (Node c in n.GetChildren())
            if (FindAnim(c) is AnimationPlayer found) return found;
        return null;
    }
}
