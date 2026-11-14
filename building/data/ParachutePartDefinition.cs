using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Parachute for the final descent. Cannot deploy while a fairing is still attached.</summary>
[GlobalClass]
public partial class ParachutePartDefinition : PartDefinition
{
    /// <summary>Drag area when deployed, in m^2. Already multiplied by the drag coefficient.</summary>
    [ExportGroup("Parachute")]
    [Export] public float DeployedDragArea { get; set; } = 80f;

    /// <summary>Dynamic pressure above which deployment fails, in Pa.</summary>
    [Export] public float MaxDeployDynamicPressure { get; set; } = 1500f;

    /// <summary>Mach number above which deployment fails.</summary>
    [Export] public float MaxDeployMach { get; set; } = 2.5f;

    /// <summary>Force the parachute joint can carry before it breaks, in N. Added to the guide (Section 7 lists it for chute_p1).</summary>
    [Export] public float AttachmentStrength { get; set; } = 150_000f;

    /// <summary>Sets the group to Parachute so new definitions are valid without extra setup.</summary>
    public ParachutePartDefinition() => PartGroup = PartGroup.Parachute;
}
