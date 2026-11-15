using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Physics;

/// <summary>
/// Phase 2 (PHASE2_PLAN.md 5.2-5.4, owner B, task B11): plans 2..4 cruise events (solar flare, meteor field,
/// instrument failure) on random days at least CruiseEventMinGapDays apart, and applies flare and failure damage.
/// Owned by CruiseSimulator. Only types enabled in the segment's EnabledHazards are planned.
/// A radiation sensor that works on the warning day announces a flare FlareWarningDays early (Severity 0);
/// the flare itself has Severity 1. MeteorField is only reported here: the dodge is C11, the impact B12.
/// </summary>
public class CruiseEventDirector
{
    /// <summary>Event types this director can plan.</summary>
    public const FlightEventMask EventMask = FlightEventMask.SolarFlare | FlightEventMask.MeteorField | FlightEventMask.InstrumentFailure;

    /// <summary>One planned event.</summary>
    public readonly record struct PlannedEvent(int Day, FlightEventType Type);

    /// <summary>Events of this cruise, sorted by day.</summary>
    public IReadOnlyList<PlannedEvent> Planned => _planned;

    private readonly PhysicsSettings _settings;
    private readonly RandomNumberGenerator _rng = new();
    private readonly List<PlannedEvent> _planned = new();
    private readonly HashSet<int> _warned = new();

    public CruiseEventDirector(PhysicsSettings settings, ulong seed = 0)
    {
        _settings = settings ?? new PhysicsSettings();
        if (seed != 0) _rng.Seed = seed;
    }

    /// <summary>Picks the event days for this cruise (CruiseEventsMin..Max events, CruiseEventMinGapDays apart).</summary>
    public void Plan(PartTree parts, MissionSegmentDefinition segment)
    {
        _planned.Clear();
        _warned.Clear();
        if (segment == null) return;

        var types = new List<(FlightEventType Type, float Weight)>();
        AddType(types, segment.EnabledHazards, FlightEventType.SolarFlare, _settings.SolarFlareWeight);
        AddType(types, segment.EnabledHazards, FlightEventType.MeteorField, _settings.MeteorFieldWeight);
        AddType(types, segment.EnabledHazards, FlightEventType.InstrumentFailure, _settings.InstrumentFailureWeight);
        if (types.Count == 0) return;

        int first = Mathf.Max(1, _settings.CruiseEventFirstDay);
        int last = Mathf.FloorToInt(segment.DurationDays) - Mathf.Max(0, _settings.CruiseEventLastDayMargin);
        if (last < first) return;

        int min = Mathf.Max(0, _settings.CruiseEventsMin);
        int max = Mathf.Max(min, _settings.CruiseEventsMax);
        int count = _rng.RandiRange(min, max);
        int gap = Mathf.Max(0, _settings.CruiseEventMinGapDays);

        var days = new List<int>();
        for (int attempt = 0; attempt < 1000 && days.Count < count; attempt++)
        {
            int day = _rng.RandiRange(first, last);
            if (days.TrueForAll(d => Mathf.Abs(d - day) >= gap)) days.Add(day);
        }
        days.Sort();
        foreach (int day in days) _planned.Add(new PlannedEvent(day, PickType(types)));
    }

    /// <summary>
    /// Events of this day: flare warnings (Severity 0, SourceId = the sensor) and the planned events (Severity 1).
    /// Damage is applied by the caller (ApplySolarFlare, ApplyInstrumentFailure).
    /// </summary>
    public List<FlightEvent> EventsForDay(int day, PartTree parts)
    {
        var events = new List<FlightEvent>();
        InstrumentModule sensor = BestRadiationSensor(PowerSolver.FindPod(parts));
        int warnDays = sensor != null ? Mathf.CeilToInt(sensor.InstrumentDefinition.FlareWarningDays) : 0;

        for (int i = 0; i < _planned.Count; i++)
        {
            PlannedEvent e = _planned[i];
            if (e.Type == FlightEventType.SolarFlare && warnDays > 0 && !_warned.Contains(i)
                && day < e.Day && day >= e.Day - warnDays)
            {
                _warned.Add(i);
                events.Add(new FlightEvent { Type = FlightEventType.SolarFlare, Severity = 0f, SourceId = sensor.ModelId });
            }
            if (e.Day == day) events.Add(new FlightEvent { Type = e.Type, Severity = 1f, SourceId = "" });
        }
        return events;
    }

