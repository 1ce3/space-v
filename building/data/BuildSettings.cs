using Godot;

namespace RocketGame.Building;

/// <summary>Shared building values. Saved as data/settings/build_settings.tres; read via PartCatalog.Instance.Settings.</summary>
[GlobalClass]
public partial class BuildSettings : Resource
{
    /// <summary>Used when no BuildSettings is loaded (tests, sandboxes).</summary>
    public const float DefaultCellSize = 2.0f;

    /// <summary>Size of one grid cell (m).</summary>
    [ExportGroup("Grid")]
    [Export(PropertyHint.Range, "0.5,10,0.1")] public float CellSize { get; set; } = DefaultCellSize;
    [Export(PropertyHint.Range, "1,32,1")] public int GridWidth { get; set; } = 9;
    [Export(PropertyHint.Range, "1,64,1")] public int GridHeight { get; set; } = 16;

    [ExportGroup("Ghost")]
    [Export] public Color GhostValidColor { get; set; } = new(0.3f, 1f, 0.4f, 0.5f);
    [Export] public Color GhostInvalidColor { get; set; } = new(1f, 0.3f, 0.3f, 0.5f);

}
