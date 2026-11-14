using Godot;
using RocketGame.Core;
using Side = RocketGame.Core.Side;

namespace RocketGame.Building;

/// <summary>Hand-written designs used by tests and as a reference build.</summary>
public static class SampleDesigns
{
    public const string DecouplerId = "decoupler_d1";

    /// <summary>Mars stack of A_BUILDING.md 5.4 (center column x = 4, y = 0 at the bottom).</summary>
    public static RocketDesign CreateMarsStack()
    {
        var design = new RocketDesign();

        // Center column, top to bottom.
        design.AddPart("fairing_n1", new Vector2I(4, 12));
        design.AddPart("chute_p1", new Vector2I(4, 11));
        design.AddPart("pod_mk2", new Vector2I(4, 10));
        design.AddPart("tank_lm1", new Vector2I(4, 9));
        design.AddPart("engine_land1", new Vector2I(4, 8));
        design.AddPart("heatshield_h1", new Vector2I(4, 7));
        design.AddPart("tank_h1", new Vector2I(4, 5));
        design.AddPart("engine_vac1", new Vector2I(4, 4));
        design.AddPart("tank_m1", new Vector2I(4, 1));
        design.AddPart("engine_lv2", new Vector2I(4, 0));

        // Side boosters and fins.
        design.AddPart("booster_m1", new Vector2I(3, 1));
        design.AddPart("booster_m1", new Vector2I(5, 1));
        design.AddPart("fin_f1", new Vector2I(2, 1));
        design.AddPart("fin_f1", new Vector2I(6, 1));

        // Decouplers.
        design.AddDecoupler(DecouplerId, new Edge(new Vector2I(4, 7), Side.Top));
        design.AddDecoupler(DecouplerId, new Edge(new Vector2I(4, 6), Side.Top));
        design.AddDecoupler(DecouplerId, new Edge(new Vector2I(4, 3), Side.Top));
        design.AddDecoupler(DecouplerId, new Edge(new Vector2I(3, 2), Side.Right));
        design.AddDecoupler(DecouplerId, new Edge(new Vector2I(4, 2), Side.Right));

        // pod_mk2 modules (slots A2 / T1 / P2).
        // pod_mk2 grid 3x3: row 0 = IMU, wheel, insulation; battery B2 and solar S1 are 1x2 below.
        design.AddModule("imu_g2", new Vector2I(0, 0));
        design.AddModule("wheel_rw1", new Vector2I(1, 0));
        design.AddModule("insulation_i1", new Vector2I(2, 0));
        design.AddModule("battery_b2", new Vector2I(0, 1));
        design.AddModule("solar_s1", new Vector2I(1, 1));

        return design;
    }
}