    /// <summary>
    /// Solar flare (5.3): every working module of the pod breaks with
    /// p = FlareBaseDamageChance * (1 - pod hull RadiationShielding) * (1 - module RadiationResistance),
    /// times FlarePoweredDownFactor for modules in poweredDown. Returns the ModelIds that broke.
    /// </summary>
    public List<string> ApplySolarFlare(PartTree parts, ICollection<PodModule> poweredDown)
    {
        var broken = new List<string>();
        CommandPodPart pod = PowerSolver.FindPod(parts);
        if (pod == null) return broken;

        float shielding = Mathf.Clamp(pod.GetRadiationShielding(), 0f, 1f);
        foreach (PodModule module in pod.Modules)
        {
            if (module.State == ComponentState.Broken) continue;
            float chance = FlareDamageChance(shielding, module.Definition?.RadiationResistance ?? 0f,
                poweredDown != null && poweredDown.Contains(module));
            if (_rng.Randf() >= chance) continue;
            Break(module);
            broken.Add(module.ModelId);
        }
        return broken;
    }

    /// <summary>Damage chance of one module in a flare (5.3), for the HUD and tests.</summary>
    public float FlareDamageChance(float podShielding, float radiationResistance, bool poweredDown)
    {
        float chance = _settings.FlareBaseDamageChance
                       * (1f - Mathf.Clamp(podShielding, 0f, 1f))
                       * (1f - Mathf.Clamp(radiationResistance, 0f, 1f));
        if (poweredDown) chance *= _settings.FlarePoweredDownFactor;
        return Mathf.Clamp(chance, 0f, 1f);
    }

    /// <summary>Instrument failure (5.4): one random working module breaks. Returns its ModelId, or "" when none is left.</summary>
    public string ApplyInstrumentFailure(PartTree parts)
    {
        CommandPodPart pod = PowerSolver.FindPod(parts);
        if (pod == null) return "";
        var working = pod.Modules.FindAll(m => m.State != ComponentState.Broken);
        if (working.Count == 0) return "";
        PodModule module = working[_rng.RandiRange(0, working.Count - 1)];
        Break(module);
        return module.ModelId;
    }

    /// <summary>Working radiation sensor with the longest warning, or null.</summary>
    public static InstrumentModule BestRadiationSensor(CommandPodPart pod)
    {
        if (pod == null) return null;
        InstrumentModule best = null;
        foreach (InstrumentModule sensor in pod.GetModules<InstrumentModule>())
        {
            InstrumentModuleDefinition def = sensor.InstrumentDefinition;
            if (def == null || def.Kind != InstrumentKind.RadiationSensor || !sensor.CanOperate()) continue;
            if (best == null || def.FlareWarningDays > best.InstrumentDefinition.FlareWarningDays) best = sensor;
        }
        return best;
    }

    private static void Break(PodModule module)
    {
        module.State = ComponentState.Broken;
        module.IsPowered = false;
    }

    private static void AddType(List<(FlightEventType, float)> types, FlightEventMask enabled, FlightEventType type, float weight)
    {
        if (weight > 0f && (enabled & HazardMonitor.ToMask(type)) != 0) types.Add((type, weight));
    }

    private FlightEventType PickType(List<(FlightEventType Type, float Weight)> types)
    {
        float total = 0f;
        foreach (var t in types) total += t.Weight;
        float roll = _rng.Randf() * total;
        foreach (var t in types)
        {
            if (roll < t.Weight) return t.Type;
            roll -= t.Weight;
        }
        return types[^1].Type;
    }
}
