using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Monopropellant thrusters: torque and sideways translation.</summary>
public class RcsThrusterModule : AttitudeActuatorModule
{
    /// <summary>Monopropellant left (kg). Physics writes it.</summary>
    public float MonopropRemainingKg { get; set; }
    /// <summary>Toggled by the player (flight_rcs_toggle).</summary>
    public bool IsEnabled { get; set; }
    /// <summary>Normalized sideways command -1..1 (+1 = +X body).</summary>
    public float TranslateCommand { get; private set; }

    protected override void ResetRuntimeState()
    {
        base.ResetRuntimeState();
        MonopropRemainingKg = AcsDefinition?.MonopropCapacityKg ?? 0f;
        IsEnabled = false;
        TranslateCommand = 0f;
    }

    public override float GetCurrentMass() => DryMass + MonopropRemainingKg;

    /// <summary>Short manual pulse (P2).</summary>
    public void FireThrusterPulse(float duration) { }

    /// <summary>Sets the sideways command, clamped -1..1.</summary>
    public void CommandTranslate(float axis)
    {
        TranslateCommand = Mathf.Clamp(axis, -1f, 1f);
    }
}
