// AI-assisted: written with Claude (Anthropic); reviewed by The Neptunians.
using Godot;
using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>
/// Esc during a flight (ascent, cruise, EDL) pauses the game and shows Resume / Main menu / Quit flight.
/// Quit flight aborts the run (debrief shows "Aborted"); Main menu leaves the run without a debrief.
/// Created by GameFlowManager; runs while the tree is paused.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    private Godot.Control _root;
    private Godot.Control _confirm;

    public bool IsOpen => _root?.Visible ?? false;

    public override void _Ready()
    {
        Layer = 90;
        ProcessMode = ProcessModeEnum.Always;
        _root = new Godot.Control { Visible = false };
        _root.SetAnchorsAndOffsetsPreset(Godot.Control.LayoutPreset.FullRect);
        AddChild(_root);
        UiStyle.AddFull(_root, new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f) });
        var center = UiStyle.AddFull(_root, new CenterContainer());

        PanelContainer panel = UiStyle.Panel(26);
        panel.CustomMinimumSize = new Vector2(320, 0);
        center.AddChild(panel);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        panel.AddChild(v);
        var title = UiStyle.MakeLabel("Paused", 28);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(title);
        v.AddChild(UiStyle.MakeButton("Resume", Close, true));
        v.AddChild(UiStyle.MakeButton("Main menu", () => ShowConfirm(
            "Leave to the main menu? This flight will be lost.", GoToMenu)));
        v.AddChild(UiStyle.MakeButton("Quit flight", () => ShowConfirm(
            "Abort this flight? You will see the flight debrief.", QuitFlight)));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel") || @event.IsEcho()) return;
        if (_confirm != null) HideConfirm();
        else if (IsOpen) Close();
        else if (CanPause()) Open();
        else return;
        GetViewport().SetInputAsHandled();
    }

    private static bool CanPause()
    {
        GameState state = GameFlowManager.Instance?.State ?? GameState.Home;
        return state is GameState.Flight or GameState.Cruise;
    }

    public void Open()
    {
        _root.Visible = true;
        GetTree().Paused = true;
    }

    public void Close()
    {
        HideConfirm();
        _root.Visible = false;
        GetTree().Paused = false;
    }

    private void GoToMenu()
    {
        Close();
        Callable.From(() => GameFlowManager.Instance?.GoToHome()).CallDeferred();
    }

    private void QuitFlight()
    {
        Close();
        InputHandler.RequestAbort();
    }

    private void ShowConfirm(string text, System.Action onYes)
    {
        HideConfirm();
        var layer = UiStyle.AddFull(_root, new Godot.Control());
        UiStyle.AddFull(layer, new ColorRect { Color = new Color(0f, 0f, 0f, 0.4f) });
        var center = UiStyle.AddFull(layer, new CenterContainer());
        PanelContainer panel = UiStyle.Panel(24);
        panel.CustomMinimumSize = new Vector2(380, 0);
        center.AddChild(panel);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 14);
        panel.AddChild(v);
        v.AddChild(UiStyle.MakeLabel(text, 17, null, true));
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 10);
        v.AddChild(h);
        Button no = UiStyle.MakeButton("Cancel", HideConfirm);
        no.SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill;
        Button yes = UiStyle.MakeButton("Yes", onYes, true);
        yes.SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill;
        h.AddChild(no);
        h.AddChild(yes);
        _confirm = layer;
    }

    private void HideConfirm()
    {
        _confirm?.QueueFree();
        _confirm = null;
    }
}
