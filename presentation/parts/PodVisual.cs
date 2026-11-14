using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Presentation;

/// <summary>Command pod: RCS puffs, deployables, robot payload.</summary>
public partial class PodVisual : PartVisual
{
    public void UpdateRcs(float torqueCommand, float translateCommand)
    {
        // TODO(A8)
    }

    public void UpdateDeployables(bool solarDeployed, bool radiatorExtended)
    {
        // TODO(A8)
    }

    public void ShowPayload(PackedScene payloadScene)
    {
        // TODO(A8)
    }
}
