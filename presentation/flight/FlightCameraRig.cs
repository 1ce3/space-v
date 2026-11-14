using Godot;
using RocketGame.Control;
using RocketGame.Core;

namespace RocketGame.Presentation;

/// <summary>Side-view camera that follows the vehicle's center of mass; zoom with flight_zoom_in/out.</summary>
public partial class FlightCameraRig : Node3D
{
    [Export] public Camera3D Camera { get; set; }
    [Export] public FlightSettings Settings { get; set; }
    /// <summary>Zoom factor per wheel step / key press.</summary>
    [Export(PropertyHint.Range, "1.01,2,0.01")] public float ZoomFactor { get; set; } = 1.15f;
    /// <summary>Follow smoothing; 0 = rigid (no lag at high speed).</summary>
    [Export(PropertyHint.Range, "0,20,0.1")] public float FollowSharpness { get; set; } = 0f;
    /// <summary>Start distance = rocket height x this factor (clamped to the settings range).</summary>
    [Export(PropertyHint.Range, "0.5,10,0.1")] public float FrameHeightFactor { get; set; } = 1.6f;
    /// <summary>Camera height above the CoM, as a fraction of the distance.</summary>
    [Export(PropertyHint.Range, "-1,1,0.01")] public float HeightOffsetFraction { get; set; } = 0.05f;

    /// <summary>Current distance from the target (m).</summary>
    public float DistanceM { get; private set; } = 60f;

    private VehicleRoot _target;

    public override void _Ready()
    {
        NodeGuard.Require(this, Camera, nameof(Camera));
    }

    /// <summary>Starts following the vehicle (snaps immediately).</summary>
    public void Follow(VehicleRoot vehicle)
    {
        _target = vehicle;
        DistanceM = ClampDistance((vehicle?.GetHeightM() ?? 0f) * FrameHeightFactor);
        if (_target != null) GlobalPosition = TargetPoint();
        PlaceCamera();
        if (Camera != null) Camera.Current = true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("flight_zoom_in")) Zoom(1f / ZoomFactor);
        else if (@event.IsActionPressed("flight_zoom_out")) Zoom(ZoomFactor);
    }

    public override void _Process(double delta)
    {
        if (_target == null || !IsInstanceValid(_target)) return;
        Vector3 goal = TargetPoint();
        float t = FollowSharpness <= 0f ? 1f : 1f - Mathf.Exp(-FollowSharpness * (float)delta);
        GlobalPosition = GlobalPosition.Lerp(goal, t);
        PlaceCamera();
    }

    private void Zoom(float factor)
    {
        DistanceM = ClampDistance(DistanceM * factor);
        PlaceCamera();
    }

    private Vector3 TargetPoint() => _target.GetWorldCenterOfMass();

    /// <summary>Camera on +Z looking along -Z (side view of the XY plane).</summary>
    private void PlaceCamera()
    {
        if (Camera == null) return;
        Camera.Position = new Vector3(0f, DistanceM * HeightOffsetFraction, DistanceM);
        Camera.Rotation = Vector3.Zero;
        Camera.Far = Mathf.Max(Camera.Far, DistanceM * 20f);
    }

    private float ClampDistance(float d)
    {
        float min = Settings?.CameraMinDistanceM ?? 30f;
        float max = Settings?.CameraMaxDistanceM ?? 400f;
        return Mathf.Clamp(d, min, max);
    }
}
