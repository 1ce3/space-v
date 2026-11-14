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

    /// <summary>Fuel left, 0..1.</summary>
    public float GetFuelFraction()
    {
        float capacity = TankDefinition?.FuelCapacity ?? 0f;
        return capacity > 0f ? CurrentFuel / capacity : 0f;
    }

    /// <summary>Removes up to amount kg; returns what was actually removed.</summary>
    public float DrainFuel(float amount)
    {
        if (amount <= 0f || CurrentFuel <= 0f) return 0f;
        float drained = Mathf.Min(amount, CurrentFuel);
        CurrentFuel -= drained;
        return drained;
    }

    /// <summary>Stops the leak.</summary>
    public void SealLeak()
    {
        LeakRateKgPerSec = 0f;
    }
}
