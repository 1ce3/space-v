using Godot;
using RocketGame.Core;
using RocketGame.Research;

namespace RocketGame.Tests.B;

/// <summary>Sandbox for task B4 (part of B0): exercises the ResearchCampaignEngine autoload. WARNING: overwrites user://save.json.</summary>
public partial class ResearchTest : Node
{
	/// <summary>RP granted before trying purchases.</summary>
	[Export] public int StartRP { get; set; } = 100;
	/// <summary>Wipe the save file when the test ends.</summary>
	[Export] public bool ResetSaveAfter { get; set; } = true;

	public override void _Ready() => CallDeferred(nameof(RunTest));

	private void RunTest()
	{
		ResearchCampaignEngine campaign = ResearchCampaignEngine.Instance;
		if (campaign == null || campaign.TechTree == null)
		{
			GD.PushError("[Research] ResearchCampaignEngine autoload is missing or not configured.");
			return;
		}
		TechTreeManager tree = campaign.TechTree;

		campaign.ResetSave();
		GD.Print($"[Research] Starters: {string.Join(", ", tree.GetUnlockedModels())}");
		GD.Print($"[Research] Max weight {tree.GetMaxBuildWeight()} (expect 20), mission {campaign.CurrentMission?.MissionId}, payload {campaign.CurrentMission?.PayloadMassKg} kg");

		campaign.AddRP(StartRP);
		Check("buy t1_heavy (30)", tree.PurchaseNode("t1_heavy"), true);
		Check("buy t2_main_engine (60)", tree.PurchaseNode("t2_main_engine"), true);
		Check("buy t2_upper_stage (80, only 10 RP left)", tree.PurchaseNode("t2_upper_stage"), false);
		Check("buy t3_edl (prerequisite missing)", tree.CanPurchase("t3_edl"), false);
		Check("engine_lv2 unlocked", tree.IsUnlocked("engine_lv2"), true);
		GD.Print($"[Research] RP now {campaign.CurrentRP} (expect 10)");

		var failed = new FlightResult
		{
			MissionId = "mars_mission", Outcome = MissionOutcome.Failed, SegmentsCompleted = 1,
			MaxAltitude = 95000f, MaxSpeed = 2100f, FailureCause = FailureCause.Thermal,
			FailedPartGroup = (int)PartGroup.HeatShield,
		};
		GD.Print($"[Research] Flight 1: {campaign.ProcessMissionEnd(failed)} (expect 60 + 60 + 15 = 135)");
		GD.Print($"[Research] Flight 2 (same): {campaign.ProcessMissionEnd(failed)} (expect 60 + 0 + 0 = 60)");
		GD.Print($"[Research] RP now {campaign.CurrentRP} (expect 205), complete = {campaign.IsCampaignComplete()}");

		if (ResetSaveAfter) campaign.ResetSave();
	}

	private static void Check(string label, bool actual, bool expected)
	{
		string mark = actual == expected ? "OK  " : "FAIL";
		GD.Print($"[Research] {mark} {label}: {actual}");
	}
}
