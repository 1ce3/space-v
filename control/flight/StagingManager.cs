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
        if (Tree == null || CurrentStageIndex >= Stages.Count)
        {
            Warn("No decouplers available to jettison");
            return false;
        }

        int index = CurrentStageIndex;
        StageGroup stage = Stages[index];
        if (index > 0)
        {
            foreach (Joint joint in stage.Joints)
            {
                if (joint.IsSeparated || joint.Decouplers.Count == 0) continue;
                joint.Decouplers[0].TriggerSeparation();
            }
        }
        foreach (EnginePart engine in stage.EnginesToArm)
        {
            if (engine.IsAttached) engine.Arm();
        }

        CurrentStageIndex++;
        if (DebugLog) GD.Print($"[Control] Stage {index} activated ({stage.Joints.Count} joints, {stage.EnginesToArm.Count} engines armed)");
        EmitSignal(SignalName.StageActivated, index);
        return true;
    }

    /// <summary>Jettisons the fairing if one is still attached.</summary>
    public bool JettisonFairing()
    {
        FairingPart fairing = Tree?.GetParts<FairingPart>().Find(f => f.IsAttached);
        if (fairing == null)
        {
            Warn("Không còn fairing để tách");
            return false;
        }
        fairing.Jettison();
        return true;
    }

    /// <summary>Stowed -> Deploy, Deployed -> Cut.</summary>
    public bool ToggleParachute()
    {
        ParachutePart chute = Tree?.GetParts<ParachutePart>().Find(c => c.IsAttached);
        if (chute == null)
        {
            Warn("Không có dù");
            return false;
        }
        if (chute.ChuteState == ParachuteState.Stowed)
        {
            if (chute.Deploy()) return true;
            Warn("Chưa thể mở dù: còn fairing");
            return false;
        }
        if (chute.ChuteState == ParachuteState.Deployed)
        {
            chute.Cut();
            return true;
        }
        return false;
    }

    /// <summary>Text for the HUD: next stage and what it does.</summary>
    public string DescribeNextStage()
    {
        if (CurrentStageIndex >= Stages.Count) return "Hết tầng";
        StageGroup stage = Stages[CurrentStageIndex];
        var engines = new List<string>();
        foreach (EnginePart e in stage.EnginesToArm) engines.Add(e.ModelId);
        string joints = stage.Joints.Count > 0 ? $"tách {stage.Joints.Count} khớp" : "phóng";
        return $"Tầng {CurrentStageIndex}/{Stages.Count - 1}: {joints}" + (engines.Count > 0 ? $", bật {string.Join(", ", engines)}" : "");
    }

    private void Warn(string message)
    {
        if (DebugLog) GD.Print($"[Control] Staging warning: {message}");
        EmitSignal(SignalName.StagingWarning, message);
    }
}
