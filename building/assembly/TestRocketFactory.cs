using System.Collections.Generic;
using Godot;
using RocketGame.Core;
using Side = RocketGame.Core.Side;

namespace RocketGame.Building;

/// <summary>
/// Ready-made rockets for B's and C's sandboxes, built without the build screen.
/// Definitions come from PartCatalog when the .tres exists; otherwise from the fallback values below
/// (A_BUILDING.md 7). This is the only place allowed to hold part stats in code.
/// </summary>
public static class TestRocketFactory
{
    /// <summary>Robot payload used when no mission is available (kg).</summary>
    public const float DefaultPayloadMassKg = 300f;

    /// <summary>Column x of the test rockets (center of the 9-wide grid).</summary>
    private const int ColumnX = 4;

    /// <summary>
    /// One column: pod_mk1 (imu_g1, battery_b1) on tank_s1 on engine_lv1. Stage 0 arms the engine.
    /// Cells: engine (4,0), tank (4,1)-(4,2), pod (4,3).
    /// </summary>
    public static AssembledRocket CreateMinimal()
    {
        var tree = new PartTree();

        var pod = (CommandPodPart)RocketPart.Create(GetPart("pod_mk1"));
        var tank = (FuelTankPart)RocketPart.Create(GetPart("tank_s1"));
        var engine = (LiquidEnginePart)RocketPart.Create(GetPart("engine_lv1"));

        pod.Cell = new Vector2I(ColumnX, 3);
        tank.Cell = new Vector2I(ColumnX, 1);
        engine.Cell = new Vector2I(ColumnX, 0);

        tree.RootPart = pod;
        tree.Add(pod);
        tree.Add(tank);
        tree.Add(engine);

        PartTree.Link(pod, tank, MakeJoint(new Edge(new Vector2I(ColumnX, 2), Side.Top), 1));
        PartTree.Link(tank, engine, MakeJoint(new Edge(new Vector2I(ColumnX, 0), Side.Top), 2));

        pod.PayloadMassKg = DefaultPayloadMassKg;
        InstallModule(pod, "imu_g1", new Vector2I(0, 0));
        InstallModule(pod, "battery_b1", new Vector2I(1, 0));

        engine.FeedTanks.Add(tank);

        var stage0 = new StageGroup { StageIndex = 0, Depth = 0 };
        stage0.EnginesToArm.Add(engine);

        new RocketAssembler().AttachVisuals(tree);
        return new AssembledRocket(tree, new List<StageGroup> { stage0 }, MassCenterCalculator.Calculate(tree.ActiveParts));
    }

    /// <summary>The sample Mars stack (A_BUILDING.md 5.4) through the real RocketAssembler: 19 parts, 5 stages. Needs PartCatalog.</summary>
    public static AssembledRocket CreateMarsStack()
    {
        if (PartCatalog.Instance == null)
        {
            GD.PushError("[Building] TestRocketFactory.CreateMarsStack needs the PartCatalog autoload (.tres definitions)");
            return null;
        }
        return new RocketAssembler().AssembleRocket(SampleDesigns.CreateMarsStack());
    }

    /// <summary>Part definition from the catalog, or the fallback for tests.</summary>
    public static PartDefinition GetPart(string modelId) =>
        PartCatalog.Instance?.GetDefinition(modelId) as PartDefinition ?? CreateFallbackPart(modelId);

    /// <summary>Module definition from the catalog, or the fallback for tests.</summary>
    public static ModuleDefinition GetModule(string modelId) =>
        PartCatalog.Instance?.GetDefinition(modelId) as ModuleDefinition ?? CreateFallbackModule(modelId);

    private static Joint MakeJoint(Edge edge, int depth)
    {
        var joint = new Joint { Depth = depth };
        joint.ContactEdges.Add(edge);
        return joint;
    }

    private static void InstallModule(CommandPodPart pod, string modelId, Vector2I cell)
    {
        PodModule module = PodModule.Create(GetModule(modelId));
        if (module == null) return;
        module.GridCell = cell;
        pod.AddModule(module);
    }

    private static PartDefinition CreateFallbackPart(string modelId)
    {
        const PartGroupMask podTop = PartGroupMask.Fairing | PartGroupMask.Parachute;
        const PartGroupMask stackBelow = PartGroupMask.CoreTank | PartGroupMask.Structural | PartGroupMask.HeatShield;

        switch (modelId)
        {
            case "pod_mk1":
                return new CommandPodPartDefinition
                {
                    ModelId = modelId, DisplayName = "Pod Mk1", BuildWeight = 1, DryMass = 600f, PowerDrawW = 40f,
                    InventorySize = new Vector2I(2, 2), MaxGForce = 8f,
                    DragCoefficient = 0.4f, ImpactToleranceMps = 8f, SideArea = 4f,
                    AllowedTop = podTop, AllowedBottom = stackBelow,
                };
            case "tank_s1":
                return new FuelTankPartDefinition
                {
                    ModelId = modelId, DisplayName = "Tank S1", BuildWeight = 2, DryMass = 150f,
                    Footprint = new Vector2I(1, 2), PropellantType = FuelType.Kerolox, FuelCapacity = 1000f,
                    DragCoefficient = 0.1f, SideArea = 8f,
                    AllowedTop = PartGroupMask.CommandPod | PartGroupMask.CoreTank | PartGroupMask.LiquidEngine | stackBelow,
                    AllowedBottom = PartGroupMask.CoreTank | PartGroupMask.LiquidEngine | PartGroupMask.Structural | PartGroupMask.HeatShield,
                    AllowedSides = PartGroupMask.SolidBooster | PartGroupMask.AeroSurface,
                };
            case "engine_lv1":
                return new LiquidEnginePartDefinition
                {
                    ModelId = modelId, DisplayName = "Engine LV1", BuildWeight = 2, DryMass = 300f,
                    PropellantType = FuelType.Kerolox, MaxThrustVac = 140_000f, IspSeaLevel = 270f, IspVacuum = 300f,
                    GimbalRangeDeg = 5f, MinThrottle = 0.5f, MaxIgnitions = 1, SideArea = 4f,
                    AllowedTop = PartGroupMask.CoreTank, AllowedBottom = stackBelow,
                };
            default:
                GD.PushError($"[Building] TestRocketFactory: no fallback for part '{modelId}'");
                return null;
        }
    }

    private static ModuleDefinition CreateFallbackModule(string modelId)
    {
        switch (modelId)
        {
            case "imu_g1":
                return new AcsModuleDefinition
                {
                    ModelId = modelId, DisplayName = "IMU G1", BuildWeight = 1, DryMass = 15f, PowerDrawW = 20f,
                    Kind = AcsDeviceKind.Imu, SensorAccuracyArcsec = 1800f, DriftRateDegPerMin = 0.2f,
                    ResponseLatencyMs = 80f, EnabledSasModes = SasModeMask.StabilityAssist,
                };
            case "battery_b1":
                return new PowerModuleDefinition
                {
                    ModelId = modelId, DisplayName = "Battery B1", BuildWeight = 1, DryMass = 20f,
                    Kind = PowerDeviceKind.Battery, EnergyStorageWh = 500f,
                };
            default:
                GD.PushError($"[Building] TestRocketFactory: no fallback for module '{modelId}'");
                return null;
        }
    }
}
