# Hướng dẫn cho Dev A: Rocket Building

Bạn là AI coding agent của **Dev A**. Đọc `AGENTS.md` ở gốc repo trước file này; mọi quy ước ở đó (đặt tên, `[Export]`, signal, thư mục, git) đều áp dụng.

## 1. Phạm vi

Bạn sở hữu và chỉ sửa:

- `building/` (data, runtime, assembly, editor, ui)
- `presentation/parts/` (`PartVisual` và 8 lớp con)
- `data/parts/`, `data/modules/`, `data/settings/build_settings.tres`
- `assets/parts/_placeholder/` (scene tạm của part)
- `scenes/build_screen.tscn`
- `tests/a/`

Diagram cần đọc: `docs/design/diagrams/01_shared_data.mmd`, `02a_building_editor_assembly.mmd`, `02b_building_runtime_parts.mmd`, `02c_building_pod_modules.mmd`, `06_presentation.mmd`.

**Bạn là người mở khoá cho hai người còn lại**: B và C cần các lớp part và một rocket mẫu của bạn để test. Hai việc A1 và A2 có hạn chót cứng; ưu tiên chúng hơn mọi thứ khác.

## 2. Phụ thuộc

| Bạn cần | Từ ai | Khi nào | Trong lúc chờ |
| --- | --- | --- | --- |
| Code Core (`core/`) | C | H0:45 | Đọc tài liệu, lên khung class |
| `ResearchCampaignEngine.Instance.TechTree` với `GetUnlockedModels()`, `IsUnlocked(id)`, `GetMaxBuildWeight()` | B | Stub ở M0, thật ở H9 | Stub của B trả "mọi part starter đã mở" và weight 20 |
| `ResearchCampaignEngine.Instance.CurrentMission` (`PayloadMassKg`, `PayloadScene`) | B | Stub ở M0 | Dùng 300 kg nếu null |
| `AerodynamicSolver.Solve(...)` cho wind tunnel | B | H6 | Wind tunnel là P2, không chờ |

| Người khác cần | Thứ gì | Hạn chót |
| --- | --- | --- |
| B, C | Mọi lớp trong 01, 02b, 02c có đủ field và hàm điều khiển chạy đúng | **H3 (việc A1)** |
| B, C | `TestRocketFactory.CreateMinimal()` trả một `AssembledRocket` bay được | **H3 (việc A2)** |
| B, C | `TestRocketFactory.CreateMarsStack()` dựng qua `RocketAssembler` thật, có stage | **H6 (việc A4)** |
| B | Đủ file `.tres` của 30 `ModelId` ở mục 7 | H8 (việc A5) |
| C | Build screen phát `DesignConfirmed` với rocket người chơi xây | H10.5 (việc A6) |

## 3. Danh sách việc theo thứ tự

Giờ tính từ lúc cả nhóm bắt đầu (H0). Làm đúng thứ tự.

### A0. Skeleton (H0:45–H1.5, P0)

- Tạo mọi class trong 5 diagram của bạn với **đúng chữ ký public** (có tính thêm các bổ sung ở `AGENTS.md` mục 8 và mục 4 file này).
- Definition (`ComponentDefinition` và mọi lớp con, `ModuleDefinition` và lớp con, `BuildSettings`) viết **đầy đủ ngay** vì chúng chỉ là dữ liệu `[Export]`.
- Lớp còn lại: thân hàm trả giá trị mặc định, thêm `// TODO(A1)`.
- `PartCatalog` là autoload stub (C đã đăng ký scene `building/editor/part_catalog.tscn`; bạn thay script và nội dung scene): `Instance`, `GetDefinition(id)` trả null.
- **Xong khi:** `dotnet build` sạch, merge vào `main` trước H1.5 (mốc M0).

### A1. Lớp part lúc bay (H1.5–H3, P0, hạn chót cứng H3)

- `IFlightComponent`, `ThermalStateRules`, `RocketPart` và 10 lớp con, `PodModule` và 11 lớp con, `Joint`, `PartTree`, `StageGroup`, `AssembledRocket`.
- `RocketPart.Create(PartDefinition def)`: factory chọn đúng lớp runtime theo kiểu definition (bảng ở mục 4.2).
- Hàm điều khiển chạy đúng luật ở mục 4.3; `PartTree.SeverSubtree` cắt cây và phát signal.
- **Xong khi:** sandbox `tests/a/part_runtime_test.tscn` dựng tay pod + tank + động cơ, gọi `Arm`, `SetThrottle`, `DrainFuel`, `SeverSubtree`, và in ra trạng thái đúng như mong đợi. Merge.

### A2. Rocket mẫu tối thiểu (H2.5–H3, P0, hạn chót cứng H3)

