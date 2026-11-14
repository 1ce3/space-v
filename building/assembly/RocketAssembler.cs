using System.Collections.Generic;
using Godot;
using RocketGame.Core;
using RocketGame.Research;
using Side = RocketGame.Core.Side;

namespace RocketGame.Building;

/// <summary>Turns a RocketDesign into the runtime rocket (A_BUILDING.md 5.3). Always creates fresh parts.</summary>
public class RocketAssembler
{
    /// <summary>Use the tech tree max weight in ValidateConnections.</summary>
    public const int MaxWeightFromTechTree = -1;
    /// <summary>Skip the weight check in ValidateConnections.</summary>
    public const int NoWeightLimit = int.MaxValue;

    /// <summary>Grid of the last AssembleRuntimeTree (parts and decouplers by cell/edge).</summary>
    public BuildGrid LastGrid { get; private set; }

    private readonly Dictionary<RocketPart, int> _depth = new();

    // ------------------------------------------------------------------ validation

    /// <summary>Player-facing errors (Vietnamese). Empty = valid. Weight limit from the tech tree.</summary>
    public List<string> ValidateConnections(RocketDesign design) => ValidateConnections(design, MaxWeightFromTechTree);

    /// <summary>
    /// Player-facing errors (Vietnamese) for the rules of A_BUILDING.md 5.2. Empty = valid.
    /// maxWeight: MaxWeightFromTechTree (tech tree, skipped if unavailable), NoWeightLimit, or a number.
    /// </summary>
    public List<string> ValidateConnections(RocketDesign design, int maxWeight)
    {
        var errors = new List<string>();
        if (design == null || design.PartEntries.Count == 0)
        {
            errors.Add("Tên lửa chưa có part nào.");
            return errors;
        }

        BuildGrid grid = NewGrid();
        var parts = new List<RocketPart>();
        int weight = 0;

        try
        {
            // Parts: known ids, legal cells and neighbors (checked in order; the rule is symmetric).
            foreach (RocketDesign.PartEntry entry in design.PartEntries)
            {
                if (GetDefinition(entry.ModelId) is not PartDefinition def || def is DecouplerPartDefinition)
                {
                    errors.Add($"Không có part '{entry.ModelId}'.");
                    continue;
                }
                weight += def.BuildWeight + (PartCatalog.Instance?.ResolveMaterial(def, entry.MaterialId)?.BuildWeightCost ?? 0);
                if (!grid.CanPlace(def, entry.Cell))
                {
                    errors.Add($"{Name(def)} ở ô {CellText(entry.Cell)} đặt sai chỗ (chồng ô, ra ngoài lưới hoặc hàng xóm không hợp lệ).");
                    continue;
                }
                RocketPart part = RocketPart.Create(def);
                part.Cell = entry.Cell;
                grid.Occupy(part);
                parts.Add(part);
            }

            // Exactly one pod.
            var pods = parts.FindAll(p => p is CommandPodPart);
            if (pods.Count == 0) errors.Add("Cần đúng một khoang điều khiển (command pod).");
            else if (pods.Count > 1) errors.Add($"Chỉ được có một khoang điều khiển (đang có {pods.Count}).");

            // At least one engine.
            if (!parts.Exists(p => p is EnginePart)) errors.Add("Cần ít nhất một động cơ hoặc tên lửa đẩy.");

            // Everything connected to the pod.
            if (pods.Count >= 1)
            {
                var reached = new HashSet<RocketPart> { pods[0] };
                var queue = new Queue<RocketPart>();
                queue.Enqueue(pods[0]);
                while (queue.Count > 0)
                    foreach (RocketPart n in grid.GetNeighbors(queue.Dequeue()))
                        if (reached.Add(n)) queue.Enqueue(n);
                foreach (RocketPart part in parts)
                    if (!reached.Contains(part)) errors.Add($"{Name(part.Definition)} ở ô {CellText(part.Cell)} không nối với khoang điều khiển.");
            }

            // Liquid engines touch a tank of their propellant.
            foreach (RocketPart part in parts)
            {
                if (part is not LiquidEnginePart engine) continue;
                FuelType fuel = engine.LiquidDefinition?.PropellantType ?? FuelType.Kerolox;
                bool fed = grid.GetNeighbors(engine).Exists(n => n is FuelTankPart tank && tank.PropellantType == fuel);
                if (!fed) errors.Add($"{Name(part.Definition)} ở ô {CellText(part.Cell)} phải chạm trực tiếp một bình {fuel}.");
            }

            // Decouplers on valid contact edges, one per edge.
            foreach (RocketDesign.DecouplerEntry entry in design.DecouplerEntries)
            {
                if (GetDefinition(entry.ModelId) is not DecouplerPartDefinition def)
                {
                    errors.Add($"Không có bộ tách tầng '{entry.ModelId}'.");
                    continue;
                }
                weight += def.BuildWeight + (PartCatalog.Instance?.ResolveMaterial(def, entry.MaterialId)?.BuildWeightCost ?? 0);
                if (!grid.CanPlaceDecoupler(def, entry.Edge))
                {
                    errors.Add($"Bộ tách tầng ở cạnh {EdgeText(entry.Edge)} không nằm giữa hai part tiếp xúc (hoặc bị trùng).");
                    continue;
                }
                var decoupler = (DecouplerPart)RocketPart.Create(def);
                grid.AddDecoupler(decoupler, entry.Edge);
                parts.Add(decoupler);
            }

            // At most one fairing, parachute, heat shield.
            CheckMaxOne<FairingPart>(parts, "chóp bảo vệ (fairing)", errors);
            CheckMaxOne<ParachutePart>(parts, "dù", errors);
            CheckMaxOne<HeatShieldPart>(parts, "khiên nhiệt", errors);

            // Modules: inside the pod inventory grid, no two modules on the same cell (Unplaced = first free cell).
            var pod = pods.Count > 0 ? (CommandPodPart)pods[0] : null;
            var occupied = new List<PodInventory.Item>();
            foreach (RocketDesign.ModuleEntry entry in design.ModuleEntries)
            {
                if (GetDefinition(entry.ModelId) is not ModuleDefinition def)
                {
                    errors.Add($"Không có module '{entry.ModelId}'.");
                    continue;
                }
                weight += def.BuildWeight;
                if (pod == null)
                {
                    errors.Add($"Module {Name(def)} cần có khoang điều khiển.");
                    continue;
                }
                Vector2I size = PodInventory.SizeOf(def);
                Vector2I inventory = pod.InventorySize;
                Vector2I? cell = entry.Cell == PodInventory.Unplaced ? PodInventory.FindFreeCell(inventory, occupied, size) : entry.Cell;
                if (cell == null || !PodInventory.InBounds(inventory, cell.Value, size))
                    errors.Add($"Module {Name(def)} ({size.X}x{size.Y} ô) không vừa lưới {inventory.X}x{inventory.Y} của {Name(pod.Definition)}.");
                else if (!PodInventory.Fits(inventory, occupied, cell.Value, size))
                    errors.Add($"Module {Name(def)} chồng lên module khác ở ô ({cell.Value.X}, {cell.Value.Y}).");
                else
                    occupied.Add(new PodInventory.Item(cell.Value, size));
            }

            // Weight.
            int max = maxWeight == MaxWeightFromTechTree ? GetTechTreeMaxWeight() : maxWeight;
            if (weight > max) errors.Add($"Tổng weight {weight} vượt giới hạn {max}.");
        }
        finally
        {
            foreach (RocketPart part in parts) part.Free();
        }
        return errors;
    }

