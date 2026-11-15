using Godot;

namespace RocketGame.Presentation;

/// <summary>
/// One-shot explosion. With a custom scene (art team) it just instantiates it; otherwise a code placeholder:
/// light flash, growing fireball and smoke particles. Frees itself after a few seconds.
/// </summary>
public static class ExplosionEffect
{
    public static Node3D Spawn(Node parent, Vector3 globalPosition, float scale = 1f, PackedScene custom = null)
    {
        if (parent == null) return null;
        Node3D root = custom?.Instantiate<Node3D>() ?? BuildPlaceholder(scale);
        parent.AddChild(root);
        root.GlobalPosition = globalPosition;
        if (custom != null) root.Scale = Vector3.One * scale;
        parent.GetTree().CreateTimer(4.0).Timeout += () => { if (GodotObject.IsInstanceValid(root)) root.QueueFree(); };
        return root;
    }

    private static Node3D BuildPlaceholder(float s)
    {
        var root = new Node3D { Name = "Explosion" };

        var light = new OmniLight3D { LightColor = new Color(1f, 0.6f, 0.25f), LightEnergy = 12f, OmniRange = 60f * s };
        root.AddChild(light);

        var fireMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1f, 0.75f, 0.3f, 1f),
        };
        var fireball = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 2f }, MaterialOverride = fireMat, Scale = Vector3.One * 0.5f * s };
        root.AddChild(fireball);

        var smokeMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            VertexColorUseAsAlbedo = true,
        };
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(1f, 0.6f, 0.2f, 1f));
        ramp.SetColor(1, new Color(0.15f, 0.15f, 0.15f, 0f));
        var smoke = new CpuParticles3D
        {
            OneShot = true, Emitting = true, Amount = 60, Lifetime = 2.5, Explosiveness = 0.95f,
            Mesh = new QuadMesh { Size = Vector2.One * 3f * s, Material = smokeMat },
            Direction = Vector3.Up, Spread = 180f, InitialVelocityMin = 8f * s, InitialVelocityMax = 25f * s,
            Gravity = new Vector3(0f, -2f, 0f), DampingMin = 3f, DampingMax = 6f,
            ScaleAmountMin = 0.6f, ScaleAmountMax = 1.8f, ColorRamp = ramp,
        };
        root.AddChild(smoke);

        var debris = new CpuParticles3D
        {
            OneShot = true, Emitting = true, Amount = 30, Lifetime = 2.0, Explosiveness = 1f,
            Mesh = new BoxMesh { Size = Vector3.One * 0.4f * s },
            Direction = Vector3.Up, Spread = 180f, InitialVelocityMin = 15f * s, InitialVelocityMax = 40f * s,
            Gravity = new Vector3(0f, -9.8f, 0f), Color = new Color(0.25f, 0.22f, 0.2f),
        };
        root.AddChild(debris);

        Tween t = root.CreateTween().SetParallel();
        t.TweenProperty(fireball, "scale", Vector3.One * 8f * s, 0.6f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        t.TweenProperty(fireMat, "albedo_color", new Color(0.6f, 0.2f, 0.05f, 0f), 0.9f);
        t.TweenProperty(light, "light_energy", 0f, 1.2f);
        return root;
    }
}