- `TestRocketFactory.CreateMinimal()`: dựng **bằng code, không qua grid** một rocket 1 cột: `pod_mk1` (gắn `imu_g1`, `battery_b1`), `tank_s1`, `engine_lv1`, với `ParentPart`, `Children`, `Joint`, `FeedTanks` đặt tay, một `StageGroup` stage 0 chứa động cơ, và `MassProperties` tính bằng `MassCenterCalculator`.
- Definition lấy từ `PartCatalog` nếu có `.tres`; chưa có thì tạo definition trong code với số ở mục 7 (chỉ trong factory test này).
- **Xong khi:** gọi `CreateMinimal()` không lỗi, tổng khối lượng và CoM in ra hợp lý. Merge và **báo người dùng nhắn B và C**.

### A3. Grid, design, khối lượng (H3–H4.5, P0)

- `BuildGrid` (chiếm ô, `CanPlace`, `CanPlaceDecoupler`, `GetNeighbors`, `GetContactEdges`), `RocketDesign` (3 danh sách entry, `Serialize`/`Deserialize` JSON, `SaveCurrent`/`LoadLast` ở `user://current_design.json`), `BuildBudget`, `MassCenterCalculator`.
- **Xong khi:** sandbox đặt các part của Mars stack (mục 5.4) theo code: mọi `CanPlace` trả true; đặt pod dưới tank trả false; budget cộng đúng.

### A4. RocketAssembler (H4.5–H6, P0, hạn chót H6)

- `ValidateConnections`, `AssembleRuntimeTree`, `InstallModules`, `LinkFeedTanks`, `DeriveStages`, `AttachVisuals`, `AssembleRocket` theo thuật toán ở mục 5.
- `SampleDesigns.CreateMarsStack()` trả `RocketDesign` của Mars stack (mục 5.4); `TestRocketFactory.CreateMarsStack()` = `RocketAssembler.AssembleRocket(SampleDesigns.CreateMarsStack())`.
- **Xong khi:** sandbox in ra 5 stage đúng như bảng ở mục 5.4 (stage 0 nạp `engine_lv2` và 2 booster; stage 2 nạp `engine_vac1`; stage 4 nạp `engine_land1`), và `FeedTanks` của từng động cơ đúng. Merge trước H6 (mốc M1).

### A5. Nội dung và PartCatalog (H6–H8, P0)

- 30 file `.tres` ở mục 7 (`data/parts/<id>.tres`, `data/modules/<id>.tres`), `data/settings/build_settings.tres`.
- 9 scene tạm `assets/parts/_placeholder/<part_group>.tscn` và `decoupler.tscn` (mục 6).
- `PartCatalog` thật: `[Export] BuildSettings Settings`, `[Export] string PartsFolder = "res://data/parts"`, `ModulesFolder`, `PlaceholderFolder`; nạp mọi `.tres` lúc `_EnterTree`; `GetDefinition`, `GetAll`, `GetUnlocked` (lọc `TechTree.GetUnlockedModels()` theo loại), `ValidateAgainstTechTree`, `ValidateScenes`, `GetPlaceholderScene`.
- `PartVisual` cơ bản: `Bind`, `SetGhost`, `ApplyThermalOverlay`, `OnSevered` (không cần lớp con ở bước này).
- **Xong khi:** game khởi động không có lỗi từ `ValidateAgainstTechTree` / `ValidateScenes`; Mars stack trong sandbox hiện đúng hình khối.

### A6. Build screen (H8–H10.5, P0, hạn chót H10.5)

- `scenes/build_screen.tscn` với `BuildScreenRoot`, `BuildingSystem` và UI theo mục 5.5.
- **Xong khi:** người chơi xây lại được Mars stack bằng chuột chỉ với part đã mở, thấy lỗi validate, bấm "Phóng" thì `DesignConfirmed` phát ra (C nối vào ở mốc M2).

### A7. Module, decoupler, nạp lại design (H10.5–H12, P0)

- Panel slot module khi có command pod; `InstallModule`/`RemoveModule`; tự gỡ decoupler mồ côi khi xoá hoặc di chuyển part (hoàn lại weight); `LoadSavedDesign()` khi vào lại build screen.
- **Xong khi:** quay lại từ debrief thì design cũ hiện lại đủ part, decoupler và module.

### A8. Hình ảnh part (H12–H14, P1)

- `EngineVisual` (lửa đơn giản tại `ExhaustPoint`, xoay `GimbalPivot`), `ParachuteVisual`, `FairingVisual`, `HeatShieldVisual`, `FinVisual`, `DecouplerVisual`, `PodVisual` (ẩn hoặc hiện robot), `TankVisual`.
- **Xong khi:** trong flight scene thấy lửa theo ga, dù bung, fairing tách, heat shield sẫm dần.

