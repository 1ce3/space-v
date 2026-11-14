using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Base class of every part placed on the grid. Created through RocketPart.Create so the right subclass is chosen.</summary>
public abstract partial class RocketPart : Node3D, IFlightComponent
{
    private const float FallbackCellSizeM = 2f;
    private const float HeatCapacityPerKg = 900f;
    private const float StartTempK = 288f;

    /// <summary>Definition this part was created from. Set by Create.</summary>
    public PartDefinition Definition { get; private set; }

    /// <summary>Lower-left grid cell of the footprint (Y increases upward).</summary>
    public Vector2I Cell { get; set; }

    /// <summary>Parent in the part tree. Null for the command pod. Kept after the part is severed.</summary>
    public RocketPart ParentPart { get; set; }

    /// <summary>Child parts in the part tree.</summary>
    public List<RocketPart> Children { get; } = new();

    /// <summary>Joint to the parent. Kept after the part is severed so listeners can see the old parent.</summary>
    public Joint ParentJoint { get; set; }

    /// <summary>Operating state.</summary>
    public ComponentState State { get; set; } = ComponentState.Idle;

    /// <summary>Current temperature, in K.</summary>
    public float TempK { get; set; } = StartTempK;

    /// <summary>True while a fairing or heat shield protects this part from the airflow.</summary>
    public bool IsShielded { get; set; }

    /// <summary>The part tree this part belongs to. Set by the assembler.</summary>
    public PartTree Tree { get; set; }

    /// <summary>The definition as the base type, for IFlightComponent.</summary>
    public ComponentDefinition BaseDefinition => Definition;

    /// <summary>Dry mass, in kg.</summary>
    public float DryMass => Definition?.DryMass ?? 0f;

    /// <summary>Footprint size, in cells.</summary>
    public Vector2I Footprint => Definition?.Footprint ?? Vector2I.One;

    /// <summary>Part category.</summary>
    public PartGroup PartGroup => Definition?.PartGroup ?? PartGroup.Structural;

    /// <summary>Cross-section facing the airflow, in m^2.</summary>
    public float FrontalArea => Definition?.FrontalArea ?? 0f;

    /// <summary>Area facing a sideways airflow, in m^2.</summary>
    public float SideArea => Definition?.SideArea ?? 0f;

    /// <summary>Drag coefficient.</summary>
    public float DragCoefficient => Definition?.DragCoefficient ?? 0f;

    /// <summary>Largest shear force the part survives, in N.</summary>
    public float MaxShear => Definition?.MaxShear ?? 0f;

    /// <summary>Largest bending moment the part survives, in N*m.</summary>
    public float MaxBending => Definition?.MaxBending ?? 0f;

    /// <summary>Largest impact speed the part survives, in m/s.</summary>
    public float ImpactToleranceMps => Definition?.ImpactToleranceMps ?? 0f;

    /// <summary>Heat needed to raise the temperature by 1 K, in J/K.</summary>
    public float HeatCapacityJPerK => DryMass * HeatCapacityPerKg;

    /// <summary>Builds the right runtime class for a definition. Returns null (and logs an error) for unknown types.</summary>
    public static RocketPart Create(PartDefinition def)
    {
        if (def == null)
        {
            GD.PushError("[RocketPart] Create was called with a null definition.");
            return null;
        }

        RocketPart part = def switch
        {
            DecouplerPartDefinition => new DecouplerPart(),
            StructuralPartDefinition => new StructuralPart(),
            FuelTankPartDefinition => new FuelTankPart(),
            LiquidEnginePartDefinition => new LiquidEnginePart(),
            SolidBoosterPartDefinition => new SolidBoosterPart(),
            AeroSurfacePartDefinition aero => aero.MaxDeflectionDeg > 0f ? new ControlFinPart() : new AeroSurfacePart(),
            CommandPodPartDefinition => new CommandPodPart(),
            FairingPartDefinition => new FairingPart(),
            HeatShieldPartDefinition => new HeatShieldPart(),
            ParachutePartDefinition => new ParachutePart(),
            _ => null,
        };

        if (part == null)
        {
            GD.PushError($"[RocketPart] No runtime class for definition type {def.GetType().Name} ('{def.ModelId}').");
            return null;
        }

        part.Definition = def;
        part.InitializeFromDefinition();
        return part;
    }

    /// <summary>Current mass: dry mass plus any fuel or propellant carried, in kg.</summary>
    public virtual float GetCurrentMass() => DryMass;

    /// <summary>Aerodynamic force on the part. Stub: Physics computes aerodynamics itself.</summary>
    public virtual Vector2 CalculateAero(Vector2 airVel, float rho, float mach) => Vector2.Zero;

    /// <summary>Center of the footprint, in grid cells (a 1x2 part at cell (2,3) gives (2.5, 4.0)).</summary>
    public virtual Vector2 GetGridPosition()
        => new(Cell.X + Footprint.X * 0.5f, Cell.Y + Footprint.Y * 0.5f);

    /// <summary>Position in the vehicle-local frame, in meters (grid position times cell size).</summary>
    public Vector2 GetLocalPosition() => GetGridPosition() * GetCellSizeM();

    /// <summary>Adds heat (negative removes it) and re-evaluates the thermal state. Heat in J.</summary>
    public void ApplyThermalLoad(float heatJoule)
    {
        float capacity = HeatCapacityJPerK;
        if (capacity <= 0f) return;
        TempK += heatJoule / capacity;
        ThermalStateRules.Evaluate(this);
    }

    /// <summary>True unless the part is Broken, Overheated or Frozen.</summary>
    public bool CanOperate()
        => State != ComponentState.Broken && State != ComponentState.Overheated && State != ComponentState.Frozen;

    /// <summary>Per-tick hook. Parts hold state only, so the default does nothing.</summary>
    public virtual void OnFixedTick(float deltaTime) { }

    /// <summary>Called once by Create after Definition is set. Subclasses load their starting state here.</summary>
    protected virtual void InitializeFromDefinition() { }

    private static float GetCellSizeM() => PartCatalog.Instance?.Settings?.CellSize ?? FallbackCellSizeM;
}
