using Godot;

namespace RocketGame.Core;

public static class NodeGuard
{
    /// <summary>Returns false and disables the node when a required export is missing.</summary>
    public static bool Require(Node owner, GodotObject value, string exportName)
    {
        if (value != null) return true;
        GD.PushError($"[{owner.GetType().Name}] Missing required export '{exportName}' on {owner.GetPath()}");
        owner.ProcessMode = Node.ProcessModeEnum.Disabled;
        return false;
    }
}