### A9. Sau M3 (H14–H16, P1/P2)

- Kéo thả để di chuyển part (`MovePart`, P1); tooltip của part (P1); cân bằng số cùng B (P1); `WindTunnelAnalyzer` (P2).
- Từ H16: chỉ sửa lỗi.

## 4. Chi tiết: dữ liệu và lớp part

### 4.1 Definition (đều là `[GlobalClass] public partial class X : Resource`, mọi field `[Export]`)

`ComponentDefinition` (abstract): `ModelId`, `DisplayName`, `Tier`, `BuildWeight`, `DryMass`, `PowerDrawW`, `MaxSafeTempK`, `MinSafeTempK`, `Texture2D Icon`; `GetFormattedTooltip()`.

`PartDefinition` (abstract): `PartGroup PartGroup`, `Vector2I Footprint`, `PartGroupMask AllowedTop`, `AllowedBottom`, `AllowedSides`, `FrontalArea`, `SideArea`, `DragCoefficient`, `MaxShear`, `MaxBending`, `ImpactToleranceMps`, `PackedScene Scene`.

| Lớp con | Field riêng |
| --- | --- |
| `StructuralPartDefinition` | `AttachmentStrength` |
| `DecouplerPartDefinition` (kế thừa Structural) | `EjectionImpulse` (N·s) |
| `FuelTankPartDefinition` | `FuelType PropellantType`, `FuelCapacity` (kg), `FuelDensity` |
| `EnginePartDefinition` (abstract) | `MaxThrustVac` (N), `IspVacuum`, `IspSeaLevel` (s), `GimbalRangeDeg`; `GetMaxThrustSeaLevel()` = `MaxThrustVac * IspSeaLevel / IspVacuum`; `GetMassFlowRate()` = `MaxThrustVac / (IspVacuum * FlightMath.G0)` (kg/s) |
| `LiquidEnginePartDefinition` | `PropellantType`, `MinThrottle` (0..1), `MaxIgnitions` |
| `SolidBoosterPartDefinition` | `PropellantMass` (kg), `Curve BurnCurve` (x = tiến độ đốt 0..1, y = hệ số lực đẩy) |
| `AeroSurfacePartDefinition` | `LiftCoefficient`, `PlanformArea`, `MaxDeflectionDeg` (> 0 nghĩa là fin điều khiển) |
| `CommandPodPartDefinition` | `AvionicsSlots`, `ThermalSlots`, `PowerSlots`, `MaxGForce` |
| `FairingPartDefinition` | `AttachmentStrength`, `MaxDynamicPressure` |
| `HeatShieldPartDefinition` | `MaxAblatorKg`, `HeatOfAblationJPerKg` |
| `ParachutePartDefinition` | `DeployedDragArea` (m², đã nhân Cd), `MaxDeployDynamicPressure` (Pa), `MaxDeployMach` |

`ModuleDefinition` (abstract): `ModuleSlotType SlotType`. Lớp con:

| Lớp con | Field |
| --- | --- |
| `AcsModuleDefinition` | `AcsDeviceKind Kind`, `ControlAuthority` (N·m; với RCS cũng là lực tịnh tiến N), `ResponseLatencyMs`, `SensorAccuracyArcsec`, `MaxMomentumStorageNms`, `DriftRateDegPerMin`, `MonopropCapacityKg`, `SasModeMask EnabledSasModes` |
| `ThermalModuleDefinition` | `ThermalDeviceKind Kind`, `ThermalConductivity`, `MaxHeatRejectionW`, `ReflectiveEfficiency` (0..1), `HeaterOutputW` |
| `PowerModuleDefinition` | `PowerDeviceKind Kind`, `NominalOutputW`, `EnergyStorageWh`, `WasteHeatW`, `DegradationRatePerYear` |

`BuildSettings` (Resource, `data/settings/build_settings.tres`): `CellSize` = 2.0, `GridWidth` = 9, `GridHeight` = 16, `Color GhostValidColor`, `GhostInvalidColor`. `PartCatalog` export resource này; ai cần `CellSize` đọc `PartCatalog.Instance.Settings.CellSize`.

### 4.2 Lớp runtime

`IFlightComponent` (interface, `building/runtime/`):

```csharp
public interface IFlightComponent
{
    ComponentDefinition BaseDefinition { get; }
    ComponentState State { get; set; }
    float TempK { get; set; }
    float HeatCapacityJPerK { get; }          // DryMass * 900
    void ApplyThermalLoad(float heatJoule);   // TempK += heat / HeatCapacityJPerK, then ThermalStateRules.Evaluate(this)
    bool CanOperate();
    void OnFixedTick(float deltaTime);
}
```

