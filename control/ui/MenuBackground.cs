using Godot;

namespace RocketGame.Control;

/// <summary>
/// The 3D launch site of scenes/main_menu.tscn (terrain, NASA center, pad, camera, light) used as a live background
/// behind the main menu and the home hub. Huy's own buttons and title in that scene are hidden, so the scene stays
/// the single place where the background is modelled. Without the scene (or without Blender for terrain.blend)
/// a plain dark color is used instead.
/// </summary>
public static class MenuBackground
{
    public const string ScenePath = "res://scenes/main_menu.tscn";
    public static readonly Color FallbackColor = new(0.03f, 0.04f, 0.08f);
    /// <summary>Children of the menu scene that are UI, not background.</summary>
    private static readonly string[] HiddenNodes = { "VBoxContainer", "Label" };

    /// <summary>Adds the background as the first child of parent (behind everything else).</summary>
    public static void AddTo(Godot.Control parent)
    {
        Node scene = null;
        if (ResourceLoader.Exists(ScenePath) && ResourceLoader.Load(ScenePath) is PackedScene packed && packed.CanInstantiate())
            scene = packed.Instantiate();

        if (scene == null)
        {
            UiStyle.AddFull(parent, new ColorRect { Color = FallbackColor, MouseFilter = Godot.Control.MouseFilterEnum.Ignore });
            return;
        }
        foreach (string name in HiddenNodes)
            if (scene.GetNodeOrNull<CanvasItem>(name) is CanvasItem item) item.Visible = false;
        if (scene is Godot.Control control)
        {
            control.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;
            UiStyle.AddFull(parent, control);
        }
        else parent.AddChild(scene);

        // Soft vignette so the panels stay readable over a bright sky.
        UiStyle.AddFull(parent, new ColorRect { Color = new Color(0f, 0f, 0f, 0.25f), MouseFilter = Godot.Control.MouseFilterEnum.Ignore });
    }
}
