using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Physics;

/// <summary>Cruise segment as a day-by-day resource check on the PartTree left after ascent (guide B 5). Created and owned by CruiseController (C).</summary>
public class CruiseSimulator
{
    /// <summary>Loaded when Settings is not set before Begin.</summary>
    public const string DefaultSettingsPath = "res://data/settings/physics_settings.tres";
    /// <summary>Solar flux used when a body is missing (W/m^2, Earth orbit).</summary>
    public const float FallbackSolarFluxWm2 = 1361f;

    /// <summary>Tuning values. Begin loads DefaultSettingsPath (or code defaults) when null.</summary>
    public PhysicsSettings Settings { get; set; }
    /// <summary>Days simulated so far.</summary>
    public int Day { get; private set; }
    /// <summary>True once ExecuteTransferBurn succeeded.</summary>
    public bool HasTransferBurned { get; private set; }
    /// <summary>Solar flux of the last StepDay (W/m^2).</summary>
    public float CurrentSolarFluxWm2 { get; private set; }
    /// <summary>Seed of the daily hazard dice, to replay a bug.</summary>
    public ulong HazardSeed { get; set; } = 54321;

    private PartTree _parts;
    private MissionSegmentDefinition _segment;
    private CelestialBodyDefinition _origin;
    private CelestialBodyDefinition _destination;
    private PowerSolver _power;
    private ThermalSolver _thermal;
    private readonly List<FlightEvent> _events = new();
    private readonly List<string> _broken = new();
    private readonly List<string> _frozen = new();
    private readonly List<LiquidEnginePart> _engines = new();
    private readonly HashSet<FuelTankPart> _tanks = new();
    private readonly HashSet<FlightEventType> _activeHazards = new();
    private readonly RandomNumberGenerator _rng = new();

    /// <summary>Starts the cruise with the vehicle as it is now.</summary>
    public void Begin(PartTree parts, MissionSegmentDefinition segment, CelestialBodyDefinition origin, CelestialBodyDefinition destination)
    {
        _parts = parts;
        _segment = segment;
        _origin = origin;
        _destination = destination;
        Day = 0;
        HasTransferBurned = false;
        Settings ??= ResourceLoader.Exists(DefaultSettingsPath)
            ? GD.Load<PhysicsSettings>(DefaultSettingsPath)
            : new PhysicsSettings();
        _power = new PowerSolver(Settings);
        _thermal = new ThermalSolver(Settings);
        _activeHazards.Clear();
        _rng.Seed = HazardSeed;
        CurrentSolarFluxWm2 = _origin?.SolarFluxWm2 ?? FallbackSolarFluxWm2;
    }

    /// <summary>
    /// Rocket-equation delta-v (m/s): armed, unbroken liquid engines; Isp = IspVacuum averaged by MaxThrustVac;
    /// fuel = CurrentFuel of the union of their attached FeedTanks; m0 = mass of the whole tree. 0 without an engine.
    /// </summary>
    public float GetRemainingDeltaV()
    {
        float isp = CollectEngines(false);
        if (isp <= 0f) return 0f;
        float fuel = TankFuel();
        float m0 = TreeMass();
        if (fuel <= 0f || m0 <= fuel) return 0f;
        return isp * FlightMath.G0 * Mathf.Log(m0 / (m0 - fuel));
    }

    /// <summary>
    /// Burns the fuel for RequiredDeltaVMps: false when delta-v is short or no engine has an ignition left.
    /// Otherwise drains m0 * (1 - exp(-dv / (Isp g0))) kg from the feed tanks (in proportion to their fuel),
    /// spends one ignition of every engine that fired, and returns true.
    /// </summary>
    public bool ExecuteTransferBurn()
    {
        float required = _segment?.RequiredDeltaVMps ?? 0f;
        if (GetRemainingDeltaV() < required) return false;
        float isp = CollectEngines(true);
        if (isp <= 0f) return false;

        float fuel = TankFuel();
        float burn = TreeMass() * (1f - Mathf.Exp(-required / (isp * FlightMath.G0)));
        if (fuel > 0f)
            foreach (FuelTankPart tank in _tanks)
                tank.DrainFuel(burn * tank.CurrentFuel / fuel);
        foreach (LiquidEnginePart engine in _engines) engine.RemainingIgnitions--;

        HasTransferBurned = true;
        return true;
    }

    /// <summary>Advances one day: solar flux, power, temperature, IMU drift, RTG ageing.</summary>
    public CruiseStepResult StepDay()
    {
        Day++;
        _events.Clear();
        _broken.Clear();
        _frozen.Clear();

        float originFlux = _origin?.SolarFluxWm2 ?? FallbackSolarFluxWm2;
        float destinationFlux = _destination?.SolarFluxWm2 ?? originFlux;
        float duration = Mathf.Max(1f, _segment?.DurationDays ?? 1f);
        CurrentSolarFluxWm2 = Mathf.Lerp(originFlux, destinationFlux, Mathf.Clamp(Day / duration, 0f, 1f));

        PowerStepResult power = _power?.SolveSteadyState(_parts, CurrentSolarFluxWm2) ?? new PowerStepResult();
        ThermalStepResult thermal = _thermal?.SolveSteadyState(_parts, CurrentSolarFluxWm2, power.ConsumedW)
            ?? new ThermalStepResult { LowestTempRatio = 1f };
        if (thermal.BrokenComponents != null) _broken.AddRange(thermal.BrokenComponents);
        if (thermal.FrozenComponents != null) _frozen.AddRange(thermal.FrozenComponents);

        AdvanceModules();
        RollDailyHazards(power, thermal);

        return new CruiseStepResult
        {
            Day = Day,
            DeltaVRemaining = GetRemainingDeltaV(),
            PowerBalanceW = power.SupplyW - power.DemandW,
            BatteryFraction = power.BatteryFraction,
            LowestTempRatio = thermal.LowestTempRatio,
            IsBrownout = power.IsBrownout,
            Events = _events,
            BrokenComponents = _broken,
            FrozenComponents = _frozen,
        };
    }

