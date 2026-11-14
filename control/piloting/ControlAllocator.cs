using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>
/// Sends one normalized command u to every usable actuator: engine gimbal, control fins,
/// reaction wheels and RCS (P0 rule of C_CONTROL.md 5.5, decision #13).
/// </summary>
public partial class ControlAllocator : Node
{
    [Export] public VehicleRoot Vehicle { get; set; }
    [Export] public bool DebugLog { get; set; }

    /// <summary>Current moment of inertia about Z (kg*m^2), from Vehicle.MassProps.</summary>
    public float MomentOfInertia { get; private set; }

    private PartTree _tree;

    public override void _Ready()
    {
        NodeGuard.Require(this, Vehicle, nameof(Vehicle));
    }

    /// <summary>Gives the allocator the part tree to find actuators.</summary>
    public void Bind(PartTree tree)
    {
        _tree = tree;
    }

    /// <summary>Called on MassPropertiesChanged.</summary>
    public void OnMassPropertiesChanged()
    {
        if (Vehicle != null) MomentOfInertia = Vehicle.MassProps.MomentOfInertia;
    }

    /// <summary>Applies the command. P0: angularAccelDemand is the normalized u in -1..1.</summary>
    public void Allocate(float angularAccelDemand, float dynamicPressure)
    {
        // TODO(C4): sign by part position vs CoM; SetGimbal, SetDeflection, CommandTorque, DumpMomentum.
    }

    /// <summary>Rough total control torque available right now (N*m), for the HUD.</summary>
    public float GetAvailableTorque()
    {
        // TODO(C7)
        return 0f;
    }
}
