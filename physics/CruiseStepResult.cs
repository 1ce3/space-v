using System.Collections.Generic;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>Resources after one cruise day. Shown by CruiseHUD (C).</summary>
public struct CruiseStepResult
{
    /// <summary>Day number, starting at 1.</summary>
    public int Day;
    /// <summary>Delta-v still available (m/s).</summary>
    public float DeltaVRemaining;
    /// <summary>Supply minus demand (W).</summary>
    public float PowerBalanceW;
    /// <summary>Battery state of charge (0..1).</summary>
    public float BatteryFraction;
    /// <summary>Smallest TempK / MinSafeTempK (below 1 = something froze).</summary>
    public float LowestTempRatio;
    /// <summary>Avionics lost power today.</summary>
    public bool IsBrownout;
    /// <summary>Hazards rolled today (P1).</summary>
    public List<FlightEvent> Events;
    /// <summary>ModelIds of components that became Broken today.</summary>
    public List<string> BrokenComponents;
    /// <summary>ModelIds of components that became Frozen today.</summary>
    public List<string> FrozenComponents;
}
