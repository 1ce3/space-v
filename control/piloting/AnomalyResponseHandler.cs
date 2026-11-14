using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Physics;

namespace RocketGame.Control;

/// <summary>Turns physics events into anomalies the player must clear with a key (C_CONTROL.md 5.6, P1).</summary>
public partial class AnomalyResponseHandler : Node
{
    [Signal] public delegate void AnomalyRaisedEventHandler(ActiveAnomaly anomaly);
    [Signal] public delegate void AnomalyResolvedEventHandler(ActiveAnomaly anomaly);

    [Export] public PhysicsCoordinator Coordinator { get; set; }
    [Export] public AttitudeController AttitudeController { get; set; }
    [Export] public ThrottleController ThrottleController { get; set; }
    /// <summary>How long a warning-only event (MaxQ, G, overheat, brownout, flameout) stays on the HUD.</summary>
    [Export(PropertyHint.Range, "1,15,0.5")] public float WarningSeconds { get; set; } = 4f;
    [Export] public bool DebugLog { get; set; }

    /// <summary>Anomalies not yet resolved.</summary>
    public List<ActiveAnomaly> ActiveAnomalies { get; } = new();
    /// <summary>True after the player switched SCE to AUX during a lightning strike.</summary>
    public bool IsSceToAuxActive { get; private set; }
    /// <summary>True after an SEU: SAS off and throttle frozen until the player toggles SAS.</summary>
    public bool IsSafeModeActive { get; private set; }
    /// <summary>True after lightning: the HUD shows garbled telemetry.</summary>
    public bool IsTelemetryGlitched { get; private set; }

    private PartTree _tree;
    /// <summary>Avionics this handler switched off (lightning), so only those come back on.</summary>
    private readonly List<PodModule> _unpowered = new();

    /// <summary>Gives the handler the part tree (leaking tanks, RCS, avionics power).</summary>
    public void Bind(PartTree tree)
    {
        _tree = tree;
        ActiveAnomalies.Clear();
        _unpowered.Clear();
        IsSceToAuxActive = false;
        IsSafeModeActive = false;
        IsTelemetryGlitched = false;
        if (ThrottleController != null) ThrottleController.IsFrozen = false;
    }

    // ------------------------------------------------------------------ A13: reboot broken modules (key B)

    private string _pendingMessage;