`ThermalStateRules.Evaluate(IFlightComponent c)` (static): `Broken` thì giữ nguyên; `TempK > 1.25 × MaxSafeTempK` → `Broken`; `> MaxSafeTempK` → `Overheated`; `Overheated` và `< 0.9 × MaxSafeTempK` → `Active`; `< MinSafeTempK` → `Frozen`; `Frozen` và `> MinSafeTempK + 10` → `Active`. Hai hệ số và 10 K là `const` có tên trong lớp này.

`RocketPart : Node3D` (implement `IFlightComponent`): `Definition`, `Cell`, `ParentPart`, `Children`, `ParentJoint`, `State`, `TempK` (khởi tạo 288), `IsShielded`; `GetCurrentMass()` (dry + nhiên liệu/thuốc phóng), `CalculateAero(...)` (stub trả `Vector2.Zero`; physics tự tính), `GetGridPosition()`, `GetLocalPosition()` (mét). Các stat cũ (`DryMass`, `FrontalArea`, ...) là property chỉ đọc chuyển tiếp tới `Definition`. `CanOperate()` = `State != Broken && State != Overheated && State != Frozen`.

| Definition | Lớp runtime tạo bởi `RocketPart.Create` |
| --- | --- |
| `DecouplerPartDefinition` | `DecouplerPart` (kiểm tra trước Structural) |
| `StructuralPartDefinition` | `StructuralPart` |
| `FuelTankPartDefinition` | `FuelTankPart` |
| `LiquidEnginePartDefinition` | `LiquidEnginePart` |
| `SolidBoosterPartDefinition` | `SolidBoosterPart` |
| `AeroSurfacePartDefinition` | `ControlFinPart` nếu `MaxDeflectionDeg > 0`, ngược lại `AeroSurfacePart` |
| `CommandPodPartDefinition` | `CommandPodPart` |
| `FairingPartDefinition` | `FairingPart` |
| `HeatShieldPartDefinition` | `HeatShieldPart` |
| `ParachutePartDefinition` | `ParachutePart` |

`PodModule` là **class C# thường** (không phải Node), implement `IFlightComponent`; `IsPowered` mặc định true; `CanOperate()` thêm điều kiện `IsPowered`. `Create(ModuleDefinition def)` chọn lớp theo `Kind`.

### 4.3 Luật của các hàm điều khiển

| Hàm | Luật |
| --- | --- |
| `EnginePart.Arm()` | `IsArmed = true`. `SolidBoosterPart` gọi luôn `Ignite()`. |
| `EnginePart.Ignite()` | Chỉ khi `IsArmed`, chưa cháy, `State` không `Broken`. Động cơ lỏng: cần `RemainingIgnitions > 0` (trừ 1) và còn nhiên liệu trong `FeedTanks`. |
| `EnginePart.Shutdown()` | Lỏng: `IsIgnited = false`. Rắn: không làm gì. |
| `EnginePart.SetGimbal(angleDeg)` | Kẹp trong `±GimbalRangeDeg`, lưu `GimbalAngleDeg`. Dấu theo `AGENTS.md` mục 7. |
| `LiquidEnginePart.SetThrottle(v)` | Kẹp 0..1, lưu `Throttle`. Không tự đánh lửa (việc của `ThrottleController` bên C). |
| `ControlFinPart.SetDeflection(angleDeg)` | Kẹp trong `±MaxDeflectionDeg`. |
| `FuelTankPart.DrainFuel(kg)` | Trừ tối đa phần còn lại, trả lượng thực rút. |
| `FuelTankPart.SealLeak()` | `LeakRateKgPerSec = 0`. |
| `HeatShieldPart.Ablate(heatJ)` | Đốt `min(heatJ / HeatOfAblationJPerKg, AblatorRemainingKg)` kg ablator; trả phần nhiệt còn lọt qua (J). |
| `ParachutePart.Deploy()` | Chỉ khi `ChuteState == Stowed` và **không còn fairing** trong `PartTree`; chuyển `Deployed`. Trả bool. |
| `ParachutePart.Cut()` | Chuyển `Cut` rồi `Tree.SeverSubtree(this, Jettison)`. |
| `FairingPart.Jettison()` | `Tree.SeverSubtree(this, Jettison)`; trước đó đặt `IsShielded = false` cho `ShieldedParts`. |
| `DecouplerPart.TriggerSeparation()` | Gọi `Tree.SeverSubtree(Joint.ChildPart, Jettison, impulseNs)`, với `impulseNs` = tổng `EjectionImpulse` của mọi decoupler trong joint. Mỗi joint chỉ cắt một lần dù có nhiều decoupler. |

Part cần biết cây của nó: thêm property `PartTree Tree` (đặt bởi assembler).