    // ------------------------------------------------------------------ assembly

    /// <summary>Creates parts, BFS from the pod, joints with decouplers. Sets Cell, Tree, ParentPart, ParentJoint.</summary>
    public PartTree AssembleRuntimeTree(RocketDesign design)
    {
        var tree = new PartTree();
        BuildGrid grid = NewGrid();
        LastGrid = grid;
        _depth.Clear();
        if (design == null) return tree;

        // 1. Fresh parts on a grid.
        var parts = new List<RocketPart>();
        foreach (RocketDesign.PartEntry entry in design.PartEntries)
        {
            if (GetDefinition(entry.ModelId) is not PartDefinition def || def is DecouplerPartDefinition)
            {
                GD.PushError($"[Building] RocketAssembler: unknown part '{entry.ModelId}'");
                continue;
            }
            RocketPart part = RocketPart.Create(def);
            part.Material = PartCatalog.Instance?.ResolveMaterial(def, entry.MaterialId);
            part.Cell = entry.Cell;
            grid.Occupy(part);
            parts.Add(part);
        }
        foreach (RocketDesign.DecouplerEntry entry in design.DecouplerEntries)
        {
            if (GetDefinition(entry.ModelId) is not DecouplerPartDefinition def)
            {
                GD.PushError($"[Building] RocketAssembler: unknown decoupler '{entry.ModelId}'");
                continue;
            }
            var decoupler = (DecouplerPart)RocketPart.Create(def);
            decoupler.Material = PartCatalog.Instance?.ResolveMaterial(def, entry.MaterialId);
            if (!grid.AddDecoupler(decoupler, entry.Edge))
            {
                GD.PushWarning($"[Building] RocketAssembler: second decoupler on edge {entry.Edge} ignored");
                decoupler.Free();
                continue;
            }
            decoupler.Cell = decoupler.Edge.Cell;
        }

        RocketPart pod = parts.Find(p => p is CommandPodPart);
        if (pod == null)
        {
            GD.PushError("[Building] RocketAssembler: design has no command pod");
            FreeAll(parts, grid.Decouplers);
            return tree;
        }

        // 2-4. Layered BFS from the pod. A part found from several parts of the same layer takes as parent
        // the one whose shared edge has a decoupler, else the earliest visited.
        tree.RootPart = pod;
        tree.Add(pod);
        _depth[pod] = 0;
        var visitOrder = new Dictionary<RocketPart, int> { [pod] = 0 };
        var layer = new List<RocketPart> { pod };
        int depth = 0;
        while (layer.Count > 0)
        {
            depth++;
            var next = new List<RocketPart>();
            var parentOf = new Dictionary<RocketPart, RocketPart>();
            foreach (RocketPart parent in layer)
            {
                foreach (RocketPart child in grid.GetNeighbors(parent))
                {
                    if (visitOrder.ContainsKey(child)) continue;
                    if (!parentOf.TryGetValue(child, out RocketPart current))
                    {
                        parentOf[child] = parent;
                        next.Add(child);
                    }
                    else if (grid.GetDecouplersBetween(current, child).Count == 0 && grid.GetDecouplersBetween(parent, child).Count > 0)
                    {
                        parentOf[child] = parent;
                    }
                }
            }

            foreach (RocketPart child in next)
            {
                visitOrder[child] = visitOrder.Count;
                _depth[child] = depth;
                RocketPart parent = parentOf[child];
                tree.Add(child);
                PartTree.Link(parent, child, MakeJoint(grid, parent, child, depth, tree));
            }
            layer = next;
        }

        // Parts not reached (validation reports them) and decouplers off the tree are dropped.
        foreach (RocketPart part in parts)
        {
            if (visitOrder.ContainsKey(part)) continue;
            GD.PushWarning($"[Building] RocketAssembler: {part.ModelId} at {part.Cell} is not connected to the pod, dropped");
            grid.Vacate(part);
            part.Free();
        }
        foreach (DecouplerPart decoupler in grid.Decouplers)
        {
            if (decoupler.Joint != null) continue;
            GD.PushWarning($"[Building] RocketAssembler: decoupler on {decoupler.Edge} is not on a tree joint, dropped");
            grid.RemoveDecoupler(decoupler.Edge);
            decoupler.Free();
        }

        // Node3D positions in the vehicle-local frame (m, z = 0).
        foreach (RocketPart part in tree.ActiveParts)
        {
            Vector2 local = part.GetLocalPosition();
            part.Position = new Vector3(local.X, local.Y, 0f);
        }
        return tree;
    }

