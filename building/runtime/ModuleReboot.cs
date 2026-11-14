using Godot;
using RocketGame.Core;
using RocketGame.Physics;

namespace RocketGame.Building;

/// <summary>
/// Phase 2 (PHASE2_PLAN.md 5.7): reboot of Broken pod modules. Each attempt costs RebootEnergyWh from the batteries,
/// succeeds with RebootSuccessChance and puts that module on a cooldown. Used by the flight and cruise (key B) and the cruise screen.
/// </summary>
public static class ModuleReboot
{
    /// <summary>Seconds before the same module can be tried again.</summary>
    public static float CooldownSeconds { get; set; } = 8f;

    private static readonly RandomNumberGenerator SharedRng = new();

    /// <summary>Tries the first Broken module that is off cooldown. Returns a HUD line, or null when nothing is broken.</summary>
    public static string TryRebootNext(CommandPodPart pod, RandomNumberGenerator rng = null, float nowSeconds = -1f)
    {
        if (pod == null) return null;
        rng ??= SharedRng;
        if (nowSeconds < 0f) nowSeconds = Time.GetTicksMsec() / 1000f;
        PodModule waiting = null;
        foreach (PodModule m in pod.Modules)
        {
            if (m.State != ComponentState.Broken) continue;
            if (nowSeconds < m.RebootReadyAtS) { waiting ??= m; continue; }
            return Attempt(pod, m, rng, nowSeconds);
        }
        return waiting != null ? $"{NameOf(waiting)}: chờ {waiting.RebootReadyAtS - nowSeconds:0} s để khởi động lại" : null;
    }

    /// <summary>One attempt on this module (energy, chance, cooldown).</summary>
    public static string Attempt(CommandPodPart pod, PodModule m, RandomNumberGenerator rng, float nowSeconds)
    {
        float wh = m.Definition?.RebootEnergyWh ?? 0f;
        if (!PowerSolver.TryDrawEnergy(pod, wh)) return $"{NameOf(m)}: không đủ điện ({wh:0} Wh) để khởi động lại";
        m.RebootReadyAtS = nowSeconds + CooldownSeconds;
        bool ok = m.TryReboot(rng);
        return ok ? $"{NameOf(m)}: khởi động lại THÀNH CÔNG" : $"{NameOf(m)}: khởi động lại thất bại (thử lại sau {CooldownSeconds:0} s)";
    }

    public static int CountBroken(CommandPodPart pod)
    {
        int n = 0;
        if (pod != null) foreach (PodModule m in pod.Modules) if (m.State == ComponentState.Broken) n++;
        return n;
    }

    private static string NameOf(PodModule m) =>
        string.IsNullOrEmpty(m.Definition?.DisplayName) ? m.Definition?.ModelId ?? "module" : m.Definition.DisplayName;
}