`PartTree`: `RootPart`, `ActiveParts`, `GetParts<T>() where T : RocketPart`, `SeverSubtree(RocketPart part, SeverCause cause, float impulseNs = 0)` (gỡ `part` khỏi `Children` của cha nhưng **giữ nguyên `part.ParentJoint`**, gỡ cả cây con và các decoupler của joint khỏi `ActiveParts`, phát `PartSevered(part, cause, impulseNs)`), `GetSubtree(RocketPart part)` (thêm, B cần để tính tải), `GetSubtreeMass(RocketPart part)`. `PartTree` là `partial class : RefCounted` để phát được signal. **Không** reparent node trong `SeverSubtree`; việc đó là của `VehicleRoot` (C) khi nghe signal.

`Joint` là class C# thường: thêm `IsSelfSeparating` (fairing, dù) và `Strength` = tổng `AttachmentStrength` của decoupler (với joint tự tách dùng `AttachmentStrength` của chính fairing hoặc dù).

`StageGroup` (class thường): `StageIndex`, `Depth`, `Joints`, `Parts`, `EnginesToArm`.

`AssembledRocket : RefCounted`: `PartTree PartTree`, `List<StageGroup> Stages`, `MassProperties MassProperties`.

## 5. Chi tiết: grid, lắp ráp, build screen

### 5.1 Grid

- Ô `(x, y)`, `0 ≤ x < GridWidth`, `0 ≤ y < GridHeight`, `y` tăng lên trên. Part chiếm ô từ `Cell` tới `Cell + Footprint - (1,1)`.
- `CanPlace(def, cell)`: trong grid, không chồng ô, và với **mỗi** part hàng xóm có chung ít nhất một cạnh: hai bên cùng cho phép nhau. Hàng xóm ở phía Top của part mới phải có `PartGroup` nằm trong `AllowedTop` của part mới, và part mới phải nằm trong `AllowedBottom` của hàng xóm; tương tự cho Bottom, và `AllowedSides` cho Left/Right.
- Decoupler nằm trên `Edge(cell, side)`; chuẩn hoá cạnh về dạng `Top` hoặc `Right` của ô dưới/trái để một cạnh chỉ có một biểu diễn. `CanPlaceDecoupler`: hai ô hai bên cạnh thuộc hai part khác nhau đang tiếp xúc hợp lệ, và cạnh chưa có decoupler.

### 5.2 Validation lúc confirm (`ValidateConnections`)

Trả danh sách lỗi tiếng Việt cho người chơi: đúng 1 command pod; ít nhất 1 động cơ; mọi part nối được tới pod; mỗi động cơ lỏng chạm trực tiếp một `CoreTank` cùng `PropellantType`; mỗi decoupler nằm trên cạnh tiếp xúc hợp lệ; tối đa 1 fairing, 1 dù, 1 heat shield; module đúng loại slot và không vượt số slot; tổng weight trong giới hạn. Thiếu heat shield, dù, nguồn điện **không** phải lỗi.

### 5.3 Thuật toán lắp ráp (`AssembleRocket`)

1. Tạo `RocketPart.Create(def)` cho từng entry (bản mới, không dùng lại part của build screen), đặt `Cell`, `Tree`.
2. **Cây:** BFS từ pod. Khi một part chưa thăm có nhiều hàng xóm đã thăm, **chọn làm cha hàng xóm mà cạnh chung có decoupler**; nếu không có thì chọn hàng xóm thăm sớm nhất. Tiếp xúc không thành cạnh cây bị bỏ qua (không giữ part lại).
3. **Joint:** mỗi cặp cha–con một `Joint` với mọi cạnh tiếp xúc giữa hai part và các `DecouplerPart` trên đó. Joint con là fairing hoặc dù thì `IsSelfSeparating = true`.
4. **Depth** của joint = độ sâu của part con tính từ pod (pod = 0).
5. **Stage:** stage 0 không có joint. Các joint có decoupler, nhóm theo depth, sắp depth giảm dần, thành stage 1, 2, ... Joint tự tách không vào stage.
6. **EnginesToArm:** mô phỏng lần lượt từ stage 0: một động cơ vào `EnginesToArm` của stage đầu tiên mà sau khi stage đó tách, không còn part nào gắn ở cạnh Bottom của nó.
7. **FeedTanks:** với mỗi động cơ lỏng, duyệt cây (cả lên và xuống) từ động cơ, **không đi qua joint có decoupler**; mọi `FuelTankPart` cùng `PropellantType` gặp được là feed tank.
8. **Module:** tạo `PodModule` cho từng entry, gắn vào `CommandPodPart.Modules`; `PayloadMassKg` lấy từ `ResearchCampaignEngine.Instance.CurrentMission`.
9. **Che chắn:** `FairingPart.ShieldedParts` = chuỗi cha của fairing tới hết pod; `HeatShieldPart.ShieldedParts` = chuỗi cha của heat shield tới hết pod.
10. **Hình ảnh:** `AttachVisuals(tree)`: instance `Definition.Scene` (hoặc `GetPlaceholderScene(group)`) làm con của part, gọi `Bind(part)`.
11. `MassProperties` = `MassCenterCalculator.Calculate(tree.ActiveParts)`.

