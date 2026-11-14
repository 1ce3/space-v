using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Control;

/// <summary>
/// Player-triggered cuts: stage separation, fairing, parachute (C_CONTROL.md 4.5).
/// One of the only two paths allowed to cut the part tree (the other is StructuralBreakHandler).
/// </summary>
public partial class StagingManager : Node
{
    [Signal] public delegate void StageActivatedEventHandler(int stageIndex);
    [Signal] public delegate void StagingWarningEventHandler(string message);

    [ExportGroup("Debug")]
    [Export] public bool DebugLog { get; set; }

    /// <summary>Index of the next stage to activate. Lives across all 3 segments.</summary>
    public int CurrentStageIndex { get; private set; }
    /// <summary>Stages derived by RocketAssembler.</summary>
    public List<StageGroup> Stages { get; set; } = new();
    /// <summary>Part tree of the flying rocket.</summary>
    public PartTree Tree { get; private set; }

    /// <summary>Called by VehicleRoot.OnDesignConfirmed (runtime data, so Bind instead of Export).</summary>
    public void Bind(PartTree tree, List<StageGroup> stages)
    {
        Tree = tree;
        Stages = stages ?? new List<StageGroup>();
        CurrentStageIndex = 0;
    }

    /// <summary>Stage 0 arms engines; later stages separate their joints then arm their engines.</summary>
    public bool ActivateNextStage()
    {
        // TODO(C3): per 4.5; out of stages -> StagingWarning("No decouplers available to jettison").
        return false;
    }

    /// <summary>Jettisons the fairing if one is still attached.</summary>
    public bool JettisonFairing()
    {
        // TODO(C3)
        return false;
    }

    /// <summary>Stowed -> Deploy, Deployed -> Cut.</summary>
    public bool ToggleParachute()
    {
        // TODO(C6)
        return false;
    }

    private void Warn(string message)
    {
        if (DebugLog) GD.Print($"[Control] Staging warning: {message}");
        EmitSignal(SignalName.StagingWarning, message);
    }
}
