using System.Collections.Generic;
using Godot;

namespace RocketGame.Research;

/// <summary>Holds the tech nodes and which ones are bought. Answers "which ModelIds are unlocked". Child of ResearchCampaignEngine.</summary>
[GlobalClass]
public partial class TechTreeManager : Node
{
    /// <summary>NodeId was purchased.</summary>
    [Signal] public delegate void NodeUnlockedEventHandler(string nodeId);

    /// <summary>Every tech node of the game (data/tech/*.tres).</summary>
    [Export] public Godot.Collections.Array<TechNode> AllNodes { get; set; } = new();
    /// <summary>ModelIds available from the start.</summary>
    [Export] public string[] StarterModelIds { get; set; } = System.Array.Empty<string>();
    /// <summary>Build weight before any frame upgrade.</summary>
    [Export(PropertyHint.Range, "1,200,1")] public int BaseBuildWeight { get; set; } = 20;

    /// <summary>Purchased NodeIds.</summary>
    public HashSet<string> UnlockedNodes { get; } = new();

    private readonly Dictionary<string, TechNode> _byId = new();
    private readonly HashSet<string> _unlockedModels = new();

    public override void _Ready()
    {
        _byId.Clear();
        foreach (TechNode node in AllNodes)
        {
            if (node == null || string.IsNullOrEmpty(node.NodeId))
            {
                GD.PushError("[Research] TechTreeManager.AllNodes has an empty entry or a node without NodeId.");
                continue;
            }
            if (!_byId.TryAdd(node.NodeId, node)) GD.PushError($"[Research] Duplicate tech NodeId '{node.NodeId}'.");
        }
        ValidatePrerequisites();
        RebuildUnlockedModels();
    }

    /// <summary>Tech node by id, or null.</summary>
    public TechNode GetNode(string nodeId) => _byId.TryGetValue(nodeId, out TechNode node) ? node : null;

    /// <summary>True when the node exists, is not bought, all prerequisites are bought and the player has enough RP.</summary>
    public bool CanPurchase(string nodeId)
    {
        TechNode node = GetNode(nodeId);
        if (node == null || UnlockedNodes.Contains(nodeId)) return false;
        foreach (string pre in node.PrerequisiteNodeIds)
        {
            if (!UnlockedNodes.Contains(pre)) return false;
        }
        ResearchCampaignEngine campaign = ResearchCampaignEngine.Instance;
        return campaign != null && campaign.CurrentRP >= node.CostRP;
    }

    /// <summary>Spends RP, unlocks the node, emits NodeUnlocked and saves the game.</summary>
    public bool PurchaseNode(string nodeId)
    {
        if (!CanPurchase(nodeId)) return false;
        TechNode node = GetNode(nodeId);
        ResearchCampaignEngine campaign = ResearchCampaignEngine.Instance;
        if (!campaign.SpendRP(node.CostRP)) return false;

        UnlockedNodes.Add(nodeId);
        foreach (string id in node.UnlocksModelIds) _unlockedModels.Add(id);
        GD.Print($"[Research] Purchased {nodeId} for {node.CostRP} RP");
        EmitSignal(SignalName.NodeUnlocked, nodeId);
        campaign.SaveProgress();
        return true;
    }

    /// <summary>Starter ModelIds plus every ModelId of the purchased nodes. PartCatalog filters by kind.</summary>
    public IReadOnlyCollection<string> GetUnlockedModels() => _unlockedModels;

    /// <summary>True when modelId is a starter or unlocked by a purchased node.</summary>
    public bool IsUnlocked(string modelId) => _unlockedModels.Contains(modelId);

    /// <summary>BaseBuildWeight plus BuildWeightBonus of every purchased node.</summary>
    public int GetMaxBuildWeight()
    {
        int weight = BaseBuildWeight;
        foreach (string id in UnlockedNodes)
        {
            TechNode node = GetNode(id);
            if (node != null) weight += node.BuildWeightBonus;
        }
        return weight;
    }

    /// <summary>Replaces the purchased set (used when loading or resetting a save). Does not emit NodeUnlocked.</summary>
    internal void LoadUnlockedNodes(IEnumerable<string> nodeIds)
    {
        UnlockedNodes.Clear();
        foreach (string id in nodeIds)
        {
            if (_byId.ContainsKey(id)) UnlockedNodes.Add(id);
            else GD.PushWarning($"[Research] Save contains unknown tech node '{id}', ignored.");
        }
        RebuildUnlockedModels();
    }

    private void RebuildUnlockedModels()
    {
        _unlockedModels.Clear();
        foreach (string id in StarterModelIds) _unlockedModels.Add(id);
        foreach (string nodeId in UnlockedNodes)
        {
            TechNode node = GetNode(nodeId);
            if (node == null) continue;
            foreach (string id in node.UnlocksModelIds) _unlockedModels.Add(id);
        }
    }

    private void ValidatePrerequisites()
    {
        foreach (TechNode node in _byId.Values)
        {
            foreach (string pre in node.PrerequisiteNodeIds)
            {
                if (!_byId.ContainsKey(pre)) GD.PushError($"[Research] Tech node '{node.NodeId}' needs unknown node '{pre}'.");
            }
        }
    }
}
