using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>Cuts a broken part off the tree and turns cut parts into falling debris (guide B 4.4).</summary>
public class StructuralBreakHandler
{
    private readonly PhysicsSettings _settings;

    public StructuralBreakHandler(PhysicsSettings settings) => _settings = settings;

    /// <summary>Severs the subtree of part with SeverCause.Break. Called by FailureDetector.TriggerBreak.</summary>
    public void HandleBreak(RocketPart part, FailureCause cause)
    {
        // TODO(B3): part.Tree.SeverSubtree(part, SeverCause.Break) once A's PartTree is on main.
        GD.Print($"[Physics] Break {cause} at {part?.Definition?.ModelId}");
    }

    /// <summary>Moves cut parts into a new DebrisBody under worldParent, keeping their transforms. Called by VehicleRoot (C) for every cut.</summary>
    public DebrisBody SpawnDebrisRigidBody(IReadOnlyList<RocketPart> parts, Vector3 linearVelocity, Vector3 angularVelocity,
        Vector3 impulse, Node3D worldParent, CelestialBodyDefinition body)
    {
        // TODO(B3): create DebrisBody, reparent parts keeping GlobalTransform, set mass and velocity, apply impulse, PartVisual.OnSevered.
        return null;
    }

    /// <summary>Momentum hand-over between vehicle and debris (handled inside SpawnDebrisRigidBody; kept for the diagram contract).</summary>
    public void TransferMomentum()
    {
    }
}
