using Godot;

namespace RocketGame.Physics;

/// <summary>Rigid body holding parts cut from the vehicle. Pulls itself toward the planet and frees itself when it lands or is far away.</summary>
public partial class DebrisBody : RigidBody3D
{
    private CelestialBodyDefinition _body;
    private PhysicsSettings _settings;

    /// <summary>Runtime setup (debris is created in code, so it cannot use exports).</summary>
    public void Initialize(CelestialBodyDefinition body, PhysicsSettings settings)
    {
        _body = body;
        _settings = settings;
        GravityScale = 0f;
        CollisionLayer = 0;
        CollisionMask = 0;
        AxisLockLinearZ = true;
        AxisLockAngularX = true;
        AxisLockAngularY = true;
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        // TODO(B3): central gravity toward (0, -Radius); QueueFree below altitude 0 or beyond DebrisDespawnDistanceM from the camera.
    }
}
