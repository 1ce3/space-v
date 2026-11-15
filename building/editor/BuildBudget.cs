using Godot;

namespace RocketGame.Building;

/// <summary>Build weight budget (max comes from the tech tree).</summary>
public class BuildBudget
{
    public int MaxWeight { get; private set; }
    public int CurrentWeight { get; private set; }
    public int Remaining => MaxWeight - CurrentWeight;

    public BuildBudget(int maxWeight = 20)
    {
        MaxWeight = maxWeight;
    }

    public bool CanAfford(ComponentDefinition def) => def != null && def.BuildWeight <= Remaining;

    public void Add(ComponentDefinition def)
    {
        if (def != null) CurrentWeight += def.BuildWeight;
    }

    public void Remove(ComponentDefinition def)
    {
        if (def != null) CurrentWeight = Mathf.Max(0, CurrentWeight - def.BuildWeight);
    }

    /// <summary>Adds weight that has no definition (a hull material's BuildWeightCost).</summary>
    public void AddWeight(int weight) => CurrentWeight = Mathf.Max(0, CurrentWeight + weight);

    public void SetMax(int value)
    {
        MaxWeight = Mathf.Max(0, value);
    }

    /// <summary>Back to zero used weight.</summary>
    public void Clear()
    {
        CurrentWeight = 0;
    }
}
