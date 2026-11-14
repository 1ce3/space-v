using Godot;

namespace RocketGame.Physics;

/// <summary>Static data of one celestial body (planet). Saved as .tres in data/bodies/. Planet center is always at (0, -Radius).</summary>
[GlobalClass]
public partial class CelestialBodyDefinition : Resource
{
    /// <summary>Snake_case id, same as the .tres file name.</summary>
    [Export] public string BodyId { get; set; } = "";
    /// <summary>Name shown to the player.</summary>
    [Export] public string DisplayName { get; set; } = "";

    /// <summary>Planet mass (kg).</summary>
    [ExportGroup("Gravity")]
    [Export] public float Mass { get; set; }
    /// <summary>Planet radius (m).</summary>
    [Export] public float Radius { get; set; }

    /// <summary>True: USSA76 layer table (Earth). False: exponential model from the fields below.</summary>
    [ExportGroup("Atmosphere")]
    [Export] public bool UsesLayerTable { get; set; }
    /// <summary>Air density at altitude 0 (kg/m^3). Exponential model only.</summary>
    [Export] public float SeaLevelDensity { get; set; }
    /// <summary>Air pressure at altitude 0 (Pa). Exponential model only.</summary>
    [Export] public float SeaLevelPressure { get; set; }
    /// <summary>Scale height of the exponential model (m).</summary>
    [Export] public float ScaleHeight { get; set; }
    /// <summary>Constant air temperature of the exponential model (K).</summary>
    [Export] public float SurfaceTemperatureK { get; set; }
    /// <summary>Constant speed of sound of the exponential model (m/s).</summary>
    [Export] public float SpeedOfSoundMps { get; set; }
    /// <summary>Above this altitude the atmosphere is vacuum (m). Exponential model only; USSA76 ends at 86 km.</summary>
    [Export] public float AtmosphereTopM { get; set; }

    /// <summary>Sutton-Graves constant for entry heating (game tuning value).</summary>
    [ExportGroup("Heating and light")]
    [Export] public float SuttonGravesConstant { get; set; }
    /// <summary>Solar flux near this body (W/m^2).</summary>
    [Export] public float SolarFluxWm2 { get; set; }

    /// <summary>Optional environment scene for BodyEnvironmentVisual (C). May be null.</summary>
    [ExportGroup("Visuals")]
    [Export] public PackedScene EnvironmentScene { get; set; }
}
