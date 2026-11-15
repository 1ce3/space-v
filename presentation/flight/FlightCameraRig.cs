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

    /// <summary>Added to the camera position every frame (metres, camera-local). Camera shake scripts write here.</summary>
    public Vector3 ShakeOffset { get; set; }
    /// <summary>Added to the camera rotation every frame (degrees). Camera shake scripts write here.</summary>
    public Vector3 ShakeRotationDeg { get; set; }

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

    /// <summary>
    /// Roll of the whole view (radians, about Z) so screen-up follows the local vertical of the round planet
    /// (set by BodyEnvironmentVisual when it bends the map; 0 = world up).
    /// </summary>
    public float RollRad { get; set; }

    /// <summary>
    /// The camera never goes lower than this above the ground (m): it rises and looks down at the rocket instead of
    /// diving into the terrain during a landing. 0 = off.
    /// </summary>
    [Export] public float MinCameraAltitudeM { get; set; } = 50f;
    /// <summary>Also on Earth (off: the launch pad keeps its low side view).</summary>
    [Export] public bool MinAltitudeOnEarth { get; set; }

    /// <summary>Camera on +Z looking along -Z (side view of the XY plane).</summary>
    private void PlaceCamera()
    {
        if (Camera == null) return;
        Rotation = new Vector3(0f, 0f, RollRad);
        Camera.Position = new Vector3(0f, DistanceM * HeightOffsetFraction, DistanceM) + ShakeOffset;
        Camera.RotationDegrees = ShakeRotationDeg;
        KeepAboveGround();
        Camera.Far = Mathf.Max(Camera.Far, DistanceM * 20f);
    }

    /// <summary>Raises the camera to MinCameraAltitudeM above the (round) ground and aims it at the rocket.</summary>
    private void KeepAboveGround()
    {
        if (MinCameraAltitudeM <= 0f || _target == null || !IsInstanceValid(_target) || !Camera.IsInsideTree()) return;
        var body = _target.Coordinator?.CurrentBody;
        if (body == null || body.Radius <= 0f) return;
        if (!MinAltitudeOnEarth && body.BodyId != Settings?.MarsBodyId) return;
        // Planet centre is (0, -Radius); ground = the physics sphere.
        var center = new Vector3(0f, -body.Radius, 0f);
        Vector3 cam = Camera.GlobalPosition;
        Vector3 fromCenter = cam - center;
        float altitude = fromCenter.Length() - body.Radius;
        if (altitude >= MinCameraAltitudeM) return;
        Vector3 up = fromCenter.Normalized();
        Camera.GlobalPosition = cam + up * (MinCameraAltitudeM - altitude);
        Vector3 aim = _target.GetWorldCenterOfMass();
        if ((aim - Camera.GlobalPosition).LengthSquared() > 1f) Camera.LookAt(aim, up);
    }

    private float ClampDistance(float d)
    {
        float min = Settings?.CameraMinDistanceM ?? 30f;
        float max = Settings?.CameraMaxDistanceM ?? 400f;
        return Mathf.Clamp(d, min, max);
    }
}
