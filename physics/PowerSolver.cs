using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>Electric supply and demand of the pod modules (guide B 5, P1 in flight).</summary>
public class PowerSolver
{
    /// <summary>Hours in one cruise day.</summary>
    public const float HoursPerDay = 24f;

    private readonly PhysicsSettings _settings;

    public PowerSolver(PhysicsSettings settings) => _settings = settings;

    /// <summary>One flight tick (P1).</summary>
    public PowerStepResult Step(PartTree parts, float dynamicPressure, float solarFluxWm2, float deltaTime)
    {
        // TODO(B6): supply vs demand, battery drain, brownout.
        return new PowerStepResult { BatteryFraction = 1f };
    }

    /// <summary>
    /// One cruise day (guide B 5 step 2). Supply = deployed healthy solar arrays (NominalOutputW * flux / 1361) + RTGs after decay.
    /// Demand = PowerDrawW of every healthy running module + pod PowerDrawW * CruiseDutyFactor.
    /// The 24 h balance charges or drains the batteries (clamped 0..capacity). Batteries empty while supply is below demand:
    /// brownout, every avionics module gets IsPowered = false. A day without brownout powers them again.
    /// </summary>
    public PowerStepResult SolveSteadyState(PartTree parts, float solarFluxWm2)
    {
        var result = new PowerStepResult();
        CommandPodPart pod = FindPod(parts);
        if (pod == null) return result;

        foreach (PodModule module in pod.Modules)
        {
            if (module is PowerModule power) result.SupplyW += power.GetOutputW(0f, solarFluxWm2);
            if (IsRunning(module)) result.DemandW += module.Definition?.PowerDrawW ?? 0f;
        }
        if (pod.State != ComponentState.Broken)
            result.DemandW += (pod.Definition?.PowerDrawW ?? 0f) * (_settings?.CruiseDutyFactor ?? 0.3f);

        float energyWh = (result.SupplyW - result.DemandW) * HoursPerDay;
        float unservedWh = 0f;
        if (energyWh >= 0f)
        {
            float spare = energyWh;
            foreach (BatteryModule battery in pod.GetModules<BatteryModule>())
            {
                if (spare <= 0f) break;
                spare += battery.Draw(-spare); // Draw returns a negative amount when charging.
            }
        }
        else
        {
            unservedWh = -energyWh;
            foreach (BatteryModule battery in pod.GetModules<BatteryModule>())
            {
                if (unservedWh <= 0f) break;
                unservedWh -= battery.Draw(unservedWh);
            }
        }

        result.IsBrownout = unservedWh > 0.001f;
        result.ConsumedW = result.DemandW - unservedWh / HoursPerDay;
        foreach (PodModule module in pod.Modules)
            if (module.Definition?.SlotType == ModuleSlotType.Avionics) module.IsPowered = !result.IsBrownout;

        result.BatteryFraction = BatteryFraction(pod);
        return result;
    }

    /// <summary>Total battery charge 0..1 (0 when there is no battery).</summary>
    public static float BatteryFraction(CommandPodPart pod)
    {
        float stored = 0f, capacity = 0f;
        foreach (BatteryModule battery in pod.GetModules<BatteryModule>())
        {
            stored += battery.StoredEnergyWh;
            capacity += battery.PowerDefinition?.EnergyStorageWh ?? 0f;
        }
        return capacity > 0f ? Mathf.Clamp(stored / capacity, 0f, 1f) : 0f;
    }

    /// <summary>The command pod (root of the tree), or null.</summary>
    public static CommandPodPart FindPod(PartTree parts)
    {
        if (parts == null) return null;
        if (parts.RootPart is CommandPodPart root && root.IsAttached) return root;
        foreach (RocketPart part in parts.ActiveParts)
            if (part is CommandPodPart pod) return pod;
        return null;
    }

    /// <summary>A module that wants power: healthy (power state ignored, so a browned-out module still asks), radiators only when extended.</summary>
    private static bool IsRunning(PodModule module)
    {
        if (!module.IsHealthy) return false;
        if (module is ActiveRadiatorModule radiator) return radiator.IsPanelExtended;
        return true;
    }
}
