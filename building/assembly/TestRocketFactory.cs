namespace RocketGame.Building;

/// <summary>Ready-made rockets for B's and C's sandboxes, built without the build screen.</summary>
public static class TestRocketFactory
{
    /// <summary>One column: pod_mk1 (imu_g1, battery_b1), tank_s1, engine_lv1; stage 0 arms the engine. Hard deadline H3.</summary>
    public static AssembledRocket CreateMinimal()
    {
        // TODO(A2)
        return null;
    }

    /// <summary>The sample Mars stack through the real RocketAssembler (5 stages). Deadline H6.</summary>
    public static AssembledRocket CreateMarsStack()
    {
        // TODO(A4): new RocketAssembler().AssembleRocket(SampleDesigns.CreateMarsStack())
        return null;
    }
}