    /// <summary>Creates PodModules from the design and installs them in the pod. Also sets the mission payload.</summary>
    public void InstallModules(RocketDesign design, CommandPodPart pod)
    {
        if (pod == null) return;
        pod.PayloadMassKg = ResearchCampaignEngine.Instance?.CurrentMission?.PayloadMassKg ?? TestRocketFactory.DefaultPayloadMassKg;
        if (design == null) return;

        foreach (RocketDesign.ModuleEntry entry in design.ModuleEntries)
        {
            if (GetDefinition(entry.ModelId) is not ModuleDefinition def)
            {
                GD.PushError($"[Building] RocketAssembler: unknown module '{entry.ModelId}'");
                continue;
            }
            Vector2I? cell = entry.Cell == PodInventory.Unplaced
                ? PodInventory.FindFreeCell(pod.InventorySize, pod.GetOccupiedCells(), PodInventory.SizeOf(def))
                : entry.Cell;
            if (cell == null || !pod.CanPlaceModule(def, cell.Value))
            {
                GD.PushWarning($"[Building] RocketAssembler: module '{entry.ModelId}' does not fit the pod grid, skipped");
                continue;
            }
            PodModule module = PodModule.Create(def);
            if (module == null) continue;
            module.GridCell = cell.Value;
            pod.AddModule(module);
        }
    }

