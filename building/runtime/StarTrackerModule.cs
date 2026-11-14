using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>Star tracker: precise, blinded inside the atmosphere.</summary>
public class StarTrackerModule : AttitudeSensorModule
{
    /// <summary>True below the blinding altitude (Control sets it).</summary>
    public bool IsBlinded { get; set; }

    public override bool CanOperate() => base.CanOperate() && !IsBlinded;

    /// <summary>Re-acquires attitude after a reset (P2 flavour; no effect for now).</summary>
    public void SolveLostInSpace() { }
}