    /// <summary>HUD line from the last reboot attempt, cleared when read.</summary>
    public string ConsumeMessage()
    {
        string m = _pendingMessage;
        _pendingMessage = null;
        return m;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.B } || _tree == null) return;
        _pendingMessage = ModuleReboot.TryRebootNext(PowerSolver.FindPod(_tree)) ?? "Không có thiết bị hỏng";
        GetViewport().SetInputAsHandled();
    }

    /// <summary>True when an anomaly of this type is waiting.</summary>
    public bool Has(FlightEventType type) => Find(type) != null;

    /// <summary>Receives PhysicsStepOutput.Events (or CruiseStepResult.Events).</summary>
    public void OnFlightEvents(IReadOnlyList<FlightEvent> events)
    {
        if (events == null) return;
        foreach (FlightEvent e in events)
        {
            if (e.Type == FlightEventType.WindShear || Has(e.Type)) continue;
            var anomaly = new ActiveAnomaly(e.Type, InitialTime(e.Type), e.SourceId);
            ActiveAnomalies.Add(anomaly);
            ApplyConsequence(anomaly);
            if (DebugLog) GD.Print($"[Control] Anomaly raised: {e.Type} ({e.SourceId}), {anomaly.TimeRemaining:0.0} s");
            EmitSignal(SignalName.AnomalyRaised, anomaly);
        }
    }

    /// <summary>Clears anomalies with the matching keys. RCS dodging needs no key here: the allocator already
    /// passes Q/E to the RCS and B's HazardMonitor ends the warning once the sideways speed changed enough.</summary>
    public void HandleInput(PilotInput input)
    {
        if (input.SceToAuxPressed && Find(FlightEventType.LightningStrike) is ActiveAnomaly lightning)
        {
            IsSceToAuxActive = true;
            IsTelemetryGlitched = false;
            foreach (PodModule module in _unpowered) module.IsPowered = true;
            _unpowered.Clear();
            Resolve(lightning);
        }
        if (input.SealLeakPressed && Find(FlightEventType.CryoLeak) is ActiveAnomaly leak)
        {
            foreach (FuelTankPart tank in _tree?.GetParts<FuelTankPart>() ?? new())
                if (tank.LeakRateKgPerSec > 0f) tank.SealLeak();
            Resolve(leak);
        }
        if (input.SasTogglePressed && Find(FlightEventType.SeuGlitch) is ActiveAnomaly seu)
        {
            IsSafeModeActive = false;
            if (ThrottleController != null) ThrottleController.IsFrozen = false;
            Resolve(seu);
        }
    }

    /// <summary>Counts down anomaly timers: debris follows B's countdown, warnings fade out.</summary>
    public void Tick(float deltaTime)
    {
        for (int i = ActiveAnomalies.Count - 1; i >= 0; i--)
        {
            ActiveAnomaly a = ActiveAnomalies[i];
            if (a.Type == FlightEventType.DebrisWarning)
            {
                a.TimeRemaining = Coordinator?.Hazards?.DebrisSecondsLeft ?? 0f;
                if (a.TimeRemaining <= 0f) Resolve(a); // dodged, or B applied the hit
            }
            else if (IsWarningOnly(a.Type))
            {
                a.TimeRemaining -= deltaTime;
                if (a.TimeRemaining <= 0f) Resolve(a);
            }
            else if (a.Type == FlightEventType.CryoLeak && !AnyTankLeaking())
            {
                Resolve(a); // the leaking tank was dropped or ran dry
            }
        }
    }

    /// <summary>Player-facing line for the HUD: what happened, which key, how long.</summary>
    public static string Describe(ActiveAnomaly a) => a.Type switch
    {
        FlightEventType.LightningStrike => "SÉT ĐÁNH: mất điện điều khiển, bấm C (SCE → AUX)",
        FlightEventType.CryoLeak => $"RÒ NHIÊN LIỆU {a.SourceId}: bấm V để bịt",
        FlightEventType.DebrisWarning => $"MẢNH VỠ trong {a.TimeRemaining:0.0} s: né bằng Q/E (RCS bật bằng R)",
        FlightEventType.SeuGlitch => "LỖI BỨC XẠ: chế độ an toàn, bấm T để khởi động lại",
        FlightEventType.MaxQExceeded => "Áp suất động quá cao: giảm ga",
        FlightEventType.GLimitExceeded => "Gia tốc quá giới hạn khoang lái: giảm ga",
        FlightEventType.ComponentOverheat => $"Quá nhiệt: {a.SourceId}",
        FlightEventType.PowerBrownout => "Mất điện: pin cạn",
        FlightEventType.EngineFlameout => $"Động cơ tắt: {a.SourceId}",
        _ => a.Type.ToString(),
    };

    /// <summary>Warning-only events: shown, never need a key.</summary>
    public static bool IsWarningOnly(FlightEventType type) => type is FlightEventType.MaxQExceeded or FlightEventType.GLimitExceeded
        or FlightEventType.ComponentOverheat or FlightEventType.PowerBrownout or FlightEventType.EngineFlameout;

    private float InitialTime(FlightEventType type)
    {
        if (type == FlightEventType.DebrisWarning) return Coordinator?.Hazards?.DebrisSecondsLeft ?? 0f;
        return IsWarningOnly(type) ? WarningSeconds : 0f;
    }

    private void ApplyConsequence(ActiveAnomaly a)
    {
        switch (a.Type)
        {
            case FlightEventType.LightningStrike:
                IsTelemetryGlitched = true;
                IsSceToAuxActive = false;
                foreach (CommandPodPart pod in _tree?.GetParts<CommandPodPart>() ?? new())
                    foreach (PodModule module in pod.Modules)
                        if (module.Definition?.SlotType == ModuleSlotType.Avionics && module.IsPowered)
                        {
                            module.IsPowered = false;
                            _unpowered.Add(module);
                        }
                AttitudeController?.SetMode(SasMode.Off);
                break;
            case FlightEventType.SeuGlitch:
                IsSafeModeActive = true;
                AttitudeController?.SetMode(SasMode.Off);
                if (ThrottleController != null) ThrottleController.IsFrozen = true;
                break;
        }
    }

    private void Resolve(ActiveAnomaly a)
    {
        ActiveAnomalies.Remove(a);
        Coordinator?.Hazards?.Acknowledge(a.Type);
        if (DebugLog) GD.Print($"[Control] Anomaly resolved: {a.Type}");
        EmitSignal(SignalName.AnomalyResolved, a);
    }

    private ActiveAnomaly Find(FlightEventType type) => ActiveAnomalies.Find(a => a.Type == type);

    private bool AnyTankLeaking()
    {
        foreach (FuelTankPart tank in _tree?.GetParts<FuelTankPart>() ?? new())
            if (tank.IsAttached && tank.LeakRateKgPerSec > 0f) return true;
        return false;
    }
}
