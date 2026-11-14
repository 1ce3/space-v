using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>Player input for one physics tick. Axes are -1..1; bools are one-shot presses.</summary>
public struct PilotInput
{
    /// <summary>flight_pitch_right - flight_pitch_left. Manual command is u = -PitchAxis.</summary>
    public float PitchAxis;
    /// <summary>flight_throttle_up - flight_throttle_down.</summary>
    public float ThrottleAxis;
    public bool ThrottleFull;
    public bool ThrottleCut;
    public bool StagePressed;
    public bool FairingPressed;
    public bool ChutePressed;
    public bool SasTogglePressed;
    /// <summary>True when one of flight_sas_mode_1..4 was pressed this tick.</summary>
    public bool HasSasModeRequest;
    /// <summary>Requested mode; valid only when HasSasModeRequest.</summary>
    public SasMode RequestedSasMode;
    public bool RcsTogglePressed;
    /// <summary>flight_rcs_right - flight_rcs_left.</summary>
    public float RcsTranslateAxis;
    public bool SceToAuxPressed;
    public bool SealLeakPressed;
    public bool DeployTogglePressed;
    public bool AbortPressed;

    /// <summary>No input at all.</summary>
    public static PilotInput Empty => default;
}
