using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Physics;

/// <summary>
/// Rolls and detects in-flight incidents (guide B 4.6, P1). Only types in EnabledHazards fire, each once until
/// Control calls Acknowledge. Physical consequences done here: a cryo leak sets LeakRateKgPerSec on a tank
/// (ApplyLeaks drains it every tick); a debris warning that is not dodged within DebrisWarningSeconds
/// (horizontal speed changed by less than DebrisDodgeSpeedMps) sets DebrisHit, and the coordinator breaks the pod (Collision).
/// </summary>
public class HazardMonitor
{
    /// <summary>Peak wind shear force (N) of the current segment (P2).</summary>
    public float WindShearForceX { get; set; }
    /// <summary>Stormy weather in the current segment (P2).</summary>
    public bool IsStormy { get; set; }
    /// <summary>Hazard types allowed in the current segment.</summary>
    public FlightEventMask EnabledHazards { get; set; }
    /// <summary>Seconds left to dodge the current debris warning; 0 when none.</summary>
    public float DebrisSecondsLeft { get; private set; }

    private readonly PhysicsSettings _settings;
    private readonly RandomNumberGenerator _rng = new();
    private readonly List<FlightEvent> _events = new();
    private readonly HashSet<FlightEventType> _active = new();
    private readonly List<FuelTankPart> _cryoTanks = new();
    private float _debrisReferenceSpeed;
    private bool _debrisHit;

    public HazardMonitor(PhysicsSettings settings, ulong seed)
    {
        _settings = settings;
        _rng.Seed = seed;
    }

    /// <summary>Reads the hazard setup of a segment and clears active hazards.</summary>
    public void Configure(MissionSegmentDefinition segment)
    {
        EnabledHazards = segment?.EnabledHazards ?? FlightEventMask.None;
        IsStormy = segment?.IsStormy ?? false;
        WindShearForceX = segment?.WindShearForceX ?? 0f;
        _active.Clear();
        DebrisSecondsLeft = 0f;
        _debrisHit = false;
    }

    /// <summary>
    /// Events for this tick, from the finished output (q, G, thermal, power) plus the solver events
    /// (EngineFlameout from PropulsionSolver), which pass through the same enable / once-until-acknowledged filter.
    /// The returned list is reused.
    /// </summary>
    public List<FlightEvent> Evaluate(PhysicsStepInput input, PhysicsStepOutput output,
        IReadOnlyList<FlightEvent> solverEvents = null, PowerStepResult? power = null)
    {
        _events.Clear();
        if (solverEvents != null)
            foreach (FlightEvent e in solverEvents) Admit(e.Type, e.Severity, e.SourceId);

        float maxQ = _settings?.MaxQLimit ?? 40000f;
        if (maxQ > 0f && output.DynamicPressure > maxQ)
            Admit(FlightEventType.MaxQExceeded, Mathf.Clamp(output.DynamicPressure / maxQ - 1f, 0f, 1f), "");

        CommandPodPart pod = PowerSolver.FindPod(input.Parts);
        float maxG = pod?.PodDefinition?.MaxGForce ?? 0f;
        if (maxG > 0f && output.CurrentGForce > maxG)
            Admit(FlightEventType.GLimitExceeded, Mathf.Clamp(output.CurrentGForce / maxG - 1f, 0f, 1f), pod.ModelId);

        List<string> overheated = output.Thermal.OverheatedComponents;
        if (overheated != null && overheated.Count > 0)
            Admit(FlightEventType.ComponentOverheat, 0.5f, overheated[0]);

        if (power.HasValue && power.Value.IsBrownout)
            Admit(FlightEventType.PowerBrownout, 1f, pod?.ModelId ?? "");

        if (IsEnabled(FlightEventType.CryoLeak) && !_active.Contains(FlightEventType.CryoLeak) && CheckCryoLeak(input.Parts, input.DeltaTime))
        {
            FuelTankPart tank = _cryoTanks[_rng.RandiRange(0, _cryoTanks.Count - 1)];
            tank.LeakRateKgPerSec = _settings?.CryoLeakRateKgPerSec ?? 3f;
            Admit(FlightEventType.CryoLeak, 0.7f, tank.ModelId);
        }

        UpdateDebris(input, output);
        return _events;
    }

    /// <summary>Control has handled this hazard; it may fire again.</summary>
    public void Acknowledge(FlightEventType type) => _active.Remove(type);

    /// <summary>True when the hazard is waiting for Acknowledge.</summary>
    public bool IsActive(FlightEventType type) => _active.Contains(type);