`MassCenterCalculator.Calculate(parts)`: CoM = tổng khối lượng có trọng số của `GetLocalPosition()`; mô-men quán tính = tổng của (hộp đặc theo footprint: m(w² + h²)/12) + m·d² với d là khoảng cách tới CoM. Khối lượng module cộng vào pod qua `GetCurrentMass()` của pod.

### 5.4 Mars stack mẫu (`SampleDesigns.CreateMarsStack()`)

Cột giữa x = 4 trên grid 9 × 16 (y = 0 là đáy):

| Ô | Part | Ghi chú |
| --- | --- | --- |
| (4,12) | `fairing_n1` | |
| (4,11) | `chute_p1` | |
| (4,10) | `pod_mk2` | module: `imu_g2`, `wheel_rw1`, `insulation_i1`, `battery_b2`, `solar_s1` |
| (4,9) | `tank_lm1` | |
| (4,8) | `engine_land1` | |
| (4,7) | `heatshield_h1` | decoupler `Edge((4,7), Top)` |
| (4,5) | `tank_h1` (1×2, y 5–6) | decoupler `Edge((4,6), Top)` |
| (4,4) | `engine_vac1` | |
| (4,1) | `tank_m1` (1×3, y 1–3) | decoupler `Edge((4,3), Top)` |
| (4,0) | `engine_lv2` | |
| (3,1), (5,1) | `booster_m1` (1×3) | decoupler `Edge((3,2), Right)` và `Edge((4,2), Right)` |
| (2,1), (6,1) | `fin_f1` | |

Stage mong đợi: 0 = nạp `engine_lv2` + 2 booster; 1 = tách 2 booster (depth 7); 2 = tách `tank_m1` (depth 6), nạp `engine_vac1`; 3 = tách `tank_h1` (depth 4); 4 = tách `heatshield_h1` (depth 3), nạp `engine_land1`. Dùng làm test tự động trong `tests/a/`.

### 5.5 Build screen (P0)

- `BuildScreenRoot : Node3D` (gốc `build_screen.tscn`) với `[Export] BuildingSystem Building` và signal `ResearchRequested()`. GameFlowManager (C) đọc `Building` để nối `DesignConfirmed`; **build screen không gọi gì của Control**.
- Camera3D trực giao nhìn theo −Z; grid vẽ bằng đường mảnh; part là `RocketPart` thật với scene của chúng.
- Chuột: chiếu tia xuống mặt phẳng z = 0 → ô. `build_place` (chuột trái): đặt model đang chọn (ghost xanh/đỏ theo `CanPlace` và `CanAfford`). `build_remove` (chuột phải): xoá. `build_toggle_decoupler` (Tab): bật chế độ decoupler, tô cạnh gần nhất, trái đặt, phải xoá. `build_cancel` (Esc): bỏ chọn. Các action này C đã tạo trong input map.
- Panel trái: palette theo nhóm (Pod, Tank, Engine, Booster, EDL, Khác, Module) từ `GetAvailableParts()` / `GetAvailableModules(slot)`, mỗi nút có icon, tên, weight.
- Panel phải: weight `Current / Max`, tổng khối lượng, dấu CoM trên grid, danh sách slot của pod, danh sách lỗi validate.
- Nút: "Phóng" (`ConfirmDesign`), "Nghiên cứu" (phát `ResearchRequested`), "Xoá hết".
- Mọi màu, kích thước UI, tốc độ zoom là `[Export]`.

## 6. Scene tạm của part (`assets/parts/_placeholder/`)

Theo hợp đồng ở tab "Hợp đồng model và đồ hoạ": gốc `Node3D` có script `PartVisual` (hoặc lớp con), con `MeshRoot`, và đủ anchor bắt buộc của nhóm. Dựng từ mesh có sẵn của Godot, kích thước theo 1 ô (`CellSize`); `PartVisual.Bind` co giãn `MeshRoot` theo `Footprint`.

| Nhóm | Hình khối | Anchor bắt buộc |
| --- | --- | --- |
| `command_pod` | Nón cụt | (không) |
| `fairing` | Nón, tách thành `HalfLeft`, `HalfRight` | `HalfLeft`, `HalfRight` |
| `parachute` | Trụ thấp; `Canopy` là bán cầu lớn, ẩn | `ChuteAttach`, `Canopy` |
| `heat_shield` | Đĩa dẹt | (không) |
| `core_tank` | Trụ | (không) |
| `liquid_engine` | Trụ ngắn + nón ngược trong `GimbalPivot` | `ExhaustPoint`, `GimbalPivot` |
| `solid_booster` | Trụ + nón mũi | `ExhaustPoint` |
| `aero_surface` | Hộp mỏng hình thang trong `FinHinge` | `FinHinge` |
| `structural` | Hộp | (không) |
| `decoupler` | Vòng mỏng | (không) |

