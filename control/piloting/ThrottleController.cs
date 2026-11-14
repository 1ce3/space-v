using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>Throttle state, G limiter and liquid engine ignite/shutdown (C_CONTROL.md 5.2).</summary>
public partial class ThrottleController : Node
{
    [Export] public FlightSettings Settings { get; set; }
    [Export] public bool GLimiterEnabled { get; set; } = true;
    [Export] public bool DebugLog { get; set; }

    /// <summary>Throttle command 0..1.</summary>
    public float Throttle { get; private set; }
    /// <summary>True in safe mode (SeuGlitch): the throttle cannot change.</summary>
    public bool IsFrozen { get; set; }
    /// <summary>Max G of the command pod; 0 disables the limiter.</summary>
    public float MaxGForce { get; set; }

    private PartTree _tree;

    public override void _Ready()
    {
        NodeGuard.Require(this, Settings, nameof(Settings));
    }

    /// <summary>Gives the controller the part tree to find liquid engines and the pod.</summary>
    public void Bind(PartTree tree)
    {
        _tree = tree;
        Throttle = 0f;
        CommandPodPart pod = tree?.RootPart as CommandPodPart;
        MaxGForce = pod?.PodDefinition?.MaxGForce ?? 0f;
    }

    /// <summary>Updates Throttle from input, applies the G limiter, then commands the liquid engines.</summary>
    public void Update(PilotInput input, float currentGForce, float deltaTime)
    {
        if (!IsFrozen && Settings != null)
        {
            float next = Throttle + input.ThrottleAxis * Settings.ThrottleRatePerSec * deltaTime;
            if (input.ThrottleFull) next = 1f;
            if (input.ThrottleCut) next = 0f;
            Throttle = Mathf.Clamp(next, 0f, 1f);
        }

        if (GLimiterEnabled && MaxGForce > 0f)
            Throttle = ClampThrottleForGLimit(Throttle, currentGForce, MaxGForce);

        if (_tree == null) return;
        foreach (LiquidEnginePart engine in _tree.GetParts<LiquidEnginePart>())
        {
            if (!engine.IsArmed) continue;
            if (Throttle > 0f && !engine.IsIgnited) engine.Ignite();
            else if (Throttle <= 0f && engine.IsIgnited) engine.Shutdown();
            engine.SetThrottle(Throttle);
        }
    }

    /// <summary>Scales the throttle down so G stays under maxGLimit (assumes G roughly proportional to throttle).</summary>
    public float ClampThrottleForGLimit(float throttle, float currentGForce, float maxGLimit)
    {
        if (maxGLimit <= 0f || currentGForce <= maxGLimit || currentGForce <= 0f) return throttle;
        return Mathf.Clamp(throttle * maxGLimit / currentGForce, 0f, 1f);
    }

    /// <summary>Sets the throttle directly (cut at segment end).</summary>
    public void SetThrottle(float value)
    {
        Throttle = Mathf.Clamp(value, 0f, 1f);
    }
}
