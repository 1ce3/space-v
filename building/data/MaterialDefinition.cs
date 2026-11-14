using Godot;

namespace RocketGame.Building;

/// <summary>
/// Phase 2 (PHASE2_PLAN.md 3.1): hull material chosen per part on tanks, the pod and decouplers.
/// Lives in res://data/materials/, file name = MaterialId. Unlocked by a TechNode listing the MaterialId.
/// </summary>
[GlobalClass]
public partial class MaterialDefinition : Resource
{
    [Export] public string MaterialId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public int Tier { get; set; }
    /// <summary>Added to the part's BuildWeight.</summary>
    [Export] public int BuildWeightCost { get; set; }
    /// <summary>Multiplies DryMass.</summary>
    [Export] public float MassMultiplier { get; set; } = 1f;
    /// <summary>Added to MaxSafeTempK (K).</summary>
    [Export] public float MaxTempBonusK { get; set; }
    /// <summary>0..1, only counts on the command pod hull (solar flares).</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float RadiationShielding { get; set; }
    /// <summary>Multiplies ImpactToleranceMps (ground contact and meteor hits).</summary>
    [Export] public float ImpactToleranceMultiplier { get; set; } = 1f;
    /// <summary>Multiplies MaxShear, MaxBending and joint strength.</summary>
    [Export] public float StrengthMultiplier { get; set; } = 1f;
    /// <summary>Hull colour, so the player sees the material.</summary>
    [Export] public Color Tint { get; set; } = Colors.White;
}
