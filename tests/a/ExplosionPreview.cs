// AI-assisted: written with Claude (Anthropic); reviewed by The Neptunians.
using Godot;
using RocketGame.Presentation;

namespace RocketGame.Tests;

/// <summary>
/// Run tests/a/explosion_preview.tscn (F6). Spawns the flight explosion (scenes/Explosion.tscn through
/// ExplosionEffect.Spawn, with its sound) every 2.5 s, and checks the preview Camera3D / WorldEnvironment of the
/// art scene were stripped so they cannot take over the game view. Stays open.
/// </summary>
public partial class ExplosionPreview : Node3D
{
    [Export] public PackedScene ExplosionScene { get; set; }
    [Export] public float ExplosionScale { get; set; } = 1.5f;
    [Export] public float IntervalS { get; set; } = 2.5f;

    private float _timer;
    private bool _checked;

    public override void _Process(double delta)
    {
        _timer -= (float)delta;
        if (_timer > 0f) return;
        _timer = IntervalS;
        Node3D boom = ExplosionEffect.Spawn(this, Vector3.Zero, ExplosionScale, ExplosionScene);
        if (_checked) return;
        _checked = true;
        bool noCamera = boom != null && boom.FindChildren("*", nameof(Camera3D), true, false).Count == 0;
        bool noEnv = boom != null && boom.FindChildren("*", nameof(WorldEnvironment), true, false).Count == 0;
        bool sound = GetChildren().Count > 0 && FindChildren("*", nameof(AudioStreamPlayer3D), true, false).Count > 0;
        GD.Print($"[Explosion preview] {(noCamera ? "OK  " : "FAIL")} preview camera stripped");
        GD.Print($"[Explosion preview] {(noEnv ? "OK  " : "FAIL")} preview environment stripped");
        GD.Print($"[Explosion preview] {(sound ? "OK  " : "FAIL")} explosion sound playing");
        GD.Print($"[Explosion preview] own camera still current: {GetViewport().GetCamera3D()?.Name}");
    }
}
