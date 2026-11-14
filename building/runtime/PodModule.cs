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
        // TODO(A1): AcsModuleDefinition.Kind -> GyroscopeImuModule / StarTrackerModule / ReactionWheelModule / RcsThrusterModule;
        //           ThermalModuleDefinition.Kind -> PassiveInsulation / ActiveRadiator / RadioisotopeHeater;
        //           PowerModuleDefinition.Kind -> Battery / SolarArray / Rtg. Then Initialize(def).
        return null;
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

    public virtual bool CanOperate() =>
        IsPowered && State != ComponentState.Broken && State != ComponentState.Overheated && State != ComponentState.Frozen;

    public virtual void OnFixedTick(float deltaTime) { }
}
