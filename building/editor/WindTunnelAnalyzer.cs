using System.Collections.Generic;

namespace RocketGame.Building;

/// <summary>Build-screen aero preview using B's AerodynamicSolver (P2).</summary>
public class WindTunnelAnalyzer
{
    /// <summary>Aero forces and stability for the given flow.</summary>
    public AeroReport Analyze(IReadOnlyList<RocketPart> parts, TestConditions conditions)
    {
        // TODO(A9): call AerodynamicSolver.Solve once B's physics is on main.
        return default;
    }
}