    /// <summary>Drains every leaking tank for dt; returns the fuel lost (kg). Called every tick by the coordinator.</summary>
    public float ApplyLeaks(PartTree parts, float deltaTime)
    {
        if (parts == null || deltaTime <= 0f) return 0f;
        float lost = 0f;
        foreach (FuelTankPart tank in parts.GetParts<FuelTankPart>())
        {
            if (tank.LeakRateKgPerSec <= 0f) continue;
            lost += tank.DrainFuel(tank.LeakRateKgPerSec * deltaTime);
            if (tank.CurrentFuel <= 0f) tank.LeakRateKgPerSec = 0f;
        }
        return lost;
    }

    /// <summary>True once when an undodged debris warning runs out; the coordinator then breaks the pod.</summary>
    public bool ConsumeDebrisHit()
    {
        bool hit = _debrisHit;
        _debrisHit = false;
        return hit;
    }

    /// <summary>Lightning roll at this altitude (P2).</summary>
    public bool CheckLightningStrike(float altitudeMeters) => false;

    /// <summary>True when the rocket has not dodged the debris (P1): the warning ran out.</summary>
    public bool CheckDebrisWarning(Vector2 rocketPos, Vector2 nearestDebrisPos) => DebrisSecondsLeft <= 0f && _debrisHit;

    /// <summary>Cryo leak roll for the attached, non-empty, not yet leaking Hydrolox / Methalox tanks in parts (P1).</summary>
    public bool CheckCryoLeak(PartTree parts, float deltaTime = 1f)
    {
        _cryoTanks.Clear();
        if (parts == null) return false;
        foreach (FuelTankPart tank in parts.GetParts<FuelTankPart>())
        {
            FuelType type = tank.TankDefinition?.PropellantType ?? FuelType.Kerolox;
            if ((type == FuelType.Hydrolox || type == FuelType.Methalox) && tank.CurrentFuel > 0f && tank.LeakRateKgPerSec <= 0f)
                _cryoTanks.Add(tank);
        }
        return _cryoTanks.Count > 0 && Roll(_settings?.CryoLeakChancePerSec ?? 0f, deltaTime);
    }

    /// <summary>Single-event upset roll at this altitude (P2).</summary>
    public bool CheckSeuGlitch(float altitudeMeters) => false;

    /// <summary>Starts, follows and resolves a debris warning.</summary>
    private void UpdateDebris(PhysicsStepInput input, PhysicsStepOutput output)
    {
        float horizontal = HorizontalSpeed(input);
        if (DebrisSecondsLeft > 0f)
        {
            if (Mathf.Abs(horizontal - _debrisReferenceSpeed) > (_settings?.DebrisDodgeSpeedMps ?? 5f))
            {
                DebrisSecondsLeft = 0f; // Dodged.
                return;
            }
            DebrisSecondsLeft -= input.DeltaTime;
            if (DebrisSecondsLeft <= 0f)
            {
                DebrisSecondsLeft = 0f;
                _debrisHit = true;
            }
            return;
        }

        if (!IsEnabled(FlightEventType.DebrisWarning) || _active.Contains(FlightEventType.DebrisWarning)) return;
        if (output.AltitudeMeters < (_settings?.DebrisMinAltitudeM ?? 80000f)) return;
        if (!Roll(_settings?.DebrisChancePerSec ?? 0f, input.DeltaTime)) return;

        _debrisReferenceSpeed = horizontal;
        DebrisSecondsLeft = Mathf.Max(0.1f, _settings?.DebrisWarningSeconds ?? 8f);
        Admit(FlightEventType.DebrisWarning, 1f, "");
    }

    /// <summary>Speed across the local vertical (m/s).</summary>
    private static float HorizontalSpeed(PhysicsStepInput input)
    {
        Vector2 up = input.Position.LengthSquared() > 1f ? input.Position.Normalized() : new Vector2(0f, 1f);
        Vector2 v = input.LinearVelocity;
        return (v - up * v.Dot(up)).Length();
    }

    private bool IsEnabled(FlightEventType type) => (EnabledHazards & ToMask(type)) != 0;

    private void Admit(FlightEventType type, float severity, string sourceId)
    {
        if (!IsEnabled(type) || _active.Contains(type)) return;
        _active.Add(type);
        _events.Add(new FlightEvent { Type = type, Severity = severity, SourceId = sourceId ?? "" });
    }

    /// <summary>Chance per second turned into a chance for this step.</summary>
    private bool Roll(float chancePerSec, float deltaTime)
    {
        if (chancePerSec <= 0f || deltaTime <= 0f) return false;
        float p = chancePerSec >= 1f ? 1f : 1f - Mathf.Pow(1f - chancePerSec, deltaTime);
        return _rng.Randf() < p;
    }

    /// <summary>FlightEventMask bit of a type (bit i = enum value i).</summary>
    public static FlightEventMask ToMask(FlightEventType type) => (FlightEventMask)(1 << (int)type);
}