Màu mỗi nhóm khác nhau, đặt bằng `[Export] Color` trên script để dễ phân biệt khi test.

## 7. Nội dung khởi điểm (30 `ModelId`)

Số dưới đây là **giá trị khởi điểm để chạy được**; A và B cân bằng lại ở H14–H16. Mục tiêu cân bằng: rocket chỉ dùng part starter lên được không gian (> 90 km) nhưng khó vào quỹ đạo; Mars stack ở mục 5.4 đáp được nếu lái tốt.

Các số này đã được kiểm bằng một mô phỏng chất điểm thô (không phải physics của game): rocket starter (pod, 2 × `tank_s1`, `engine_lv1`, 2 × `booster_s1`) lên tối đa khoảng 100 km mà không vào quỹ đạo; Mars stack vào quỹ đạo 90 km và còn khoảng 1 250 m/s cho cú đốt chuyển quỹ đạo (cần 800); ở sao Hỏa, dù mở được ở khoảng 12 km với tải khoảng 4,6 g, và lander đốt hãm đáp được với gần 340 kg nhiên liệu còn dư. Cột "Mở bởi" là `NodeId` của tech tree (B), "starter" nghĩa là có sẵn từ đầu.

Mặc định cho mọi part trừ khi bảng ghi khác: `MaxSafeTempK` 1200, `MinSafeTempK` 200, `MaxShear` 2 000 000 N, `MaxBending` 2 000 000 N·m, `ImpactToleranceMps` 10, `FrontalArea` 3.1 m², `SideArea` = 4 × chiều cao theo ô (m²), `PowerDrawW` 0.

**Part**

| ModelId | Nhóm | Footprint | Dry (kg) | Weight | Mở bởi | Thông số riêng |
| --- | --- | --- | --- | --- | --- | --- |
| `pod_mk1` | CommandPod | 1×1 | 600 | 1 | starter | slot A1/T0/P1, MaxGForce 8, PowerDraw 40, Cd 0.4, ImpactTolerance 8 |
| `pod_mk2` | CommandPod | 1×1 | 650 | 2 | `t2_pod` | slot A2/T1/P2, MaxGForce 8, PowerDraw 40, Cd 0.4, ImpactTolerance 10 |
| `fairing_n1` | Fairing | 1×1 | 120 | 1 | starter | Cd 0.25, AttachmentStrength 100 000, MaxDynamicPressure 60 000, MaxSafeTemp 1500 |
| `chute_p1` | Parachute | 1×1 | 60 | 1 | `t3_edl` | DeployedDragArea 80, MaxDeployDynamicPressure 1500, MaxDeployMach 2.5, AttachmentStrength 150 000 |
| `heatshield_h1` | HeatShield | 1×1 | 200 | 2 | `t3_edl` | MaxAblatorKg 80, HeatOfAblation 150 000, MaxSafeTemp 2400, Cd 1.5, FrontalArea 6.0 |
| `tank_s1` | CoreTank | 1×2 | 150 | 2 | starter | Kerolox 1000 kg, Cd 0.1 |
| `tank_m1` | CoreTank | 1×3 | 400 | 3 | `t1_heavy` | Kerolox 6000 kg, Cd 0.1 |
| `tank_h1` | CoreTank | 1×2 | 150 | 2 | `t2_upper_stage` | Hydrolox 1800 kg, Cd 0.1 |
| `tank_lm1` | CoreTank | 1×1 | 80 | 1 | `t3_lander` | Methalox 400 kg, Cd 0.1 |
| `engine_lv1` | LiquidEngine | 1×1 | 300 | 2 | starter | Kerolox, ThrustVac 140 000, Isp 270/300, gimbal 5°, MinThrottle 0.5, ignitions 1 |
| `engine_lv2` | LiquidEngine | 1×1 | 600 | 3 | `t2_main_engine` | Kerolox, 260 000, Isp 280/310, 5°, 0.5, 1 |
| `engine_vac1` | LiquidEngine | 1×1 | 250 | 3 | `t2_upper_stage` | Hydrolox, 60 000, Isp 200/340, 3°, 0.3, 3 |
| `engine_land1` | LiquidEngine | 1×1 | 120 | 2 | `t3_lander` | Methalox, 25 000, Isp 290/320, 6°, 0.15, 4 |
| `booster_s1` | SolidBooster | 1×3 | 300 | 2 | starter | ThrustVac 130 000, Isp 230/250, thuốc 1500 kg, BurnCurve 1.0 → 0.8 |
| `booster_m1` | SolidBooster | 1×3 | 500 | 3 | `t1_heavy` | 135 000, Isp 230/250, thuốc 5000 kg, BurnCurve 1.0 → 0.8 |
| `decoupler_d1` | (decoupler) | cạnh | 30 | 1 | starter | AttachmentStrength 300 000, EjectionImpulse 3000, FrontalArea 0.5, Cd 0.1 |
| `fin_f1` | AeroSurface | 1×1 | 40 | 1 | starter | LiftCoefficient 0.8, PlanformArea 1.5, MaxDeflection 20° |
| `structural_s1` | Structural | 1×1 | 50 | 1 | starter | AttachmentStrength 500 000, Cd 0.1 |

