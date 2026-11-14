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
    private CruiseEventDirector _director;
    private readonly List<string> _damaged = new();
    private readonly List<FlightEvent> _pendingEvents = new();
    private readonly List<FlightEvent> _burnEvents = new();
    private readonly HashSet<PodModule> _poweredDown = new();
    private FuelTankPart _leakingTank;

    /// <summary>Cruise events planned at Begin (B11), for the HUD and tests.</summary>
    public IReadOnlyList<CruiseEventDirector.PlannedEvent> PlannedEvents =>
        _director?.Planned ?? (IReadOnlyList<CruiseEventDirector.PlannedEvent>)System.Array.Empty<CruiseEventDirector.PlannedEvent>();
    /// <summary>Incidents of the last ExecuteTransferBurn (EngineFlameout or CryoLeak); also reported by the next StepDay.</summary>
    public IReadOnlyList<FlightEvent> LastBurnEvents => _burnEvents;
    /// <summary>A tank leaks fuel every day until SealLeak.</summary>
    public bool HasLeak => _leakingTank != null && _leakingTank.IsAttached;
    /// <summary>True while modules are powered down for a solar flare (PowerDownForFlare).</summary>
    public bool IsPoweredDownForFlare => _poweredDown.Count > 0;

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
        _damaged.Clear();
        _pendingEvents.Clear();
        _burnEvents.Clear();
        _poweredDown.Clear();
        _leakingTank = null;
        _director = new CruiseEventDirector(Settings, HazardSeed + 1);
        _director.Plan(parts, segment);
    }

    /// <summary>Player sealed the tank leak (key V).</summary>
    public void SealLeak() => _leakingTank = null;

    /// <summary>
    /// After a flare warning the player may power the pod modules down (except power sources): they then take
    /// FlarePoweredDownFactor of the flare damage chance. Power comes back by itself after the flare, or with false.
    /// </summary>
    public void PowerDownForFlare(bool powerDown)
    {
        CommandPodPart pod = PowerSolver.FindPod(_parts);
        if (powerDown)
        {
            if (pod == null) return;
            foreach (PodModule module in pod.Modules)
            {
                if (module.Definition is PowerModuleDefinition || !module.IsPowered) continue;
                module.IsPowered = false;
                _poweredDown.Add(module);
            }
            return;
        }
        foreach (PodModule module in _poweredDown)
            if (module.State != ComponentState.Broken) module.IsPowered = true;
        _poweredDown.Clear();
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
        RollTransferBurnEvent(required, isp);
        return true;
    }

    /// <summary>
    /// 5.5: with TransferBurnEventChance, a flameout (relight costs TransferRelightDeltaVFraction of the required
    /// delta-v more) or a leak on a random tank with fuel (drains CruiseLeakFractionPerDay a day until SealLeak).
    /// Only types the segment enables.
    /// </summary>
    private void RollTransferBurnEvent(float required, float isp)
    {
        _burnEvents.Clear();
        FlightEventMask enabled = _segment?.EnabledHazards ?? FlightEventMask.None;
        bool flameout = (enabled & FlightEventMask.EngineFlameout) != 0 && _engines.Count > 0;
        var tanks = new List<FuelTankPart>();
        if ((enabled & FlightEventMask.CryoLeak) != 0)
            foreach (FuelTankPart tank in _tanks) if (tank.IsAttached && tank.CurrentFuel > 0f) tanks.Add(tank);
        if (!flameout && tanks.Count == 0) return;
        if (_rng.Randf() >= (Settings?.TransferBurnEventChance ?? 0f)) return;

        bool doFlameout = flameout && (tanks.Count == 0 || _rng.Randf() < 0.5f);
        if (doFlameout)
        {
            LiquidEnginePart engine = _engines[_rng.RandiRange(0, _engines.Count - 1)];
            float extraDv = required * (Settings?.TransferRelightDeltaVFraction ?? 0f);
            float fuel = TankFuel();
            float burn = Mathf.Min(fuel, TreeMass() * (1f - Mathf.Exp(-extraDv / (isp * FlightMath.G0))));
            if (fuel > 0f)
                foreach (FuelTankPart tank in _tanks)
                    tank.DrainFuel(burn * tank.CurrentFuel / fuel);
            _burnEvents.Add(new FlightEvent { Type = FlightEventType.EngineFlameout, Severity = 0.5f, SourceId = engine.ModelId });
        }
        else
        {
            _leakingTank = tanks[_rng.RandiRange(0, tanks.Count - 1)];
            _burnEvents.Add(new FlightEvent { Type = FlightEventType.CryoLeak, Severity = 0.5f, SourceId = _leakingTank.ModelId });
        }
        _pendingEvents.AddRange(_burnEvents);
    }

    /// <summary>Advances one day: solar flux, power, temperature, IMU drift, RTG ageing.</summary>
    public CruiseStepResult StepDay()
    {
        Day++;
        _events.Clear();
        _broken.Clear();
        _frozen.Clear();
        _damaged.Clear();
        _events.AddRange(_pendingEvents);
        _pendingEvents.Clear();

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
        ApplyPlannedEvents();
        if (HasLeak) _leakingTank.DrainFuel(_leakingTank.CurrentFuel * (Settings?.CruiseLeakFractionPerDay ?? 0f));

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
            DamagedModules = _damaged,
        };
    }

    /// <summary>B11: planned events of today; applies flare and instrument-failure damage.</summary>
    private void ApplyPlannedEvents()
    {
        if (_director == null) return;
        foreach (FlightEvent planned in _director.EventsForDay(Day, _parts))
        {
            FlightEvent e = planned;
            if (e.Type == FlightEventType.SolarFlare && e.Severity >= 1f)
            {
                List<string> broken = _director.ApplySolarFlare(_parts, _poweredDown);
                _damaged.AddRange(broken);
                e.SourceId = string.Join(",", broken);
                PowerDownForFlare(false);
            }
            else if (e.Type == FlightEventType.InstrumentFailure)
            {
                e.SourceId = _director.ApplyInstrumentFailure(_parts);
                if (e.SourceId.Length > 0) _damaged.Add(e.SourceId);
            }
            _events.Add(e);
        }
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
