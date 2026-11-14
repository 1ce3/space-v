using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>
/// A part of the flying rocket (also used for previews in the build screen). Node3D child of VehicleRoot in flight.
/// Holds runtime state; stats are read-only forwards to Definition.
/// </summary>
public partial class RocketPart : Node3D, IFlightComponent
{
    /// <summary>Data of this part.</summary>
    public PartDefinition Definition { get; private set; }
    /// <summary>Lower-left cell of the footprint.</summary>
    public Vector2I Cell { get; set; }
    /// <summary>Parent in the tree (null for the pod and for decouplers).</summary>
    public RocketPart ParentPart { get; set; }
    public List<RocketPart> Children { get; } = new();
    /// <summary>Joint to the parent. Kept after a cut so listeners can find the parent.</summary>
    public Joint ParentJoint { get; set; }
    /// <summary>Tree this part belongs to (set by RocketAssembler).</summary>
    public PartTree Tree { get; set; }

    public ComponentState State { get; set; } = ComponentState.Idle;
    /// <summary>Temperature (K).</summary>
    public float TempK { get; set; } = 288f;
    /// <summary>True while a fairing or heat shield hides this part from the flow (physics writes it).</summary>
    public bool IsShielded { get; set; }

    public ComponentDefinition BaseDefinition => Definition;
    public float HeatCapacityJPerK => GetEffectiveDryMass() * ThermalStateRules.SpecificHeat;

    // Read-only forwards to Definition.
    public string ModelId => Definition?.ModelId ?? "";
    public PartGroup PartGroup => Definition?.PartGroup ?? PartGroup.Structural;
    public Vector2I Footprint => Definition?.Footprint ?? Vector2I.One;
    /// <summary>Mass without fuel (kg).</summary>
    /// <summary>Dry mass with the hull material applied (kg).</summary>
    public float DryMass => (Definition?.DryMass ?? 0f) * (Material?.MassMultiplier ?? 1f);
    public float FrontalArea => Definition?.FrontalArea ?? 0f;
    public float SideArea => Definition?.SideArea ?? 0f;
    public float DragCoefficient => Definition?.DragCoefficient ?? 0f;
    public float MaxShear => (Definition?.MaxShear ?? 0f) * (Material?.StrengthMultiplier ?? 1f);
    public float MaxBending => (Definition?.MaxBending ?? 0f) * (Material?.StrengthMultiplier ?? 1f);
    public float ImpactToleranceMps => (Definition?.ImpactToleranceMps ?? 0f) * (Material?.ImpactToleranceMultiplier ?? 1f);

    /// <summary>Phase 2: hull material (null for parts that take none).</summary>
    public MaterialDefinition Material { get; set; }

    // A12: DryMass, MaxShear, MaxBending and ImpactToleranceMps above already include the material, so every reader
    // (mass, structure, ground contact) uses it. The GetEffective names stay for code written against the C9 contract.
    public float GetEffectiveDryMass() => DryMass;
    public float GetEffectiveMaxSafeTempK() => (Definition?.MaxSafeTempK ?? 0f) + (Material?.MaxTempBonusK ?? 0f);
    public float GetEffectiveImpactToleranceMps() => ImpactToleranceMps;
    public float GetEffectiveMaxShear() => MaxShear;
    public float GetEffectiveMaxBending() => MaxBending;
    /// <summary>Extra build weight of the material.</summary>
    public int MaterialWeight => Material?.BuildWeightCost ?? 0;
    public float GetRadiationShielding() => Material?.RadiationShielding ?? 0f;
    /// <summary>B9: joint strength this part brings (decoupler, fairing, parachute) with the material, 0 for other parts.</summary>
    public float GetEffectiveAttachmentStrength()
    {
        float baseStrength = Definition switch
        {
            DecouplerPartDefinition d => d.AttachmentStrength,
            FairingPartDefinition f => f.AttachmentStrength,
            ParachutePartDefinition c => c.AttachmentStrength,
            _ => 0f,
        };
        return baseStrength * (Material?.StrengthMultiplier ?? 1f);
    }

    /// <summary>Creates the runtime class that matches the definition type (A_BUILDING.md 4.2).</summary>
    public static RocketPart Create(PartDefinition def)
    {
        RocketPart part = def switch
        {
            DecouplerPartDefinition => new DecouplerPart(),
            StructuralPartDefinition => new StructuralPart(),
            FuelTankPartDefinition => new FuelTankPart(),
            LiquidEnginePartDefinition => new LiquidEnginePart(),
            SolidBoosterPartDefinition => new SolidBoosterPart(),
            AeroSurfacePartDefinition aero => aero.IsControlSurface ? new ControlFinPart() : new AeroSurfacePart(),
            CommandPodPartDefinition => new CommandPodPart(),
            FairingPartDefinition => new FairingPart(),
            HeatShieldPartDefinition => new HeatShieldPart(),
            ParachutePartDefinition => new ParachutePart(),
            _ => null,
        };

        if (part == null)
        {
            GD.PushError($"[Building] RocketPart.Create: unsupported definition '{def?.ModelId ?? "null"}' ({def?.GetType().Name ?? "null"})");
            return null;
        }

        part.Initialize(def);
        return part;
    }

    /// <summary>Sets the definition and resets runtime state (fuel, ignitions, ablator...).</summary>
    public void Initialize(PartDefinition def)
    {
        Definition = def;
        Name = string.IsNullOrEmpty(def?.ModelId) ? "Part" : def.ModelId;
        ResetRuntimeState();
    }

    /// <summary>Subclasses fill their tanks / counters here.</summary>
    protected virtual void ResetRuntimeState()
    {
        State = ComponentState.Idle;
        TempK = 288f;
        IsShielded = false;
    }

    /// <summary>Dry mass plus fuel or propellant (kg).</summary>
    public virtual float GetCurrentMass() => GetEffectiveDryMass();

    /// <summary>True while this part is still attached to its tree.</summary>
    public bool IsAttached => Tree != null && Tree.ActiveParts.Contains(this);

    /// <summary>Kept for the contract; physics computes aero itself. Always zero.</summary>
    public Vector2 CalculateAero(Vector2 airVel, float rho, float mach) => Vector2.Zero;

    /// <summary>Footprint center in cells (vehicle-local grid units).</summary>
    public virtual Vector2 GetGridPosition() => new Vector2(Cell.X + Footprint.X * 0.5f, Cell.Y + Footprint.Y * 0.5f);

    /// <summary>GetGridPosition() * CellSize, in meters (vehicle-local frame). Physics uses this.</summary>
    public Vector2 GetLocalPosition() => GetGridPosition() * GetCellSize();

    public void ApplyThermalLoad(float heatJoule)
    {
        if (HeatCapacityJPerK <= 0f) return;
        TempK = Mathf.Max(0f, TempK + heatJoule / HeatCapacityJPerK);
        ThermalStateRules.Evaluate(this);
    }

    public virtual bool CanOperate() =>
        State != ComponentState.Broken && State != ComponentState.Overheated && State != ComponentState.Frozen;

    public virtual void OnFixedTick(float deltaTime) { }

    /// <summary>CellSize from PartCatalog settings, or the default when no catalog is loaded.</summary>
    public static float GetCellSize() => PartCatalog.Instance?.Settings?.CellSize ?? BuildSettings.DefaultCellSize;
}
