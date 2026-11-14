using Godot;
using RocketGame.Research;

namespace RocketGame.Tests.B;

/// <summary>
/// Sandbox for task B4: opens the research screen with a fresh save, runs automatic checks (buy, prerequisites, RP,
/// save file on disk), then leaves the screen open so the buttons can be tried by hand. WARNING: overwrites user://save.json.
/// </summary>
public partial class ResearchScreenTest : Node
{
    /// <summary>scenes/research_screen.tscn.</summary>
    [Export] public PackedScene Screen { get; set; }
    /// <summary>RP given before the checks.</summary>
    [Export] public int StartRP { get; set; } = 200;
    /// <summary>Run the automatic purchase checks before handing over to the player.</summary>
    [Export] public bool RunChecks { get; set; } = true;
    /// <summary>Wipe the save file when the sandbox closes.</summary>
    [Export] public bool ResetSaveOnExit { get; set; } = true;

    private ResearchScreenRoot _screen;

    public override void _Ready()
    {
        if (!Core.NodeGuard.Require(this, Screen, nameof(Screen))) return;
        CallDeferred(nameof(Open));
    }

    public override void _ExitTree()
    {
        if (_screen != null) _screen.BuildRequested -= OnBuildRequested;
        if (ResetSaveOnExit) ResearchCampaignEngine.Instance?.ResetSave();
    }

    private void Open()
    {
        ResearchCampaignEngine campaign = ResearchCampaignEngine.Instance;
        if (campaign?.TechTree == null)
        {
            GD.PushError("[Research] ResearchCampaignEngine autoload is missing.");
            return;
        }
        campaign.ResetSave();
        campaign.AddRP(StartRP);

        _screen = Screen.Instantiate<ResearchScreenRoot>();
        _screen.BuildRequested += OnBuildRequested;
        AddChild(_screen);

        if (RunChecks) Check(campaign);
        GD.Print("[Research] Screen open: try the Unlock buttons and Go to Workshop by hand.");
    }

    private void Check(ResearchCampaignEngine campaign)
    {
        Expect("buy t1_heavy (30 RP)", _screen.TryPurchase("t1_heavy"), true);
        Expect("buy t2_upper_stage (needs t2_main_engine)", _screen.TryPurchase("t2_upper_stage"), false);
        Expect("buy t2_main_engine (60 RP)", _screen.TryPurchase("t2_main_engine"), true);
        Expect("buy t2_main_engine again", _screen.TryPurchase("t2_main_engine"), false);
        Expect($"RP = {StartRP - 90}", campaign.CurrentRP == StartRP - 90, true);

        var onDisk = new SaveGame { FilePath = campaign.SavePath };
        bool loaded = onDisk.Load();
        Expect("save file has RP and both nodes",
            loaded && onDisk.CurrentRP == StartRP - 90 && onDisk.UnlockedNodes.Contains("t1_heavy") && onDisk.UnlockedNodes.Contains("t2_main_engine"),
            true);
    }

    private static void Expect(string label, bool actual, bool expected)
        => GD.Print($"[Research] {(actual == expected ? "OK  " : "FAIL")} {label}: {actual}");

    private void OnBuildRequested() => GD.Print("[Research] OK   BuildRequested received (Go to Workshop)");
}
