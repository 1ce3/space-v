using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>Result of one physics tick. Lists are owned and reused by PhysicsCoordinator; copy them if you keep them.</summary>
public struct PhysicsStepOutput
{
    /// <summary>Sum of all forces in world 2D (N), gravity included.</summary>
    public Vector2 NetForce;
    /// <summary>Sum of all torques about the center of mass (N*m, + = counter-clockwise).</summary>
    public float NetTorqueZ;
    /// <summary>Propellant burned this tick (kg).</summary>
    public float ConsumedMass;
    /// <summary>Gravity part of NetForce (N). Used by the stress evaluator.</summary>
    public Vector2 GravityForce;
    /// <summary>Altitude of the center of mass (m).</summary>
    public float AltitudeMeters;
    /// <summary>Felt acceleration in g (gravity excluded).</summary>
    public float CurrentGForce;
    /// <summary>q = 0.5 rho v^2 (Pa).</summary>
    public float DynamicPressure;
    /// <summary>Speed / speed of sound.</summary>
    public float MachNumber;
    /// <summary>Convective heat flux at the nose (W/m^2).</summary>
    public float HeatFlux;
    /// <summary>Air state at the current altitude.</summary>
    public AtmosphereSample Air;
    /// <summary>Thrust and aero force on each part (world 2D).</summary>
    public List<PartLoad> PartLoads;
    /// <summary>Events physics detected this tick.</summary>
    public List<FlightEvent> Events;
    /// <summary>Thermal summary of this tick.</summary>
    public ThermalStepResult Thermal;
    /// <summary>Electric balance of this tick (P1): supply, demand, battery, brownout.</summary>
    public PowerStepResult Power;
}
