using Godot;
using RocketGame.Core;
using RocketGame.Physics;
using RocketGame.Research;

namespace RocketGame.Tests.B;

/// <summary>Sandbox for task B0: loads the mission and physics settings and prints what Godot read from the .tres files.</summary>
public partial class ResourceCheck : Node
{
    /// <summary>Mission to check (mars_mission.tres).</summary>
    [Export] public MissionDefinition Mission { get; set; }
    /// <summary>Physics settings to check (physics_settings.tres).</summary>
    [Export] public PhysicsSettings Settings { get; set; }
    /// <summary>Mach numbers at which the drag curve is printed.</summary>
    [Export] public float[] MachSamples { get; set; } = { 0f, 0.5f, 1.1f, 2f, 3f, 5f };

    public override void _Ready()
    {
        if (!NodeGuard.Require(this, Mission, nameof(Mission))) return;
        if (!NodeGuard.Require(this, Settings, nameof(Settings))) return;

        GD.Print($"[Research] Mission {Mission.MissionId}: payload {Mission.PayloadMassKg} kg, {Mission.Segments.Count} segments");
        foreach (MissionSegmentDefinition s in Mission.Segments)
        {
            string body = s.Body != null ? s.Body.BodyId : "(null)";
            GD.Print($"[Research]   {s.SegmentId}: {s.Kind}, body {body}, RewardRP {s.RewardRP}");
        }

        GD.Print($"[Physics] MachDragCurve {(Settings.MachDragCurve != null ? "loaded" : "null (built-in defaults)")}");
        foreach (float mach in MachSamples)
        {
            GD.Print($"[Physics]   Mach {mach:F1}: drag x{Settings.SampleMachDrag(mach):F2}");
        }
    }
}
