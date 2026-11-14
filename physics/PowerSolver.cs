using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>Electric supply and demand of the pod modules (guide B 5, P1 in flight).</summary>
public class PowerSolver
{
    /// <summary>Hours in one cruise day.</summary>
    public const float HoursPerDay = 24f;
    /// <summary>Average unserved power (W) below which a step does not count as a brownout (rounding).</summary>
    public const float BrownoutToleranceW = 0.1f;
    /// <summary>Below this total stored energy (Wh) the batteries count as empty.</summary>
    public const float EmptyBatteryWh = 0.01f;

    private readonly PhysicsSettings _settings;

    public PowerSolver(PhysicsSettings settings) => _settings = settings;

    /// <summary>True while the last step ended in a brownout (avionics switched off by this solver).</summary>
    public bool IsBrownout { get; private set; }

    /// <summary>
    /// One flight tick (guide B 4.6, P1): supply = solar arrays (deployed, not torn by q) at solarFluxWm2 + RTGs;
    /// demand = running modules + the pod at full duty. The balance over deltaTime charges or drains the batteries.
    /// </summary>
    public PowerStepResult Step(PartTree parts, float dynamicPressure, float solarFluxWm2, float deltaTime)
    {
        var result = new PowerStepResult();
        CommandPodPart pod = FindPod(parts);
        if (pod == null || deltaTime <= 0f) return result;

        Measure(pod, dynamicPressure, solarFluxWm2, 1f, ref result);
        Balance(pod, deltaTime / 3600f, ref result);
        return result;
    }

    /// <summary>
    /// One cruise day (guide B 5 step 2). Supply = deployed healthy solar arrays (NominalOutputW * flux / 1361) + RTGs after decay.
    /// Demand = PowerDrawW of every healthy running module + pod PowerDrawW * CruiseDutyFactor.
    /// The 24 h balance charges or drains the batteries (clamped 0..capacity).
    /// </summary>
    public PowerStepResult SolveSteadyState(PartTree parts, float solarFluxWm2)
    {
        var result = new PowerStepResult();
        CommandPodPart pod = FindPod(parts);
        if (pod == null) return result;

        Measure(pod, 0f, solarFluxWm2, _settings?.CruiseDutyFactor ?? 0.3f, ref result);
        Balance(pod, HoursPerDay, ref result);
        return result;
    }

    private static void Measure(CommandPodPart pod, float dynamicPressure, float solarFluxWm2, float podDuty, ref PowerStepResult result)
    {
        foreach (PodModule module in pod.Modules)
        {
            if (module is PowerModule power) result.SupplyW += power.GetOutputW(dynamicPressure, solarFluxWm2);
            if (IsRunning(module)) result.DemandW += module.Definition?.PowerDrawW ?? 0f;
        }
        if (pod.State != ComponentState.Broken)
            result.DemandW += (pod.Definition?.PowerDrawW ?? 0f) * podDuty;
    }

    /// <summary>
    /// Charges or drains the batteries with (supply - demand) over hours. Batteries empty while supply is below demand:
    /// brownout, every avionics module gets IsPowered = false. When power is back the solver switches them on again
    /// (only the ones it switched off, so other systems keep control of IsPowered otherwise).
    /// </summary>
    private void Balance(CommandPodPart pod, float hours, ref PowerStepResult result)
    {
        float energyWh = (result.SupplyW - result.DemandW) * hours;
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

        // Float rounding of a 500 Wh battery is larger than one 60 Hz tick of consumption, so a shortfall only counts
        // as a brownout when the batteries are really empty.
        bool batteriesEmpty = BatteryStoredWh(pod) < EmptyBatteryWh;
        if (!batteriesEmpty) unservedWh = 0f;
        result.IsBrownout = hours > 0f && batteriesEmpty && unservedWh / hours > BrownoutToleranceW;
        result.ConsumedW = hours > 0f ? result.DemandW - unservedWh / hours : result.DemandW;
        if (result.IsBrownout != IsBrownout)
        {
            foreach (PodModule module in pod.Modules)
                if (module.Definition?.SlotType == ModuleSlotType.Avionics) module.IsPowered = !result.IsBrownout;
            IsBrownout = result.IsBrownout;
        }
        result.BatteryFraction = BatteryFraction(pod);
    }

    private static float BatteryStoredWh(CommandPodPart pod)
    {
        float stored = 0f;
        foreach (BatteryModule battery in pod.GetModules<BatteryModule>()) stored += battery.StoredEnergyWh;
        return stored;
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