    /// <summary>Control has handled this hazard; it may fire again.</summary>
    public void Acknowledge(FlightEventType type) => _activeHazards.Remove(type);

    /// <summary>
    /// Daily incidents, only for types the segment enables and each once until Acknowledge (guide B 5 step 5):
    /// PowerBrownout and ComponentOverheat from today's result, DebrisWarning and SeuGlitch by chance per day.
    /// The simulator only reports them; CruiseController (C) decides what the player must do.
    /// </summary>
    private void RollDailyHazards(PowerStepResult power, ThermalStepResult thermal)
    {
        CommandPodPart pod = PowerSolver.FindPod(_parts);
        if (power.IsBrownout) AddHazard(FlightEventType.PowerBrownout, 1f, pod?.ModelId);
        if (thermal.OverheatedComponents is { Count: > 0 }) AddHazard(FlightEventType.ComponentOverheat, 0.5f, thermal.OverheatedComponents[0]);
        if (_rng.Randf() < (Settings?.DebrisChancePerDay ?? 0f)) AddHazard(FlightEventType.DebrisWarning, 1f, "");
        if (_rng.Randf() < (Settings?.SeuChancePerDay ?? 0f)) AddHazard(FlightEventType.SeuGlitch, 0.5f, RandomAvionicsId(pod));
    }

    private void AddHazard(FlightEventType type, float severity, string sourceId)
    {
        FlightEventMask enabled = _segment?.EnabledHazards ?? FlightEventMask.None;
        if ((enabled & HazardMonitor.ToMask(type)) == 0 || _activeHazards.Contains(type)) return;
        _activeHazards.Add(type);
        _events.Add(new FlightEvent { Type = type, Severity = severity, SourceId = sourceId ?? "" });
    }

    private string RandomAvionicsId(CommandPodPart pod)
    {
        if (pod == null) return "";
        var avionics = pod.Modules.FindAll(m => m.Definition?.SlotType == ModuleSlotType.Avionics);
        return avionics.Count > 0 ? avionics[_rng.RandiRange(0, avionics.Count - 1)].ModelId : pod.ModelId;
    }

    /// <summary>IMU drift (reset by a working star tracker) and RTG ageing.</summary>
    private void AdvanceModules()
    {
        CommandPodPart pod = PowerSolver.FindPod(_parts);
        if (pod == null) return;

        float driftMinutes = Settings?.CruiseDriftMinutesPerDay ?? 1f;
        bool starFix = false;
        foreach (StarTrackerModule tracker in pod.GetModules<StarTrackerModule>())
            starFix |= tracker.CanOperate();
        foreach (GyroscopeImuModule imu in pod.GetModules<GyroscopeImuModule>())
        {
            if (starFix) imu.CalibrateWithStarTracker();
            else imu.AccumulatedDriftDeg += imu.DriftRateDegPerMin * driftMinutes;
        }
        foreach (RtgModule rtg in pod.GetModules<RtgModule>()) rtg.MissionElapsedYears += 1f / 365f;
    }

    /// <summary>Fills _engines and _tanks; returns the thrust-weighted vacuum Isp (0 when none).</summary>
    private float CollectEngines(bool needIgnition)
    {
        _engines.Clear();
        _tanks.Clear();
        if (_parts == null) return 0f;

        float thrust = 0f, weighted = 0f;
        foreach (LiquidEnginePart engine in _parts.GetParts<LiquidEnginePart>())
        {
            if (!engine.IsArmed || engine.State == ComponentState.Broken || !engine.IsAttached) continue;
            if (needIgnition && engine.RemainingIgnitions <= 0) continue;
            LiquidEnginePartDefinition def = engine.LiquidDefinition;
            if (def == null || def.MaxThrustVac <= 0f) continue;

            _engines.Add(engine);
            thrust += def.MaxThrustVac;
            weighted += def.IspVacuum * def.MaxThrustVac;
            foreach (FuelTankPart tank in engine.FeedTanks)
                if (tank.IsAttached) _tanks.Add(tank);
        }
        return thrust > 0f ? weighted / thrust : 0f;
    }

    private float TankFuel()
    {
        float fuel = 0f;
        foreach (FuelTankPart tank in _tanks) fuel += tank.CurrentFuel;
        return fuel;
    }

    private float TreeMass()
    {
        float mass = 0f;
        if (_parts == null) return mass;
        foreach (RocketPart part in _parts.ActiveParts) mass += part.GetCurrentMass();
        return mass;
    }
}
