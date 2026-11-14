using Godot;

namespace RocketGame.Building;

/// <summary>Build screen settings, stored as data/settings/build_settings.tres. Read through PartCatalog.Instance.Settings.</summary>
[GlobalClass]
public partial class BuildSettings : Resource
{
    /// <summary>Edge length of one grid cell, in meters.</summary>
    [Export] public float CellSize { get; set; } = 2.0f;

    /// <summary>Grid width, in cells.</summary>
    [Export] public int GridWidth { get; set; } = 9;

    /// <summary>Grid height, in cells.</summary>
    [Export] public int GridHeight { get; set; } = 16;

    /// <summary>Ghost tint when the part can be placed.</summary>
    [Export] public Color GhostValidColor { get; set; } = new Color(0.2f, 0.9f, 0.3f, 0.5f);

    /// <summary>Ghost tint when the part cannot be placed.</summary>
    [Export] public Color GhostInvalidColor { get; set; } = new Color(0.9f, 0.2f, 0.2f, 0.5f);
}
