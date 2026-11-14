using Godot;
using RocketGame.Control;
using RocketGame.Core;

namespace RocketGame.Presentation;

/// <summary>Side-view camera that follows the vehicle; zoom with flight_zoom_in/out.</summary>
public partial class FlightCameraRig : Node3D
{
    [Export] public Camera3D Camera { get; set; }
    [Export] public FlightSettings Settings { get; set; }
    /// <summary>Distance change per zoom step (m).</summary>
    [Export] public float ZoomStepM { get; set; } = 20f;
    /// <summary>Follow smoothing; 0 = rigid.</summary>
    [Export(PropertyHint.Range, "0,20,0.1")] public float FollowSharpness { get; set; } = 8f;

    /// <summary>Current distance from the target (m).</summary>
    public float DistanceM { get; private set; } = 80f;

    private Node3D _target;

    public override void _Ready()
    {
        NodeGuard.Require(this, Camera, nameof(Camera));
    }

    /// <summary>Starts following the vehicle.</summary>
    public void Follow(VehicleRoot vehicle)
    {
        _target = vehicle;
        // TODO(C2): snap to target, look along -Z.
    }

    public override void _Process(double delta)
    {
        // TODO(C2): lerp position to the target, apply zoom within Settings min/max.
    }
}
