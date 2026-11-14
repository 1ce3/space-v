using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>Cuts a broken part off the tree and turns cut parts into falling debris (guide B 4.4).</summary>
public class StructuralBreakHandler
{
    /// <summary>Radius of the placeholder collision sphere of a debris body (m). Collisions are off (layer and mask 0).</summary>
    private const float DebrisShapeRadiusM = 0.5f;
    /// <summary>Smallest mass given to a debris body (kg), so Jolt never gets zero.</summary>
    private const float MinDebrisMassKg = 1f;

    /// <summary>PartVisual.OnSevered(SeverCause), called by name.</summary>
    private static readonly StringName OnSeveredMethod = "OnSevered";

    private readonly PhysicsSettings _settings;

    public StructuralBreakHandler(PhysicsSettings settings) => _settings = settings;

    /// <summary>Severs the subtree of part with SeverCause.Break. Called by FailureDetector.TriggerBreak.</summary>
    public void HandleBreak(RocketPart part, FailureCause cause)
    {
        if (part?.Tree == null)
        {
            GD.PushWarning($"[Physics] Break {cause} on a part without a tree, ignored.");
            return;
        }
        GD.Print($"[Physics] Break {cause} at {part.ModelId}");
        part.Tree.SeverSubtree(part, SeverCause.Break);
    }

    /// <summary>
    /// Moves cut parts into a new DebrisBody under worldParent, keeping their transforms, and gives it the vehicle's velocity plus impulse (N*s).
    /// Called by VehicleRoot (C) for every cut, outside the physics callback.
    /// </summary>
    public DebrisBody SpawnDebrisRigidBody(IReadOnlyList<RocketPart> parts, Vector3 linearVelocity, Vector3 angularVelocity,
        Vector3 impulse, Node3D worldParent, CelestialBodyDefinition body, SeverCause cause = SeverCause.Jettison)
    {
        if (parts == null || parts.Count == 0 || worldParent == null)
        {
            GD.PushWarning("[Physics] SpawnDebrisRigidBody called without parts or parent.");
            return null;
        }

        float mass = 0f;
        Vector3 weighted = Vector3.Zero;
        foreach (RocketPart part in parts)
        {
            float m = Mathf.Max(part.GetCurrentMass(), 0f);
            mass += m;
            weighted += part.GlobalPosition * m;
        }
        Vector3 center = mass > 0f ? weighted / mass : parts[0].GlobalPosition;

        var debris = new DebrisBody { Name = "Debris" };
        worldParent.AddChild(debris);
        debris.GlobalPosition = center;
        debris.Initialize(body, _settings);
        debris.Mass = Mathf.Max(mass, MinDebrisMassKg);
        debris.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = DebrisShapeRadiusM } });

        foreach (RocketPart part in parts)
        {
            part.Reparent(debris, true);
            // Presentation is not referenced by type (AGENTS rule 5): notify any child visual by method name.
            foreach (Node child in part.GetChildren())
            {
                if (child.HasMethod(OnSeveredMethod)) child.Call(OnSeveredMethod, (int)cause);
            }
        }

        debris.LinearVelocity = linearVelocity;
        debris.AngularVelocity = angularVelocity;
        debris.ApplyCentralImpulse(impulse);
        return debris;
    }

    /// <summary>Momentum hand-over between vehicle and debris (done inside SpawnDebrisRigidBody; kept for the diagram contract).</summary>
    public void TransferMomentum()
    {
    }
}
