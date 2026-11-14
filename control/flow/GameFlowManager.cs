using Godot;

namespace RocketGame.Control;

/// <summary>Autoload: game state machine and scene switching. Stub from C0; implemented in C2.</summary>
public partial class GameFlowManager : Node
{
    public static GameFlowManager Instance { get; private set; }

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }
}
