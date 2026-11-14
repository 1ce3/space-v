# Dev_A_Guide.md — Comprehensive Implementation Specification (Dev A)

> **Source of Truth:** Consolidated from `AGENTS.md`, `docs/agents/A_BUILDING.md`, and project handoff notes. If a `.mmd` diagram conflicts with this guide, **this guide and `AGENTS.md` take precedence**.

---

## 1. Environment, Language & Coding Standards
- **Engine & Runtime:** Godot `4.7.2.stable.mono` (.NET 8, C#).
- **Language:** Strictly **English** for all C# identifiers, comments, XML docs, and Git commits (exception: player-facing validation strings returned by `ValidateConnections` are in Vietnamese).
- **Namespaces:**
  - `RocketGame.Building` for `building/`
  - `RocketGame.Presentation` for `presentation/parts/`
  - Always import `RocketGame.Core` (`Enums.cs`, `CoreTypes.cs`, `FlightMath.cs`, `NodeGuard.cs`).
- **Naming Conventions:**
  - `PascalCase`: Classes, structs, enums, interfaces (`I*`), methods, public properties, constants, enum members, and scene node names.
  - `_camelCase`: Private fields (e.g., `_partTree`).
  - `camelCase`: Local variables and parameters.
  - Booleans: Must begin with `Is`, `Has`, or `Can` (e.g., `IsArmed`, `IsShielded`, `CanOperate()`).
  - Signals: `[Signal] public delegate void <Name>EventHandler(...)`; handlers named `On<SignalName>`. Connect via C# events (`+=`) in `_Ready()` and disconnect (`-=`) in `_ExitTree()`.
  - Files & IDs: `.cs` filename must match the single public class inside it. Scene, `.tres`, asset files, and IDs (`ModelId`, `NodeId`, `BodyId`, `MissionId`) use `snake_case` matching the `.tres` filename (e.g., `engine_lv2` $\rightarrow$ `data/parts/engine_lv2.tres`).
- **Units & Coordinate Conventions:**
  - Strict SI units everywhere (`m`, `kg`, `s`, `N`, `K`, `rad`, `W`, `J`, `Pa`). Add unit suffixes (`AngleDeg`, `MassKg`, `SpeedMps`, `PowerW`, `TempK`, `MomentumNms`, `ImpulseNs`).
  - Public API angles are **degrees**; radians are used only inside internal math.
  - **Vehicle-local frame:** Origin `(0, 0)` is the lower-left corner of grid cell `(0, 0)`. `+X` = right/east; `+Y` = rocket nose (up); `+Z` = toward camera. `Cell.Y` increases **upward**. `RocketPart.Cell` is the lower-left cell of its footprint.
  - `GetGridPosition()`: Center of footprint in cell units (e.g., `Cell (2,3)` + `Footprint (1,2)` $\rightarrow$ `(2.5f, 4.0f)`). For decouplers, midpoint of the edge.
  - `GetLocalPosition()`: `GetGridPosition() * CellSize` in meters.
  - `SetGimbal(+θ)`: Rotates thrust vector **clockwise** by `θ` degrees from body `+Y`, generating `+TorqueZ` (counterclockwise pitch up/left) for an engine below the CoM.
  - `SetDeflection(+δ)`: Creates a `+X` aerodynamic force in body frame, generating `+TorqueZ` for a fin below the CoM.
- **Godot C# Rules:**
  - Every Godot-derived class must be `partial`.
  - Every external node reference and configurable numeric/color/asset parameter must be `[Export]`. Validate required exports in `_Ready()` with `NodeGuard.Require(this, Prop, nameof(Prop))`.
  - Never use string node paths (`GetNode("../../...")`). Use `[Export]` or autoload singletons (`PartCatalog.Instance`, `ResearchCampaignEngine.Instance`, `GameFlowManager.Instance`).
  - Never export `Dictionary` or enum arrays—use `[Flags]` bitmasks (`PartGroupMask`, `SasModeMask`, `FlightEventMask`).
  - Types passed through signals must inherit from `RefCounted` (`AssembledRocket`, `PartTree`, `MissionRun`, `FlightResult`, `ActiveAnomaly`).
  - Keep methods short ($\le 40$ lines). Give every public member a one-line `/// <summary>` XML doc.
  - Log with `GD.Print($"[Building] ...")`; gate per-tick logs behind `[Export] public bool DebugLog`.
  - Run `dotnet build` before every commit. Branch: `dev/a-building`, commit format: `[A] building: <summary>`.

---

## 2. Directory Ownership & System Boundaries

### Owned Directories (Only Edit These)
- `building/data/` — `ComponentDefinition`, `PartDefinition` + subclasses, `ModuleDefinition` + subclasses, `BuildSettings`
- `building/runtime/` — `IFlightComponent`, `ThermalStateRules`, `RocketPart` + 10 subclasses, `PodModule` + 11 subclasses, `Joint`, `PartTree`, `StageGroup`
- `building/assembly/` — `RocketAssembler`, `AssembledRocket`, `MassCenterCalculator`, `TestRocketFactory`, `SampleDesigns`
- `building/editor/` — `BuildingSystem`, `BuildGrid`, `BuildBudget`, `RocketDesign`, `PartCatalog`, `WindTunnelAnalyzer`
- `building/ui/` — Build screen UI scripts (`BuildScreenRoot`, palette, inspector/stats panels)
- `presentation/parts/` — `PartVisual` and its 8 derived classes
- `data/parts/`, `data/modules/`, `data/settings/build_settings.tres` — `.tres` content resources
- `assets/parts/_placeholder/` — 10 temporary primitive part/decoupler scenes
- `scenes/build_screen.tscn` — Build screen scene
- `tests/a/` — Dev A sandbox test scenes and scripts

### Strictly Forbidden
- Do **not** touch `core/`, `physics/`, `research/`, `control/`, `presentation/flight/`, `project.godot`, or any scene owned by Dev B or Dev C.
- Do **not** call Control from Building (emit `DesignConfirmed` from `BuildingSystem` and `ResearchRequested` from `BuildScreenRoot`).
- Do **not** reparent Godot nodes or spawn debris inside `PartTree.SeverSubtree` (Dev C handles reparenting in `VehicleRoot` upon receiving `PartSevered`).
- Do **not** write aerodynamic, thermal flux, or electrical simulation equations (Dev B owns those; `RocketPart.CalculateAero` remains a stub returning `Vector2.Zero`).
- Do **not** reference `PartVisual` or presentation nodes from `building/` (presentation is strictly read-only).

---

## 3. Detailed Task Roadmap (P0 $\rightarrow$ P1 $\rightarrow$ P2)

> **Status:** `A0` (Skeleton) is complete. **Immediate Priority:** Complete `A1` and `A2` before **H3 (Milestone M1)** to unblock Dev B and Dev C.

### Task A1: In-Flight Runtime Part & Module Classes (H1.5–H3, P0 — Hard Deadline H3)
Implement in `building/runtime/` (and `AssembledRocket.cs` in `building/assembly/`):
1. `IFlightComponent` interface
2. `ThermalStateRules` static evaluator
3. `RocketPart` (`partial class : Node3D, IFlightComponent`) + 10 derived classes + `RocketPart.Create(PartDefinition def)` factory
4. `PodModule` (plain C# class implementing `IFlightComponent`) + 11 derived classes + `PodModule.Create(ModuleDefinition def)` factory
5. `Joint` (plain C# class)
6. `PartTree` (`partial class : RefCounted`, emits `PartSevered`)
7. `StageGroup` (plain C# class)
8. `AssembledRocket` (`partial class : RefCounted`)
- **Verification (`tests/a/part_runtime_test.tscn`):** Manually instantiate a pod + tank + engine, call `Arm()`, `SetThrottle()`, `DrainFuel()`, and `SeverSubtree()`, and verify expected state/signal outputs. Merge to `main`.

### Task A2: Minimal Sample Rocket (H2.5–H3, P0 — Hard Deadline H3)
Implement `TestRocketFactory.CreateMinimal()` in `building/assembly/TestRocketFactory.cs`:
- Build **in code without using `BuildGrid`**: a 1-column stack of `pod_mk1` (with `imu_g1` and `battery_b1` installed), `tank_s1`, and `engine_lv1`.
- Wire `ParentPart`, `Children`, `ParentJoint`, `Tree`, and `FeedTanks` manually.
- Create Stage `0` containing `engine_lv1` in `EnginesToArm`.
- Compute `MassProperties` via `MassCenterCalculator.Calculate(...)`.
- Fetch definitions from `PartCatalog` if `.tres` files exist; otherwise instantiate definitions in code using Section 7 stats (only inside `TestRocketFactory`).
- **Verification:** Run `CreateMinimal()`, print total mass (`600 + 15 + 20 + 150 + 1000 + 300 = 2085 kg` + `300 kg` payload if mission active) and CoM without errors. Merge to `main` and notify Dev B & Dev C.

### Task A3: Grid, Design Persistence & Mass Calculator (H3–H4.5, P0)
- `BuildGrid`: `9 x 16` cell occupancy, `CanPlace(def, cell)`, `CanPlaceDecoupler(edge)`, `GetNeighbors(cell, footprint)`, `GetContactEdges(partA, partB)`.
- `RocketDesign`: Stores part placements, decoupler edges, and pod modules; JSON `Serialize()` / `Deserialize()`; `SaveCurrent()` / `LoadLast()` at `user://current_design.json`.
- `BuildBudget`: Sums `BuildWeight` across all placed parts, decouplers, and modules; compares against `ResearchCampaignEngine.Instance.TechTree.GetMaxBuildWeight()`.
- `MassCenterCalculator`: Calculates `TotalMass`, `CenterOfMass` (meters), and `MomentOfInertia` ($I = \sum \left[\frac{m(w^2 + h^2)}{12} + m \cdot d^2\right]$).
- **Verification:** Place all Mars stack items (Section 5.4) in code; confirm `CanPlace` returns `true` for valid cells, `false` when placing a pod under a tank, and budget sums match.

### Task A4: RocketAssembler & Mars Stack (H4.5–H6, P0 — Deadline H6)
- Implement `RocketAssembler` pipeline (`ValidateConnections`, `AssembleRuntimeTree`, `InstallModules`, `LinkFeedTanks`, `DeriveStages`, `AttachVisuals`, `AssembleRocket`).
- Implement `SampleDesigns.CreateMarsStack()` and `TestRocketFactory.CreateMarsStack()`.
- **Verification:** Sandbox prints 5 stages matching Section 5.4 (`Stage 0`: arm `engine_lv2` + 2 `booster_m1`; `Stage 1`: decouple 2 boosters at depth 7; `Stage 2`: decouple `tank_m1` at depth 6 & arm `engine_vac1`; `Stage 3`: decouple `tank_h1` at depth 4; `Stage 4`: decouple `heatshield_h1` at depth 3 & arm `engine_land1`) with accurate `FeedTanks`. Merge before H6 (Milestone M1).

### Task A5: Content `.tres`, Placeholder Scenes & PartCatalog (H6–H8, P0)
- Author all 30 `.tres` files in `data/parts/` and `data/modules/` plus `data/settings/build_settings.tres`.
- Build 10 placeholder scenes in `assets/parts/_placeholder/` (`command_pod.tscn`, `fairing.tscn`, `parachute.tscn`, `heat_shield.tscn`, `core_tank.tscn`, `liquid_engine.tscn`, `solid_booster.tscn`, `aero_surface.tscn`, `structural.tscn`, `decoupler.tscn`).
- Implement `PartCatalog` autoload (`building/editor/part_catalog.tscn`) and base `PartVisual` (`Bind`, `SetGhost`, `ApplyThermalOverlay`, `OnSevered`).
- **Verification:** `ValidateAgainstTechTree()` and `ValidateScenes()` pass with zero errors; Mars stack renders as a clean blockout.

### Task A6: Build Screen UI & Interaction (H8–H10.5, P0 — Deadline H10.5)
- Build `scenes/build_screen.tscn` with `BuildScreenRoot` (`[Export] BuildingSystem Building`, `[Signal] ResearchRequested`) and `BuildingSystem` (`[Signal] DesignConfirmed`).
- Orthographic `Camera3D` looking down `-Z`; grid line renderer; raycast mouse onto `z = 0` plane.
- Input bindings (already in `project.godot`): `build_place` (LMB), `build_remove` (RMB), `build_toggle_decoupler` (Tab), `build_cancel` (Esc).
- Left palette (Pod, Tank, Engine, Booster, EDL, Other, Module) showing icon, name, weight; Right panel showing weight `Current / Max`, total mass, CoM marker on grid, pod module slots, validation errors, and buttons (`Launch`, `Research`, `Clear all`).

### Task A7: Pod Module Slots, Orphan Decoupler Cleanup & Design Reload (H10.5–H12, P0)
- Interactive module slot assignment/removal on `CommandPodPart`; auto-remove orphaned decouplers and refund weight when adjacent parts are deleted/moved; auto-load `user://current_design.json` when entering `build_screen.tscn`.

### Task A8: Specialized Part Visuals (H12–H14, P1)
- Implement the 8 `PartVisual` subclasses (`EngineVisual`, `ParachuteVisual`, `FairingVisual`, `HeatShieldVisual`, `FinVisual`, `DecouplerVisual`, `PodVisual`, `TankVisual`).

### Task A9: Post-M3 Polish & Rebalance (H14–H16, P1/P2)
- Drag-and-drop `MovePart` (P1), hover tooltips (P1), value rebalance with Dev B (P1), `WindTunnelAnalyzer` (P2). Feature freeze at H16.

---

## 4. Class & Contract Specifications

### 4.1 Definitions (`building/data/`)
All definitions inherit `Resource`, have `[GlobalClass]`, are `partial`, and mark all data fields `[Export]`.
- **`ComponentDefinition` (abstract):** `string ModelId`, `string DisplayName`, `int Tier`, `int BuildWeight`, `float DryMass`, `float PowerDrawW`, `float MaxSafeTempK = 1200f`, `float MinSafeTempK = 200f`, `Texture2D Icon`; method `GetFormattedTooltip()`.
- **`PartDefinition` (abstract : `ComponentDefinition`):** `PartGroup PartGroup`, `Vector2I Footprint = Vector2I.One`, `PartGroupMask AllowedTop`, `PartGroupMask AllowedBottom`, `PartGroupMask AllowedSides`, `float FrontalArea = 3.1f`, `float SideArea = 4f`, `float DragCoefficient = 0.1f`, `float MaxShear = 2_000_000f`, `float MaxBending = 2_000_000f`, `float ImpactToleranceMps = 10f`, `PackedScene Scene`.
  - `StructuralPartDefinition`: `float AttachmentStrength`
  - `DecouplerPartDefinition` (: `StructuralPartDefinition`): `float EjectionImpulse` (N·s)
  - `FuelTankPartDefinition`: `FuelType PropellantType`, `float FuelCapacity` (kg), `float FuelDensity`
  - `EnginePartDefinition` (abstract): `float MaxThrustVac` (N), `float IspVacuum` (s), `float IspSeaLevel` (s), `float GimbalRangeDeg`; helpers `GetMaxThrustSeaLevel() => MaxThrustVac * IspSeaLevel / IspVacuum` and `GetMassFlowRate() => MaxThrustVac / (IspVacuum * FlightMath.G0)`.
	- `LiquidEnginePartDefinition`: `FuelType PropellantType`, `float MinThrottle`, `int MaxIgnitions`
	- `SolidBoosterPartDefinition`: `float PropellantMass` (kg), `Curve BurnCurve` (X: progress `0..1`, Y: thrust multiplier)
  - `AeroSurfacePartDefinition`: `float LiftCoefficient`, `float PlanformArea`, `float MaxDeflectionDeg` (`> 0` = controllable fin)
  - `CommandPodPartDefinition`: `int AvionicsSlots`, `int ThermalSlots`, `int PowerSlots`, `float MaxGForce`
  - `FairingPartDefinition`: `float AttachmentStrength`, `float MaxDynamicPressure`
  - `HeatShieldPartDefinition`: `float MaxAblatorKg`, `float HeatOfAblationJPerKg`
  - `ParachutePartDefinition`: `float DeployedDragArea` (m², pre-multiplied by $C_d$), `float MaxDeployDynamicPressure` (Pa), `float MaxDeployMach`
- **`ModuleDefinition` (abstract : `ComponentDefinition`):** `ModuleSlotType SlotType` (defaults: `MaxSafeTempK = 350f`, `MinSafeTempK = 230f`).
  - `AcsModuleDefinition`: `AcsDeviceKind Kind`, `float ControlAuthority` (N·m or N for RCS), `float ResponseLatencyMs`, `float SensorAccuracyArcsec`, `float MaxMomentumStorageNms`, `float DriftRateDegPerMin`, `float MonopropCapacityKg`, `SasModeMask EnabledSasModes`
  - `ThermalModuleDefinition`: `ThermalDeviceKind Kind`, `float ThermalConductivity`, `float MaxHeatRejectionW`, `float ReflectiveEfficiency`, `float HeaterOutputW`
  - `PowerModuleDefinition`: `PowerDeviceKind Kind`, `float NominalOutputW`, `float EnergyStorageWh`, `float WasteHeatW`, `float DegradationRatePerYear`
- **`BuildSettings` (: `Resource`):** `float CellSize = 2.0f`, `int GridWidth = 9`, `int GridHeight = 16`, `Color GhostValidColor`, `Color GhostInvalidColor`.

### 4.2 Runtime Classes & Control Methods (`building/runtime/`)
- **`IFlightComponent`:**
  - `ComponentDefinition BaseDefinition { get; }`
  - `ComponentState State { get; set; }`
  - `float TempK { get; set; }`
  - `float HeatCapacityJPerK => BaseDefinition.DryMass * 900f;`
  - `void ApplyThermalLoad(float heatJoule)` $\rightarrow$ `TempK += heatJoule / HeatCapacityJPerK; ThermalStateRules.Evaluate(this);`
  - `bool CanOperate()`
  - `void OnFixedTick(float deltaTime)`
- **`ThermalStateRules.Evaluate(IFlightComponent c)`:**
  - Named constants: `BreakMultiplier = 1.25f`, `RecoveryMultiplier = 0.9f`, `FreezeRecoveryMarginK = 10f`.
  - If `c.State == ComponentState.Broken` $\rightarrow$ return (irreversible).
  - If `c.TempK > BreakMultiplier * c.BaseDefinition.MaxSafeTempK` $\rightarrow$ `c.State = ComponentState.Broken`.
  - Else if `c.TempK > c.BaseDefinition.MaxSafeTempK` $\rightarrow$ `c.State = ComponentState.Overheated`.
  - Else if `c.State == ComponentState.Overheated && c.TempK < RecoveryMultiplier * c.BaseDefinition.MaxSafeTempK` $\rightarrow$ `c.State = ComponentState.Active`.
  - Else if `c.TempK < c.BaseDefinition.MinSafeTempK` $\rightarrow$ `c.State = ComponentState.Frozen`.
  - Else if `c.State == ComponentState.Frozen && c.TempK > c.BaseDefinition.MinSafeTempK + FreezeRecoveryMarginK` $\rightarrow$ `c.State = ComponentState.Active`.
- **Factory Mapping (`RocketPart.Create(PartDefinition def)`):**
  - `DecouplerPartDefinition` $\rightarrow$ `DecouplerPart` *(check before `StructuralPartDefinition`!)*
  - `StructuralPartDefinition` $\rightarrow$ `StructuralPart`
  - `FuelTankPartDefinition` $\rightarrow$ `FuelTankPart`
  - `LiquidEnginePartDefinition` $\rightarrow$ `LiquidEnginePart`
  - `SolidBoosterPartDefinition` $\rightarrow$ `SolidBoosterPart`
  - `AeroSurfacePartDefinition` $\rightarrow$ `ControlFinPart` if `MaxDeflectionDeg > 0f`, else `AeroSurfacePart`
  - `CommandPodPartDefinition` $\rightarrow$ `CommandPodPart`
  - `FairingPartDefinition` $\rightarrow$ `FairingPart`
  - `HeatShieldPartDefinition` $\rightarrow$ `HeatShieldPart`
  - `ParachutePartDefinition` $\rightarrow$ `ParachutePart`
- **Mandatory Control Rules:**
  - `EnginePart.Arm()`: Sets `IsArmed = true`. `SolidBoosterPart` immediately calls `Ignite()`.
  - `EnginePart.Ignite()`: Only if `IsArmed && !IsIgnited && State != ComponentState.Broken`. `LiquidEnginePart` also requires `RemainingIgnitions > 0` (decrements by 1) and fuel available in `FeedTanks`.
  - `EnginePart.Shutdown()`: `LiquidEnginePart` sets `IsIgnited = false`; `SolidBoosterPart` does nothing.
  - `EnginePart.SetGimbal(float angleDeg)`: Clamps to `[-GimbalRangeDeg, +GimbalRangeDeg]` and stores in `GimbalAngleDeg`.
  - `LiquidEnginePart.SetThrottle(float v)`: Clamps to `[0f, 1f]` and stores in `Throttle` (never auto-ignites).
  - `ControlFinPart.SetDeflection(float angleDeg)`: Clamps to `[-MaxDeflectionDeg, +MaxDeflectionDeg]`.
  - `FuelTankPart.DrainFuel(float kg)`: Drains `Mathf.Min(kg, CurrentFuel)`, updates `CurrentFuel`, returns actual kg drained. `SealLeak()` sets `LeakRateKgPerSec = 0f`.
  - `HeatShieldPart.Ablate(float heatJ)`: Ablates `kg = Mathf.Min(heatJ / HeatOfAblationJPerKg, AblatorRemainingKg)`, subtracts from `AblatorRemainingKg`, returns leftover heat `heatJ - (kg * HeatOfAblationJPerKg)`.
  - `ParachutePart.Deploy()`: Requires `ChuteState == ParachuteState.Stowed` and **zero active fairings** in `Tree.ActiveParts`; sets `ChuteState = ParachuteState.Deployed` and returns `true` (else `false`). `Cut()` sets `ParachuteState.Cut` and calls `Tree.SeverSubtree(this, SeverCause.Jettison)`.
  - `FairingPart.Jettison()`: Sets `IsShielded = false` on all `ShieldedParts`, then calls `Tree.SeverSubtree(this, SeverCause.Jettison)`.
  - `DecouplerPart.TriggerSeparation()`: Calls `Tree.SeverSubtree(Joint.ChildPart, SeverCause.Jettison, totalImpulseNs)` where `totalImpulseNs` is the sum of `EjectionImpulse` across all decouplers in that `Joint`. Ensure a joint is severed **at most once** even if multiple decouplers share it.
- **`PartTree` (`partial class : RefCounted`):**
  - Properties: `RootPart`, `ActiveParts` (`List<RocketPart>`).
  - Signal: `[Signal] public delegate void PartSeveredEventHandler(RocketPart part, SeverCause cause, float impulseNs);`
  - `GetParts<T>() where T : RocketPart`: Returns active parts of type `T`.
  - `GetSubtree(RocketPart part)`: Returns `part` and all its active descendants.
  - `GetSubtreeMass(RocketPart part)`: Sums `GetCurrentMass()` over `GetSubtree(part)`.
  - `SeverSubtree(RocketPart part, SeverCause cause, float impulseNs = 0f)`: Removes `part` from `part.ParentPart.Children`, **keeps `part.ParentJoint` intact**, removes all subtree parts and `part.ParentJoint.Decouplers` from `ActiveParts`, and emits `PartSevered`.

---

## 5. Grid, Validation & Assembly Rules

### 5.1 Grid Placement & Edge Normalization
- Valid cell range: `0 <= x < GridWidth (9)` and `0 <= y < GridHeight (16)`. Footprint spans `[Cell.X .. Cell.X + Footprint.X - 1]` and `[Cell.Y .. Cell.Y + Footprint.Y - 1]`.
- `CanPlace(def, cell)`: All footprint cells must be in-bounds and empty, and **every** orthogonally adjacent part must mutually permit contact (`AllowedTop` vs neighbor's `AllowedBottom`, `AllowedBottom` vs neighbor's `AllowedTop`, `AllowedSides` vs neighbor's `AllowedSides`).
- **Edge Normalization:** Always canonicalize an `Edge(cell, side)` to `Side.Top` (using the lower cell) or `Side.Right` (using the left cell). `CanPlaceDecoupler(edge)` is valid iff the two cells straddling `edge` belong to two different parts in valid contact and no decoupler already occupies `edge`.

### 5.2 Validation Errors (`ValidateConnections` — Vietnamese Strings)
Return a `List<string>` of player-facing errors:
1. Must have **exactly 1** `CommandPodPart`.
2. Must have **at least 1** engine (`LiquidEnginePart` or `SolidBoosterPart`).
3. Every placed part must be reachable from the command pod.
4. Every `LiquidEnginePart` must directly touch a `FuelTankPart` with matching `PropellantType`.
5. Every decoupler must sit on a valid contact edge between two distinct parts.
6. At most **1** fairing, **1** parachute, and **1** heat shield.
7. Installed modules must match their target `ModuleSlotType` and not exceed `AvionicsSlots` / `ThermalSlots` / `PowerSlots`.
8. Total `BuildWeight` must not exceed `TechTree.GetMaxBuildWeight()`.
*(Note: Missing heat shield, parachute, or power module is valid and NOT an error.)*

### 5.3 Assembly Pipeline (`RocketAssembler.AssembleRocket`)
1. Instantiate fresh `RocketPart.Create(def)` instances (never reuse editor preview parts), assigning `Cell` and `Tree`.
2. **BFS Tree:** Start BFS from `CommandPodPart`. When an unvisited part has multiple visited neighbors, pick the neighbor whose shared contact edges contain a **decoupler** as its `ParentPart`; otherwise pick the earliest-visited neighbor. Ignore non-tree side contacts.
3. **Joints:** Create one `Joint` per parent–child edge pair containing all shared contact edges and any `DecouplerPart` instances on them. Set `IsSelfSeparating = true` if the child is a `FairingPart` or `ParachutePart`. `Joint.Strength` = sum of decoupler `AttachmentStrength` (or child's `AttachmentStrength` if self-separating).
4. **Depth & Stages:** Joint depth = child part's hop depth from pod (`pod = 0`). Stage `0` has no joints. Group non-self-separating joints that have decouplers by depth, sort by **descending depth**, and assign to Stages `1, 2, ...`.
5. **EnginesToArm:** Simulate stage separations sequentially starting at Stage `0`; assign each engine to the `EnginesToArm` list of the **first** stage after which no part remains attached to the engine's `Bottom` edge.
6. **FeedTanks:** For each `LiquidEnginePart`, traverse the `PartTree` both upward and downward **without crossing any joint that contains a decoupler**; collect every `FuelTankPart` with matching `PropellantType`.
7. **Modules & Payload:** Instantiate `PodModule` entries into `CommandPodPart.Modules`; read `PayloadMassKg` from `ResearchCampaignEngine.Instance?.CurrentMission?.PayloadMassKg ?? 300f`.
8. **Shielding:** Populate `FairingPart.ShieldedParts` and `HeatShieldPart.ShieldedParts` with the ancestor chain from that part up to `CommandPodPart`.
9. **Visuals & Mass:** Instantiate `Definition.Scene ?? PartCatalog.Instance.GetPlaceholderScene(group)` under each part, call `Bind(part)`, and compute `MassCenterCalculator.Calculate(tree.ActiveParts)`.

### 5.4 Reference Mars Stack (`SampleDesigns.CreateMarsStack()`)
On the `9 x 16` grid (center column `x = 4`, `y = 0` at bottom):
- `(4, 12)`: `fairing_n1`
- `(4, 11)`: `chute_p1`
- `(4, 10)`: `pod_mk2` — Modules: `imu_g2`, `wheel_rw1`, `insulation_i1`, `battery_b2`, `solar_s1`
- `(4, 9)`: `tank_lm1`
- `(4, 8)`: `engine_land1`
- `(4, 7)`: `heatshield_h1` + Decoupler `Edge((4, 7), Side.Top)`
- `(4, 5)`: `tank_h1` (`1x2`, occupies `y = 5..6`) + Decoupler `Edge((4, 6), Side.Top)`
- `(4, 4)`: `engine_vac1`
- `(4, 1)`: `tank_m1` (`1x3`, occupies `y = 1..3`) + Decoupler `Edge((4, 3), Side.Top)`
- `(4, 0)`: `engine_lv2`
- `(3, 1)` & `(5, 1)`: `booster_m1` (`1x3`) + Decouplers `Edge((3, 2), Side.Right)` & `Edge((4, 2), Side.Right)`
- `(2, 1)` & `(6, 1)`: `fin_f1`

**Expected Stage Breakdown:**
- **Stage 0:** Arm `engine_lv2` + 2× `booster_m1`
- **Stage 1:** Fire side decouplers (depth 7) $\rightarrow$ jettison 2× `booster_m1`
- **Stage 2:** Fire `Edge((4, 3), Top)` (depth 6) $\rightarrow$ jettison `tank_m1` + `engine_lv2`; arm `engine_vac1`
- **Stage 3:** Fire `Edge((4, 6), Top)` (depth 4) $\rightarrow$ jettison `tank_h1` + `engine_vac1`
- **Stage 4:** Fire `Edge((4, 7), Top)` (depth 3) $\rightarrow$ jettison `heatshield_h1`; arm `engine_land1`

---

## 6. Placeholder Scenes Contract (`assets/parts/_placeholder/`)
Each `.tscn` has a `Node3D` root with `PartVisual` (or subclass), an `[Export] Color` tint, a `MeshRoot` child built from primitive meshes sized to `1 x 1` cell (`2.0 m`), and exact anchor nodes (no colliders, lights, or cameras):
- `command_pod.tscn`: Truncated cone (no anchors)
- `fairing.tscn`: Split cone with `HalfLeft`, `HalfRight`
- `parachute.tscn`: Short cylinder with `ChuteAttach` and hidden large hemisphere `Canopy`
- `heat_shield.tscn`: Flat disk (no anchors)
- `core_tank.tscn`: Cylinder (no anchors)
- `liquid_engine.tscn`: Short cylinder + inverted cone inside `GimbalPivot`, plus `ExhaustPoint`
- `solid_booster.tscn`: Cylinder + nose cone, plus `ExhaustPoint`
- `aero_surface.tscn`: Thin trapezoidal box inside `FinHinge`
- `structural.tscn`: Box (no anchors)
- `decoupler.tscn`: Thin ring (no anchors)

---

## 7. Complete Baseline Content Reference (30 `ModelId`s)

### Neighbor Attachment Matrix (`PartGroupMask`)
- **Fairing:** Top `None` | Bottom `CommandPod | Parachute` | Sides `None`
- **Parachute:** Top `Fairing` | Bottom `CommandPod` | Sides `None`
- **CommandPod:** Top `Fairing | Parachute` | Bottom `CoreTank | Structural | HeatShield` | Sides `None`
- **HeatShield:** Top `CommandPod | CoreTank | LiquidEngine` | Bottom `CoreTank | Structural` | Sides `None`
- **CoreTank:** Top `CommandPod | CoreTank | LiquidEngine | Structural | HeatShield` | Bottom `CoreTank | LiquidEngine | Structural | HeatShield` | Sides `SolidBooster | AeroSurface`
- **LiquidEngine:** Top `CoreTank` | Bottom `CoreTank | Structural | HeatShield` | Sides `None`
- **SolidBooster:** Top `None` | Bottom `None` | Sides `CoreTank | AeroSurface`
- **AeroSurface:** Top `None` | Bottom `None` | Sides `CoreTank | SolidBooster`
- **Structural:** Top `CommandPod | CoreTank | LiquidEngine | Structural | HeatShield` | Bottom `CoreTank | Structural` | Sides `None`

### 18 Parts (`data/parts/<ModelId>.tres`)
*(Defaults unless noted: `MaxSafeTempK = 1200`, `MinSafeTempK = 200`, `MaxShear = 2_000_000`, `MaxBending = 2_000_000`, `ImpactToleranceMps = 10`, `FrontalArea = 3.1`, `SideArea = 4 * Footprint.Y`, `PowerDrawW = 0`)*
1. `pod_mk1`: `CommandPod`, `1x1`, Dry `600`, Wt `1`, starter | Slots `A1/T0/P1`, `MaxGForce 8`, `PowerDrawW 40`, `Cd 0.4`, `ImpactToleranceMps 8`
2. `pod_mk2`: `CommandPod`, `1x1`, Dry `650`, Wt `2`, `t2_pod` | Slots `A2/T1/P2`, `MaxGForce 8`, `PowerDrawW 40`, `Cd 0.4`, `ImpactToleranceMps 10`
3. `fairing_n1`: `Fairing`, `1x1`, Dry `120`, Wt `1`, starter | `Cd 0.25`, `AttachmentStrength 100_000`, `MaxDynamicPressure 60_000`, `MaxSafeTempK 1500`
4. `chute_p1`: `Parachute`, `1x1`, Dry `60`, Wt `1`, `t3_edl` | `DeployedDragArea 80`, `MaxDeployDynamicPressure 1500`, `MaxDeployMach 2.5`, `AttachmentStrength 150_000`
5. `heatshield_h1`: `HeatShield`, `1x1`, Dry `200`, Wt `2`, `t3_edl` | `MaxAblatorKg 80`, `HeatOfAblationJPerKg 150_000`, `MaxSafeTempK 2400`, `Cd 1.5`, `FrontalArea 6.0`
6. `tank_s1`: `CoreTank`, `1x2`, Dry `150`, Wt `2`, starter | `Kerolox`, `FuelCapacity 1000`, `Cd 0.1`
7. `tank_m1`: `CoreTank`, `1x3`, Dry `400`, Wt `3`, `t1_heavy` | `Kerolox`, `FuelCapacity 6000`, `Cd 0.1`
8. `tank_h1`: `CoreTank`, `1x2`, Dry `150`, Wt `2`, `t2_upper_stage` | `Hydrolox`, `FuelCapacity 1800`, `Cd 0.1`
9. `tank_lm1`: `CoreTank`, `1x1`, Dry `80`, Wt `1`, `t3_lander` | `Methalox`, `FuelCapacity 400`, `Cd 0.1`
10. `engine_lv1`: `LiquidEngine`, `1x1`, Dry `300`, Wt `2`, starter | `Kerolox`, `MaxThrustVac 140_000`, `Isp 270/300`, `Gimbal 5°`, `MinThrottle 0.5`, `MaxIgnitions 1`
11. `engine_lv2`: `LiquidEngine`, `1x1`, Dry `600`, Wt `3`, `t2_main_engine` | `Kerolox`, `MaxThrustVac 260_000`, `Isp 280/310`, `Gimbal 5°`, `MinThrottle 0.5`, `MaxIgnitions 1`
12. `engine_vac1`: `LiquidEngine`, `1x1`, Dry `250`, Wt `3`, `t2_upper_stage` | `Hydrolox`, `MaxThrustVac 60_000`, `Isp 200/340`, `Gimbal 3°`, `MinThrottle 0.3`, `MaxIgnitions 3`
13. `engine_land1`: `LiquidEngine`, `1x1`, Dry `120`, Wt `2`, `t3_lander` | `Methalox`, `MaxThrustVac 25_000`, `Isp 290/320`, `Gimbal 6°`, `MinThrottle 0.15`, `MaxIgnitions 4`
14. `booster_s1`: `SolidBooster`, `1x3`, Dry `300`, Wt `2`, starter | `MaxThrustVac 130_000`, `Isp 230/250`, `PropellantMass 1500`, `BurnCurve 1.0 -> 0.8`
15. `booster_m1`: `SolidBooster`, `1x3`, Dry `500`, Wt `3`, `t1_heavy` | `MaxThrustVac 135_000`, `Isp 230/250`, `PropellantMass 5000`, `BurnCurve 1.0 -> 0.8`
16. `decoupler_d1`: `DecouplerPartDefinition` (`PartGroup.Structural`), edge, Dry `30`, Wt `1`, starter | `AttachmentStrength 300_000`, `EjectionImpulse 3000`, `FrontalArea 0.5`, `Cd 0.1`
17. `fin_f1`: `AeroSurface`, `1x1`, Dry `40`, Wt `1`, starter | `LiftCoefficient 0.8`, `PlanformArea 1.5`, `MaxDeflectionDeg 20`
18. `structural_s1`: `Structural`, `1x1`, Dry `50`, Wt `1`, starter | `AttachmentStrength 500_000`, `Cd 0.1`

### 12 Modules (`data/modules/<ModelId>.tres`)
*(Defaults: `MaxSafeTempK = 350`, `MinSafeTempK = 230`)*
1. `imu_g1`: `Acs / Imu`, `Avionics`, Dry `15`, Wt `1`, Draw `20`, starter | `Accuracy 1800"`, `Drift 0.2°/min`, `Latency 80ms`, `SAS: StabilityAssist`
2. `imu_g2`: `Acs / Imu`, `Avionics`, Dry `15`, Wt `1`, Draw `25`, `t1_avionics` | `Accuracy 720"`, `Drift 0.05°/min`, `Latency 40ms`, `SAS: all 4 modes`
3. `startracker_st1`: `Acs / StarTracker`, `Avionics`, Dry `10`, Wt `1`, Draw `15`, `t3_navigation` | `Accuracy 5"`, `Drift 0`, `Latency 200ms`, `SAS: all 4 modes`
4. `wheel_rw1`: `Acs / ReactionWheel`, `Avionics`, Dry `40`, Wt `1`, Draw `40`, `t1_avionics` | `ControlAuthority 3000`, `MaxMomentumStorageNms 30_000`
5. `rcs_r1`: `Acs / RcsThruster`, `Avionics`, Dry `30`, Wt `1`, Draw `10`, `t3_navigation` | `ControlAuthority 2000`, `MonopropCapacityKg 40`
6. `battery_b1`: `Power / Battery`, `Power`, Dry `20`, Wt `1`, Draw `0`, starter | `EnergyStorageWh 500`
7. `battery_b2`: `Power / Battery`, `Power`, Dry `30`, Wt `1`, Draw `0`, `t2_power` | `EnergyStorageWh 2000`
8. `solar_s1`: `Power / SolarArray`, `Power`, Dry `25`, Wt `1`, Draw `0`, `t2_power` | `NominalOutputW 300`
9. `rtg_t1`: `Power / Rtg`, `Power`, Dry `45`, Wt `2`, Draw `0`, `t4_rtg` | `NominalOutputW 120`, `DegradationRatePerYear 0.008`, `WasteHeatW 1500`
10. `insulation_i1`: `Thermal / PassiveInsulation`, `Thermal`, Dry `15`, Wt `1`, Draw `0`, `t3_thermal` | `ReflectiveEfficiency 0.7`
11. `radiator_a1`: `Thermal / ActiveRadiator`, `Thermal`, Dry `25`, Wt `1`, Draw `15`, `t3_thermal` | `MaxHeatRejectionW 2000`
12. `rhu_h1`: `Thermal / RadioisotopeHeater`, `Thermal`, Dry `5`, Wt `1`, Draw `0`, `t3_thermal` | `HeaterOutputW 50`
```[cite: 2, 3, 4]