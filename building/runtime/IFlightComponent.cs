using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Runtime state shared by grid parts (RocketPart) and pod modules (PodModule).</summary>
public interface IFlightComponent
{
    /// <summary>The definition this component was created from.</summary>
    ComponentDefinition BaseDefinition { get; }

    /// <summary>Operating state. Written by ThermalStateRules and by Physics.</summary>
    ComponentState State { get; set; }

    /// <summary>Current temperature, in K.</summary>
    float TempK { get; set; }

    /// <summary>Heat needed to raise the temperature by 1 K, in J/K (dry mass times 900).</summary>
    float HeatCapacityJPerK { get; }

    /// <summary>Adds heat (negative removes it), updates TempK, then re-evaluates State. Heat in J.</summary>
    void ApplyThermalLoad(float heatJoule);

    /// <summary>True when the component can do its job (not Broken, Overheated or Frozen).</summary>
    bool CanOperate();

    /// <summary>Called once per physics tick by the owner of the component. Time in s.</summary>
    void OnFixedTick(float deltaTime);
}
