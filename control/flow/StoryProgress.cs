using Godot;

namespace RocketGame.Control;

/// <summary>Story flags (tutorial done, ending seen) saved apart from the research save.</summary>
public static class StoryProgress
{
    public const string Path = "user://story.cfg";
    public static bool TutorialDone { get; set; }
    public static bool EndingSeen { get; set; }
    public static bool ResearchGuideSeen { get; set; }
    /// <summary>Set after the ending: the home screen shows the win panel once.</summary>
    public static bool ShowWinOnHome { get; set; }

    // Last flight log (home hub card and "View full log"): the debrief text of the last finished run.
    public static string LastLogTitle { get; private set; } = "";
    public static string LastLogBody { get; private set; } = "";
    public static string LastLogReward { get; private set; } = "";
    public static string LastLogDate { get; private set; } = "";
    /// <summary>(int)MissionOutcome of the last run: 0 success, 1 failed, 2 aborted.</summary>
    public static int LastLogOutcome { get; private set; } = 1;
    public static bool HasLastLog => !string.IsNullOrEmpty(LastLogTitle);

    /// <summary>Stores the debrief text of a finished run and saves.</summary>
    public static void SetLastLog(string title, string body, string reward, int outcome)
    {
        LastLogTitle = title ?? "";
        LastLogBody = body ?? "";
        LastLogReward = reward ?? "";
        LastLogOutcome = outcome;
        LastLogDate = Time.GetDatetimeStringFromSystem(false, true);
        Save();
    }

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return;
        TutorialDone = (bool)cfg.GetValue("story", "tutorial_done", false);
        EndingSeen = (bool)cfg.GetValue("story", "ending_seen", false);
        ResearchGuideSeen = (bool)cfg.GetValue("story", "research_guide_seen", false);
        LastLogTitle = cfg.GetValue("log", "title", "").AsString();
        LastLogBody = cfg.GetValue("log", "body", "").AsString();
        LastLogReward = cfg.GetValue("log", "reward", "").AsString();
        LastLogDate = cfg.GetValue("log", "date", "").AsString();
        LastLogOutcome = cfg.GetValue("log", "outcome", 1).AsInt32();
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("story", "tutorial_done", TutorialDone);
        cfg.SetValue("story", "ending_seen", EndingSeen);
        cfg.SetValue("story", "research_guide_seen", ResearchGuideSeen);
        cfg.SetValue("log", "title", LastLogTitle);
        cfg.SetValue("log", "body", LastLogBody);
        cfg.SetValue("log", "reward", LastLogReward);
        cfg.SetValue("log", "date", LastLogDate);
        cfg.SetValue("log", "outcome", LastLogOutcome);
        cfg.Save(Path);
    }

    public static void Reset()
    {
        TutorialDone = false;
        EndingSeen = false;
        ResearchGuideSeen = false;
        ShowWinOnHome = false;
        LastLogTitle = LastLogBody = LastLogReward = LastLogDate = "";
        LastLogOutcome = 1;
        Save();
    }
}
