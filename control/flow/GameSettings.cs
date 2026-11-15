// AI-assisted: written with Claude (Anthropic); reviewed by The Neptunians.
using Godot;

namespace RocketGame.Control;

/// <summary>Player options from the main menu (volume, fullscreen), saved in user://settings.cfg.</summary>
public static class GameSettings
{
    public const string Path = "user://settings.cfg";
    /// <summary>0..1, master bus.</summary>
    public static float MasterVolume { get; set; } = 0.8f;
    public static bool Fullscreen { get; set; }

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return;
        MasterVolume = cfg.GetValue("audio", "master", 0.8f).AsSingle();
        Fullscreen = cfg.GetValue("video", "fullscreen", false).AsBool();
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("audio", "master", MasterVolume);
        cfg.SetValue("video", "fullscreen", Fullscreen);
        cfg.Save(Path);
    }

    /// <summary>Pushes the values to the audio server and the window.</summary>
    public static void Apply()
    {
        int bus = AudioServer.GetBusIndex("Master");
        if (bus >= 0)
        {
            AudioServer.SetBusMute(bus, MasterVolume <= 0.001f);
            AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(Mathf.Max(MasterVolume, 0.001f)));
        }
        DisplayServer.WindowMode mode = DisplayServer.WindowGetMode();
        bool isFull = mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        if (Fullscreen != isFull)
            DisplayServer.WindowSetMode(Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
    }
}
