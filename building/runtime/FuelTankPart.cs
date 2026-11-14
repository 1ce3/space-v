using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Liquid propellant tank.</summary>
public partial class FuelTankPart : RocketPart
{
    /// <summary>Propellant left (kg). Physics writes it.</summary>
    public float CurrentFuel { get; set; }
    /// <summary>Leak rate set by a CryoLeak (kg/s); 0 = sealed.</summary>
    public float LeakRateKgPerSec { get; set; }

    public FuelTankPartDefinition TankDefinition => Definition as FuelTankPartDefinition;
    public FuelType PropellantType => TankDefinition?.PropellantType ?? FuelType.Kerolox;

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        CurrentFuel = TankDefinition?.FuelCapacity ?? 0f;
        LeakRateKgPerSec = 0f;
    }

    public override float GetCurrentMass() => DryMass + CurrentFuel;

    /// <summary>Removes up to amount kg; returns what was actually removed.</summary>
    public float DrainFuel(float amount)
    {
        // TODO(A1)
        return 0f;
    }

    /// <summary>Stops the leak.</summary>
    public void SealLeak()
    {
        LeakRateKgPerSec = 0f;
    }
}