    /// <summary>Fills FeedTanks of each liquid engine (same propellant, not across decoupler joints).</summary>
    public void LinkFeedTanks(PartTree tree)
    {
        if (tree == null) return;
        var seen = new HashSet<RocketPart>();
        var stack = new Stack<RocketPart>();
        foreach (LiquidEnginePart engine in tree.GetParts<LiquidEnginePart>())
        {
            engine.FeedTanks.Clear();
            FuelType fuel = engine.LiquidDefinition?.PropellantType ?? FuelType.Kerolox;
            seen.Clear();
            stack.Clear();
            stack.Push(engine);
            seen.Add(engine);
            while (stack.Count > 0)
            {
                RocketPart part = stack.Pop();
                if (part is FuelTankPart tank && tank.PropellantType == fuel) engine.FeedTanks.Add(tank);

                // Up, unless the joint to the parent has a decoupler.
                if (part.ParentPart != null && part.ParentJoint != null && part.ParentJoint.Decouplers.Count == 0 && seen.Add(part.ParentPart))
                    stack.Push(part.ParentPart);
                // Down, unless the child's joint has a decoupler.
                foreach (RocketPart child in part.Children)
                    if (child.ParentJoint != null && child.ParentJoint.Decouplers.Count == 0 && seen.Add(child))
                        stack.Push(child);
            }
        }
    }

    /// <summary>Instances each part's visual scene (or the group placeholder) and binds it. Parts without a scene stay invisible.</summary>
    public void AttachVisuals(PartTree tree)
    {
        if (tree == null) return;
        foreach (RocketPart part in tree.ActiveParts) AttachVisual(part);
    }

    /// <summary>Adds the visual of one part as child "Visual" (skipped if it already has one). Returns the visual or null.</summary>
    public static Node AttachVisual(RocketPart part)
    {
        if (part == null) return null;
        if (part.HasNode("Visual")) return part.GetNode("Visual");
        PackedScene scene = GetVisualScene(part.Definition as PartDefinition);
        if (scene == null) return null;

        Node visual = scene.Instantiate();
        visual.Name = "Visual";
        part.AddChild(visual);
        // Called by name: Building never references the presentation classes (AGENTS 1.5).
        if (visual.HasMethod(PartCatalog.BindMethod)) visual.Call(PartCatalog.BindMethod, part);
        else GD.PushWarning($"[Building] RocketAssembler: scene of {part.ModelId} has no PartVisual root");
        return visual;
    }

