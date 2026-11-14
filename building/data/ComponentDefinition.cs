using Godot;

namespace RocketGame.Building;

/// <summary>Abstract base of every part and module definition. Pure data, stored as .tres under data/parts or data/modules.</summary>
[GlobalClass]
public abstract partial class ComponentDefinition : Resource
{
    /// <summary>Unique snake_case id. Must match the .tres file name (for example engine_lv2).</summary>
    [Export] public string ModelId { get; set; } = "";

    /// <summary>Name shown to the player.</summary>
    [Export] public string DisplayName { get; set; } = "";

    /// <summary>Tech tier (1 = starter).</summary>
    [Export] public int Tier { get; set; } = 1;

    /// <summary>Cost against the build weight budget (dimensionless points).</summary>
    [Export] public int BuildWeight { get; set; } = 1;

    /// <summary>Mass without fuel or propellant, in kg.</summary>
    [Export] public float DryMass { get; set; }

    /// <summary>Electrical power drawn while operating, in W.</summary>
    [Export] public float PowerDrawW { get; set; }

    /// <summary>Above this temperature the component becomes Overheated.</summary>
    [Export] public float MaxSafeTempK { get; set; } = 1200f;

    /// <summary>Below this temperature the component becomes Frozen.</summary>
    [Export] public float MinSafeTempK { get; set; } = 200f;

    /// <summary>Icon for the build screen palette.</summary>
    [Export] public Texture2D Icon { get; set; }

    /// <summary>Multi-line text for the part tooltip. Derived classes append their own stats.</summary>
    public virtual string GetFormattedTooltip()
        => $"{DisplayName}\nWeight: {BuildWeight} | Dry mass: {DryMass:0} kg";
}
