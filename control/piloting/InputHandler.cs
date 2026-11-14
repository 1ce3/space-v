using Godot;
using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>
/// Reads the flight_* input actions. Axes are read on Poll(); one-shot presses are latched in
/// _UnhandledInput and handed out (then cleared) by the next Poll() on the physics tick.
/// </summary>
public partial class InputHandler : Node
{
    /// <summary>Action names, so no string literal is repeated elsewhere.</summary>
    public static class Actions
    {
        public const string PitchLeft = "flight_pitch_left";
        public const string PitchRight = "flight_pitch_right";
        public const string ThrottleUp = "flight_throttle_up";
        public const string ThrottleDown = "flight_throttle_down";
        public const string ThrottleFull = "flight_throttle_full";
        public const string ThrottleCut = "flight_throttle_cut";
        public const string Stage = "flight_stage";
        public const string Fairing = "flight_fairing";
        public const string Chute = "flight_chute";
        public const string SasToggle = "flight_sas_toggle";
        public const string SasMode1 = "flight_sas_mode_1";
        public const string SasMode2 = "flight_sas_mode_2";
        public const string SasMode3 = "flight_sas_mode_3";
        public const string SasMode4 = "flight_sas_mode_4";
        public const string RcsToggle = "flight_rcs_toggle";
        public const string RcsLeft = "flight_rcs_left";
        public const string RcsRight = "flight_rcs_right";
        public const string DeployToggle = "flight_deploy_toggle";
        public const string SceToAux = "flight_sce_to_aux";
        public const string SealLeak = "flight_seal_leak";
        public const string Abort = "flight_abort";
        public const string ZoomIn = "flight_zoom_in";
        public const string ZoomOut = "flight_zoom_out";
    }

    [Export] public bool DebugLog { get; set; }

    /// <summary>False while input is locked (segment ended, cruise menus...).</summary>
    public bool IsInputEnabled { get; private set; } = true;

    private PilotInput _pending;

    /// <summary>Input for this tick. Returns PilotInput.Empty when disabled.</summary>
    public PilotInput Poll()
    {
        if (!IsInputEnabled)
        {
            _pending = PilotInput.Empty;
            return PilotInput.Empty;
        }

        PilotInput input = _pending;
        _pending = PilotInput.Empty;
        input.PitchAxis = Input.GetAxis(Actions.PitchLeft, Actions.PitchRight);
        input.ThrottleAxis = Input.GetAxis(Actions.ThrottleDown, Actions.ThrottleUp);
        input.RcsTranslateAxis = Input.GetAxis(Actions.RcsLeft, Actions.RcsRight);
        return input;
    }

    /// <summary>Enables or locks pilot input.</summary>
    public void SetInputEnabled(bool enabled)
    {
        IsInputEnabled = enabled;
        _pending = PilotInput.Empty;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsInputEnabled || @event.IsEcho()) return;

        if (@event.IsActionPressed(Actions.ThrottleFull)) _pending.ThrottleFull = true;
        else if (@event.IsActionPressed(Actions.ThrottleCut)) _pending.ThrottleCut = true;
        else if (@event.IsActionPressed(Actions.Stage)) _pending.StagePressed = true;
        else if (@event.IsActionPressed(Actions.Fairing)) _pending.FairingPressed = true;
        else if (@event.IsActionPressed(Actions.Chute)) _pending.ChutePressed = true;
        else if (@event.IsActionPressed(Actions.SasToggle)) _pending.SasTogglePressed = true;
        else if (@event.IsActionPressed(Actions.SasMode1)) RequestSas(SasMode.StabilityAssist);
        else if (@event.IsActionPressed(Actions.SasMode2)) RequestSas(SasMode.HoldPitch);
        else if (@event.IsActionPressed(Actions.SasMode3)) RequestSas(SasMode.Prograde);
        else if (@event.IsActionPressed(Actions.SasMode4)) RequestSas(SasMode.Retrograde);
        else if (@event.IsActionPressed(Actions.RcsToggle)) _pending.RcsTogglePressed = true;
        else if (@event.IsActionPressed(Actions.DeployToggle)) _pending.DeployTogglePressed = true;
        else if (@event.IsActionPressed(Actions.SceToAux)) _pending.SceToAuxPressed = true;
        else if (@event.IsActionPressed(Actions.SealLeak)) _pending.SealLeakPressed = true;
        else if (@event.IsActionPressed(Actions.Abort)) _pending.AbortPressed = true;
        else return;

        if (DebugLog) GD.Print($"[Control] Input {@event.AsText()}");
    }

    private void RequestSas(SasMode mode)
    {
        _pending.HasSasModeRequest = true;
        _pending.RequestedSasMode = mode;
    }
}