    /// <summary>Own scene of the definition, else the group (or decoupler) placeholder.</summary>
    public static PackedScene GetVisualScene(PartDefinition def)
    {
        if (def == null) return null;
        if (def.Scene != null) return def.Scene;
        PartCatalog catalog = PartCatalog.Instance;
        if (catalog == null) return null;
        return def is DecouplerPartDefinition ? catalog.GetDecouplerPlaceholderScene() : catalog.GetPlaceholderScene(def.PartGroup);
    }

    /// <summary>Stage 0 plus one stage per decoupler depth (deepest first), with Parts and EnginesToArm.</summary>
    public List<StageGroup> DeriveStages(PartTree tree)
    {
        var stages = new List<StageGroup> { new() { StageIndex = 0, Depth = 0 } };
        if (tree?.RootPart == null) return stages;

        // 5. Joints with decouplers (not self-separating), grouped by depth, deepest first.
        var byDepth = new SortedDictionary<int, StageGroup>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
        foreach (RocketPart part in tree.ActiveParts)
        {
            Joint joint = part.ParentJoint;
            if (joint == null || joint.ChildPart != part || joint.Decouplers.Count == 0 || joint.IsSelfSeparating) continue;
            if (!byDepth.TryGetValue(joint.Depth, out StageGroup stage))
            {
                stage = new StageGroup { Depth = joint.Depth };
                byDepth[joint.Depth] = stage;
            }
            stage.Joints.Add(joint);
            foreach (RocketPart p in tree.GetSubtree(part))
                if (!stage.Parts.Contains(p)) stage.Parts.Add(p);
        }
        foreach (StageGroup stage in byDepth.Values)
        {
            stage.StageIndex = stages.Count;
            stages.Add(stage);
        }

        // 6. An engine is armed by the first stage after which nothing is attached below it.
        BuildGrid grid = LastGrid;
        var attached = new HashSet<RocketPart>(tree.ActiveParts);
        var pending = new List<EnginePart>(tree.GetParts<EnginePart>());
        foreach (StageGroup stage in stages)
        {
            foreach (RocketPart p in stage.Parts) attached.Remove(p);
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                EnginePart engine = pending[i];
                if (!attached.Contains(engine))
                {
                    pending.RemoveAt(i); // dropped before it could fire
                    continue;
                }
                if (HasAttachedBelow(engine, attached, grid)) continue;
                stage.EnginesToArm.Add(engine);
                pending.RemoveAt(i);
            }
            stage.EnginesToArm.Reverse(); // keep tree order
        }
        foreach (EnginePart engine in pending)
            GD.PushWarning($"[Building] RocketAssembler: {engine.ModelId} at {engine.Cell} is never clear below, never armed");
        return stages;
    }

    /// <summary>Full pipeline: tree, modules, feed tanks, stages, shielding, visuals, mass properties.</summary>
    public AssembledRocket AssembleRocket(RocketDesign design)
    {
        PartTree tree = AssembleRuntimeTree(design);
        if (tree.RootPart is CommandPodPart pod) InstallModules(design, pod);
        LinkFeedTanks(tree);
        List<StageGroup> stages = DeriveStages(tree);
        LinkShielding(tree);
        AttachVisuals(tree);
        return new AssembledRocket(tree, stages, MassCenterCalculator.Calculate(tree.ActiveParts));
    }

    /// <summary>9. Fairing and heat shield protect their chain of parents up to the pod.</summary>
    public void LinkShielding(PartTree tree)
    {
        if (tree == null) return;
        foreach (FairingPart fairing in tree.GetParts<FairingPart>())
        {
            fairing.ShieldedParts.Clear();
            for (RocketPart p = fairing.ParentPart; p != null; p = p.ParentPart) fairing.ShieldedParts.Add(p);
        }
        foreach (HeatShieldPart shield in tree.GetParts<HeatShieldPart>())
        {
            shield.ShieldedParts.Clear();
            for (RocketPart p = shield.ParentPart; p != null; p = p.ParentPart) shield.ShieldedParts.Add(p);
        }
    }

    /// <summary>Depth of a part from the pod in the last assembled tree, or -1.</summary>
    public int GetDepth(RocketPart part) => part != null && _depth.TryGetValue(part, out int d) ? d : -1;

    // ------------------------------------------------------------------ helpers

    private static Joint MakeJoint(BuildGrid grid, RocketPart parent, RocketPart child, int depth, PartTree tree)
    {
        var joint = new Joint
        {
            Depth = depth,
            IsSelfSeparating = child is FairingPart || child is ParachutePart,
        };
        foreach (Edge edge in grid.GetContactEdges(parent, child))
        {
            joint.ContactEdges.Add(edge);
            DecouplerPart decoupler = grid.GetDecouplerAt(edge);
            if (decoupler == null) continue;
            joint.Decouplers.Add(decoupler);
            decoupler.Joint = joint;
            tree.Add(decoupler);
            joint.Strength += (decoupler.DecouplerDefinition?.AttachmentStrength ?? 0f) * (decoupler.Material?.StrengthMultiplier ?? 1f);
        }
        if (joint.Decouplers.Count == 0)
        {
            joint.Strength = child.Definition switch
            {
                FairingPartDefinition f => f.AttachmentStrength,
                ParachutePartDefinition c => c.AttachmentStrength,
                _ => 0f,
            };
        }
        return joint;
    }

    private static bool HasAttachedBelow(RocketPart part, HashSet<RocketPart> attached, BuildGrid grid)
    {
        if (grid == null) return false;
        for (int x = 0; x < Mathf.Max(1, part.Footprint.X); x++)
        {
            RocketPart below = grid.GetPartAt(new Vector2I(part.Cell.X + x, part.Cell.Y - 1));
            if (below != null && below != part && attached.Contains(below)) return true;
        }
        return false;
    }

    private static void CheckMaxOne<T>(List<RocketPart> parts, string label, List<string> errors) where T : RocketPart
    {
        int count = parts.FindAll(p => p is T).Count;
        if (count > 1) errors.Add($"Chỉ được có một {label} (đang có {count}).");
    }

    private static int GetTechTreeMaxWeight()
    {
        TechTreeManager techTree = ResearchCampaignEngine.Instance?.TechTree;
        return techTree != null ? techTree.GetMaxBuildWeight() : NoWeightLimit;
    }

    private static ComponentDefinition GetDefinition(string modelId) => PartCatalog.Instance?.GetDefinition(modelId);

    private static BuildGrid NewGrid()
    {
        BuildSettings s = PartCatalog.Instance?.Settings;
        return new BuildGrid(s?.GridWidth ?? 9, s?.GridHeight ?? 16, s?.CellSize ?? BuildSettings.DefaultCellSize);
    }

    private static void FreeAll(List<RocketPart> parts, List<DecouplerPart> decouplers)
    {
        foreach (RocketPart p in parts) p.Free();
        foreach (DecouplerPart d in decouplers) d.Free();
    }

    private static string Name(ComponentDefinition def) =>
        def == null ? "?" : string.IsNullOrEmpty(def.DisplayName) ? def.ModelId : def.DisplayName;

    private static string CellText(Vector2I cell) => $"({cell.X}, {cell.Y})";

    private static string EdgeText(Edge edge)
    {
        Edge e = BuildGrid.Normalize(edge);
        return e.Side == Side.Top ? $"trên ô {CellText(e.Cell)}" : $"phải ô {CellText(e.Cell)}";
    }
}
