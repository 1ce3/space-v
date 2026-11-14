using Godot;

namespace RocketGame.Physics;

/// <summary>Rigid body holding parts cut from the vehicle. Pulls itself toward the planet center and frees itself when it lands or is far from the camera.</summary>
public partial class DebrisBody : RigidBody3D
{
    private CelestialBodyDefinition _body;
    private PhysicsSettings _settings;
    private readonly OrbitalGravitySolver _gravity = new();
    private float _age;

    /// <summary>Runtime setup (debris is created in code, so it cannot use exports).</summary>
    public void Initialize(CelestialBodyDefinition body, PhysicsSettings settings)
    {
        _body = body;
        _settings = settings;
        if (body != null) _gravity.Configure(body);
        GravityScale = 0f;
        CollisionLayer = 0;
        CollisionMask = 0;
        CanSleep = false;
        AxisLockLinearZ = true;
        AxisLockAngularX = true;
        AxisLockAngularY = true;
        CenterOfMassMode = CenterOfMassModeEnum.Custom;
        CenterOfMass = Vector3.Zero;
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (_body == null) return;
        Vector3 origin = state.Transform.Origin;
        var position = new Vector2(origin.X, origin.Y);
        Vector2 force = _gravity.CalculateGravityForce(position, Mass);
        state.ApplyCentralForce(new Vector3(force.X, force.Y, 0f));

        float altitude = _gravity.GetAltitude(position);
        bool far = IsFarFromCamera(origin);
        if ((altitude < 0f || far) && !IsQueuedForDeletion())
        {
            GD.Print($"[Physics] Debris {Name} removed: {(altitude < 0f ? "below ground" : "far from camera")} (alt {altitude:0} m, age {_age:0.0} s)");
            QueueFree();
        }
        _age += state.Step;
    }

    private bool IsFarFromCamera(Vector3 origin)
    {
        Camera3D camera = GetViewport()?.GetCamera3D();
        if (camera == null || _settings == null) return false;
        return camera.GlobalPosition.DistanceTo(origin) > _settings.DebrisDespawnDistanceM;
    }
}
