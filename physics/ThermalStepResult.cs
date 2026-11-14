using System.Collections.Generic;
using RocketGame.Building;

namespace RocketGame.Physics;

/// <summary>Thermal summary of one tick or one cruise day. Lists are reused by ThermalSolver.</summary>
public struct ThermalStepResult
{
    /// <summary>Largest TempK / MaxSafeTempK over all components.</summary>
    public float MaxTempRatio;
    /// <summary>Smallest TempK / MinSafeTempK over all components (cruise cold check).</summary>
    public float LowestTempRatio;
    /// <summary>Ablator burned this step (kg).</summary>
    public float MassAblatedKg;
    /// <summary>ModelIds of parts and modules that became Overheated this step.</summary>
    public List<string> OverheatedComponents;
    /// <summary>ModelIds of parts and modules that became Frozen this step (cruise).</summary>
    public List<string> FrozenComponents;
    /// <summary>ModelIds of parts and modules that became Broken this step.</summary>
    public List<string> BrokenComponents;
    /// <summary>Grid parts that became Broken this step (FailureDetector turns them into a Break).</summary>
    public List<RocketPart> BrokenParts;
}
