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

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return;
        TutorialDone = (bool)cfg.GetValue("story", "tutorial_done", false);
        EndingSeen = (bool)cfg.GetValue("story", "ending_seen", false);
        ResearchGuideSeen = (bool)cfg.GetValue("story", "research_guide_seen", false);
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("story", "tutorial_done", TutorialDone);
        cfg.SetValue("story", "ending_seen", EndingSeen);
        cfg.SetValue("story", "research_guide_seen", ResearchGuideSeen);
        cfg.Save(Path);
    }

    public static void Reset()
    {
        TutorialDone = false;
        EndingSeen = false;
        ResearchGuideSeen = false;
        ShowWinOnHome = false;
        Save();
    }
}
