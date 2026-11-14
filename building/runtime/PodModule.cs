using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Device in a command pod slot. Plain C# class (not a Node).</summary>
public abstract class PodModule : IFlightComponent
{
    public ModuleDefinition Definition { get; private set; }
    /// <summary>Pod it is installed in.</summary>
    public CommandPodPart Pod { get; set; }
    /// <summary>Index within its slot type.</summary>
    public int SlotIndex { get; set; }

    public ComponentState State { get; set; } = ComponentState.Idle;
    /// <summary>Temperature (K).</summary>
    public float TempK { get; set; } = 288f;
    /// <summary>False during a brownout or after lightning (avionics).</summary>
    public bool IsPowered { get; set; } = true;

    public ComponentDefinition BaseDefinition => Definition;
    public float HeatCapacityJPerK => DryMass * ThermalStateRules.SpecificHeat;
    public string ModelId => Definition?.ModelId ?? "";
    /// <summary>Mass without consumables (kg).</summary>
    public float DryMass => Definition?.DryMass ?? 0f;

    /// <summary>Creates the runtime class that matches the definition Kind.</summary>
    public static PodModule Create(ModuleDefinition def)
    {
        PodModule module = def switch
        {
            AcsModuleDefinition acs => acs.Kind switch
            {
                AcsDeviceKind.Imu => new GyroscopeImuModule(),
                AcsDeviceKind.StarTracker => new StarTrackerModule(),
                AcsDeviceKind.ReactionWheel => new ReactionWheelModule(),
                _ => new RcsThrusterModule(),
            },
            ThermalModuleDefinition thermal => thermal.Kind switch
            {
                ThermalDeviceKind.PassiveInsulation => new PassiveInsulationModule(),
                ThermalDeviceKind.ActiveRadiator => new ActiveRadiatorModule(),
                _ => new RadioisotopeHeaterModule(),
            },
            PowerModuleDefinition power => power.Kind switch
            {
                PowerDeviceKind.Battery => new BatteryModule(),
                PowerDeviceKind.SolarArray => new SolarArrayModule(),
                _ => new RtgModule(),
            },
            _ => null,
        };

        if (module == null)
        {
            GD.PushError($"[Building] PodModule.Create: unsupported definition '{def?.ModelId ?? "null"}'");
            return null;
        }

        module.Initialize(def);
        return module;
    }

    /// <summary>Sets the definition and resets runtime state.</summary>
    public void Initialize(ModuleDefinition def)
    {
        Definition = def;
        ResetRuntimeState();
    }

    /// <summary>Subclasses fill their counters here.</summary>
    protected virtual void ResetRuntimeState()
    {
        State = ComponentState.Idle;
        TempK = 288f;
        IsPowered = true;
    }

    /// <summary>Dry mass plus consumables (kg).</summary>
    public virtual float GetCurrentMass() => DryMass;

    public void ApplyThermalLoad(float heatJoule)
    {
        if (HeatCapacityJPerK <= 0f) return;
        TempK = Mathf.Max(0f, TempK + heatJoule / HeatCapacityJPerK);
        ThermalStateRules.Evaluate(this);
    }

    /// <summary>Not Broken, Overheated or Frozen (power is not considered).</summary>
    public bool IsHealthy =>
        State != ComponentState.Broken && State != ComponentState.Overheated && State != ComponentState.Frozen;

    public virtual bool CanOperate() => IsPowered && IsHealthy;

    public virtual void OnFixedTick(float deltaTime) { }
}
