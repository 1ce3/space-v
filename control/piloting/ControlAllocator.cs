using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>
/// Sends one normalized command u to every usable actuator: engine gimbal, control fins,
/// reaction wheels and RCS (P0 rule of C_CONTROL.md 5.5, decision #13).
/// Gimbal and fins flip sign with their position vs the CoM so every actuator pushes the same way.
/// </summary>
public partial class ControlAllocator : Node
{
    [Export] public VehicleRoot Vehicle { get; set; }
    [Export] public bool DebugLog { get; set; }

    /// <summary>Current moment of inertia about Z (kg*m^2), from Vehicle.MassProps.</summary>
    public float MomentOfInertia { get; private set; }
    /// <summary>Last command sent (-1..1).</summary>
    public float LastCommand { get; private set; }
    /// <summary>Sideways RCS command (-1..1), written by FlightControlSubsystem from the pilot input.</summary>
    public float TranslateCommand { get; set; }

    private PartTree _tree;

    public override void _Ready()
    {
        NodeGuard.Require(this, Vehicle, nameof(Vehicle));
    }

    /// <summary>Gives the allocator the part tree to find actuators.</summary>
    public void Bind(PartTree tree)
    {
        _tree = tree;
        LastCommand = 0f;
        TranslateCommand = 0f;
        OnMassPropertiesChanged();
    }

    /// <summary>Called on MassPropertiesChanged.</summary>
    public void OnMassPropertiesChanged()
    {
        if (Vehicle != null) MomentOfInertia = Vehicle.MassProps.MomentOfInertia;
    }

    /// <summary>Applies the command. P0: angularAccelDemand is the normalized u in -1..1.</summary>
    public void Allocate(float angularAccelDemand, float dynamicPressure)
    {
        if (_tree == null || Vehicle == null) return;
        float u = Mathf.Clamp(angularAccelDemand, -1f, 1f);
        LastCommand = u;
        float comY = Vehicle.MassProps.CenterOfMass.Y;

        foreach (EnginePart engine in _tree.GetParts<EnginePart>())
        {
            if (!engine.IsAttached) continue;
            float range = engine.EngineDefinition?.GimbalRangeDeg ?? 0f;
            engine.SetGimbal(engine.IsIgnited ? SignFor(engine, comY) * u * range : 0f);
        }

        foreach (ControlFinPart fin in _tree.GetParts<ControlFinPart>())
        {
            if (!fin.IsAttached) continue;
            float max = fin.AeroDefinition?.MaxDeflectionDeg ?? 0f;
            fin.SetDeflection(SignFor(fin, comY) * u * max);
        }

        foreach (CommandPodPart pod in _tree.GetParts<CommandPodPart>())
        {
            if (!pod.IsAttached) continue;
            bool rcsOn = false;
            foreach (RcsThrusterModule rcs in pod.GetModules<RcsThrusterModule>())
            {
                rcs.CommandTorque(rcs.IsEnabled ? u : 0f);
                rcs.CommandTranslate(rcs.IsEnabled ? TranslateCommand : 0f);
                rcsOn |= rcs.IsEnabled && rcs.CanOperate() && rcs.MonopropRemainingKg > 0f;
            }
            foreach (ReactionWheelModule wheel in pod.GetModules<ReactionWheelModule>())
            {
                wheel.CommandTorque(wheel.CanOperate() ? u : 0f);
                if (wheel.IsSaturated && rcsOn) wheel.DumpMomentum();
            }
        }
    }

    /// <summary>Rough total control torque available right now (N*m), for the HUD.</summary>
    public float GetAvailableTorque()
    {
        // TODO(C7): gimbal and fin torque need thrust and q; only modules for now.
        if (_tree == null) return 0f;
        float total = 0f;
        foreach (CommandPodPart pod in _tree.GetParts<CommandPodPart>())
            foreach (AttitudeActuatorModule module in pod.GetModules<AttitudeActuatorModule>())
                if (module.CanOperate() && (module is not RcsThrusterModule rcs || rcs.IsEnabled)) total += module.GetMaxTorque();
        return total;
    }

    /// <summary>+1 below the CoM, -1 above: a push to +X below the CoM gives positive torque.</summary>
    private static float SignFor(RocketPart part, float comY) => part.GetLocalPosition().Y < comY ? 1f : -1f;
}
