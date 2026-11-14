using System.Collections.Generic;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Physics;

/// <summary>Cruise segment as a day-by-day resource check on the PartTree left after ascent (guide B 5). Created and owned by CruiseController (C).</summary>
public class CruiseSimulator
{
    /// <summary>Tuning values. Uses defaults when null.</summary>
    public PhysicsSettings Settings { get; set; }
    /// <summary>Days simulated so far.</summary>
    public int Day { get; private set; }

    private PartTree _parts;
    private MissionSegmentDefinition _segment;
    private CelestialBodyDefinition _origin;
    private CelestialBodyDefinition _destination;
    private readonly List<FlightEvent> _events = new();
    private readonly List<string> _broken = new();

    /// <summary>Starts the cruise with the vehicle as it is now.</summary>
    public void Begin(PartTree parts, MissionSegmentDefinition segment, CelestialBodyDefinition origin, CelestialBodyDefinition destination)
    {
        _parts = parts;
        _segment = segment;
        _origin = origin;
        _destination = destination;
        Day = 0;
    }

    /// <summary>Rocket-equation delta-v of the armed liquid engines and their feed tanks (m/s).</summary>
    public float GetRemainingDeltaV()
    {
        // TODO(B5): Isp-weighted average, fuel of FeedTanks, Isp * g0 * ln(m0 / (m0 - fuel)).
        return 0f;
    }

    /// <summary>Burns the fuel for RequiredDeltaVMps. False when delta-v or ignitions are not enough.</summary>
    public bool ExecuteTransferBurn()
    {
        // TODO(B5): drain m0 * (1 - exp(-dv / (Isp g0))), spend one ignition per engine.
        return false;
    }

    /// <summary>Advances one day: solar flux, power, temperature, IMU drift, RTG ageing.</summary>
    public CruiseStepResult StepDay()
    {
        Day++;
        _events.Clear();
        _broken.Clear();
        // TODO(B5): PowerSolver.SolveSteadyState, ThermalSolver.SolveSteadyState, drift, hazards.
        return new CruiseStepResult
        {
            Day = Day,
            DeltaVRemaining = GetRemainingDeltaV(),
            BatteryFraction = 1f,
            LowestTempRatio = 1f,
            Events = _events,
            BrokenComponents = _broken,
        };
    }
}