Decoupler cần một `PartGroup` trong definition: đặt `Structural` (không dùng cho luật hàng xóm vì decoupler nằm trên cạnh).

**AllowedNeighbors** theo bảng ở mục 5 của tab "Thiết kế hợp nhất":

| Nhóm | AllowedTop | AllowedBottom | AllowedSides |
| --- | --- | --- | --- |
| Fairing | – | CommandPod, Parachute | – |
| Parachute | Fairing | CommandPod | – |
| CommandPod | Fairing, Parachute | CoreTank, Structural, HeatShield | – |
| HeatShield | CommandPod, CoreTank, LiquidEngine | CoreTank, Structural | – |
| CoreTank | CommandPod, CoreTank, LiquidEngine, Structural, HeatShield | CoreTank, LiquidEngine, Structural, HeatShield | SolidBooster, AeroSurface |
| LiquidEngine | CoreTank | CoreTank, Structural, HeatShield | – |
| SolidBooster | – | – | CoreTank, AeroSurface |
| AeroSurface | – | – | CoreTank, SolidBooster |
| Structural | CommandPod, CoreTank, LiquidEngine, Structural, HeatShield | CoreTank, Structural | – |

**Module**

| ModelId | Lớp / Kind | Slot | Dry (kg) | Weight | PowerDraw (W) | Mở bởi | Thông số riêng |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `imu_g1` | Acs / Imu | Avionics | 15 | 1 | 20 | starter | Accuracy 1800″, Drift 0.2°/phút, Latency 80 ms, SAS: StabilityAssist |
| `imu_g2` | Acs / Imu | Avionics | 15 | 1 | 25 | `t1_avionics` | 720″, 0.05°/phút, 40 ms, SAS: cả 4 mode |
| `startracker_st1` | Acs / StarTracker | Avionics | 10 | 1 | 15 | `t3_navigation` | 5″, drift 0, 200 ms, SAS: cả 4 mode |
| `wheel_rw1` | Acs / ReactionWheel | Avionics | 40 | 1 | 40 | `t1_avionics` | ControlAuthority 3000, MaxMomentum 30 000 |
| `rcs_r1` | Acs / RcsThruster | Avionics | 30 | 1 | 10 | `t3_navigation` | ControlAuthority 2000, Monoprop 40 kg |
| `battery_b1` | Power / Battery | Power | 20 | 1 | 0 | starter | 500 Wh |
| `battery_b2` | Power / Battery | Power | 30 | 1 | 0 | `t2_power` | 2000 Wh |
| `solar_s1` | Power / SolarArray | Power | 25 | 1 | 0 | `t2_power` | NominalOutput 300 W (ở 1361 W/m²) |
| `rtg_t1` | Power / Rtg | Power | 45 | 2 | 0 | `t4_rtg` | 120 W, Degradation 0.008/năm, WasteHeat 1500 W |
| `insulation_i1` | Thermal / PassiveInsulation | Thermal | 15 | 1 | 0 | `t3_thermal` | ReflectiveEfficiency 0.7 |
| `radiator_a1` | Thermal / ActiveRadiator | Thermal | 25 | 1 | 15 | `t3_thermal` | MaxHeatRejection 2000 W |
| `rhu_h1` | Thermal / RadioisotopeHeater | Thermal | 5 | 1 | 0 | `t3_thermal` | HeaterOutput 50 W |

Module dùng mặc định `MaxSafeTempK` 350, `MinSafeTempK` 230.

## 8. Không làm

- Không viết công thức lực, nhiệt, điện (B làm). `RocketPart.CalculateAero` để stub.
- Không reparent node, không tạo debris, không đụng `VehicleRoot` (C làm).
- Không viết logic tech tree; chỉ gọi `ResearchCampaignEngine.Instance.TechTree`.
- Không xoay part, không mirror, không nhiều slot save (ngoài phạm vi).
