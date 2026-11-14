using Godot;

namespace RocketGame.Building;

/// <summary>Base data of everything the tech tree can unlock (parts and pod modules). Saved as .tres.</summary>
public abstract partial class ComponentDefinition : Resource
{
    /// <summary>Unique id, snake_case, equal to the .tres file name.</summary>
    [ExportGroup("Identity")]
    [Export] public string ModelId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export(PropertyHint.Range, "0,4,1")] public int Tier { get; set; }
    /// <summary>Build budget cost.</summary>
    [Export(PropertyHint.Range, "0,20,1")] public int BuildWeight { get; set; } = 1;
    [Export] public Texture2D Icon { get; set; }

    /// <summary>Mass without fuel or propellant (kg).</summary>
    [ExportGroup("Physical")]
    [Export] public float DryMass { get; set; } = 100f;
    /// <summary>Electric power used while operating (W).</summary>
    [Export] public float PowerDrawW { get; set; }
    /// <summary>Above this the component becomes Overheated (K).</summary>
    [Export] public float MaxSafeTempK { get; set; } = 1200f;
    /// <summary>Below this the component becomes Frozen (K).</summary>
    [Export] public float MinSafeTempK { get; set; } = 200f;

    /// <summary>Multi-line text for the build palette tooltip.</summary>
    public virtual string GetFormattedTooltip()
    {
        return $"{DisplayName}\nWeight: {BuildWeight}\nDry mass: {DryMass:0} kg"
            + (PowerDrawW > 0f ? $"\nPower: {PowerDrawW:0} W" : "");
    }
}
