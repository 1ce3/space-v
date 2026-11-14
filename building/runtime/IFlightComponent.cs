using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Anything with a temperature and a health state: rocket parts and pod modules.</summary>
public interface IFlightComponent
{
    /// <summary>The part or module definition.</summary>
    ComponentDefinition BaseDefinition { get; }
    ComponentState State { get; set; }
    /// <summary>Temperature (K).</summary>
    float TempK { get; set; }
    /// <summary>DryMass * 900 (J/K).</summary>
    float HeatCapacityJPerK { get; }
    /// <summary>TempK += heat / HeatCapacityJPerK, then ThermalStateRules.Evaluate(this). Negative heat cools.</summary>
    void ApplyThermalLoad(float heatJoule);
    /// <summary>False when Broken, Overheated or Frozen (modules: also when unpowered).</summary>
    bool CanOperate();
    /// <summary>Per-physics-tick hook for internal timers.</summary>
    void OnFixedTick(float deltaTime);
}
