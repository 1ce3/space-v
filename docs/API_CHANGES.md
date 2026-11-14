# API changes

Một dòng cho mỗi thay đổi hợp đồng (chỉ thêm, không đổi tên hay xoá). Định dạng:

`Giờ | Người | Thay đổi | Lý do`

| Giờ | Người | Thay đổi | Lý do |
| --- | --- | --- | --- |
| B0 | B | `PhysicsSettings.SampleMachDrag(float mach)`: trả hệ số cản theo Mach, dùng mặc định của guide nếu `MachDragCurve` null | Không phụ thuộc việc curve trong .tres nạp đúng |
| B0 | B | `ResearchCampaignEngine.AddRP(int)`, `SaveProgress()` (public) | Cộng RP cho debug/test; `TechTreeManager.PurchaseNode` gọi lưu game |
| B0 | B | Solver nhận `PartTree`: `PropulsionSolver.Solve(PartTree, float ambientPressure, Vector2 com, float dt)`, `AerodynamicSolver.Solve(PartTree, Vector2 airVelBody, AtmosphereSample, Vector2 com)`, `ThermalSolver.Step/SolveSteadyState(PartTree, ...)`, `PowerSolver.Step/SolveSteadyState(PartTree, ...)`, `HazardMonitor.CheckCryoLeak(PartTree)` | Diagram để tham số không kiểu (`engines`, `components`, `tanks`) |
| B0 | B | `ThermalStepResult`: `OverheatedComponents`, `BrokenComponents` là `List<string>` (ModelId), thêm `BrokenParts` (`List<RocketPart>`) và `LowestTempRatio`; `CruiseStepResult` thêm `BatteryFraction`, `BrokenComponents` là `List<string>`; `PowerStepResult` thêm `BatteryFraction` | Đủ dữ liệu cho HUD và FailureDetector |
| B0 | B | `PhysicsCoordinator` thêm `CurrentBody`, `[Export] HazardSeed`, `[Export] DebugLog`; `FailureDetector.BeginTick()`; `HazardMonitor.Configure(segment)`, `Acknowledge(type)`, `EnabledHazards`; `ThermalSolver.Configure(body)`; `CruiseSimulator.Settings`; `DebrisBody.Initialize(body, settings)` | Theo guide B 4.3, 4.4, 4.6 |
| B0 | B | `ResearchScreenRoot : Godot.Control` (`research/ui/`), signal `BuildRequested`, `[Export] Button BuildButton`; scene `scenes/research_screen.tscn` | Quyết định #22 |
| B0 | B | `StructuralBreakHandler.SpawnDebrisRigidBody(..., CelestialBodyDefinition body, SeverCause cause = SeverCause.Jettison)` thêm tham số mặc định `cause` | Để gọi `PartVisual.OnSevered(cause)` đúng loại cắt; C gọi cho Break thì truyền `SeverCause.Break` |
| A0 | A | `ParachutePartDefinition.AttachmentStrength` | Bảng mục 7 có số này; dùng làm `Joint.Strength` của joint tự tách |
| A0 | A | `BuildingSystem.ModuleChanged(ModuleSlotType, int)`, `RemoveModule(ModuleSlotType, int)`, `[Export] Node3D PartsRoot`; `PartCatalog.GetUnlocked<T>()`, `GetDecouplerPlaceholderScene()`; `BuildGrid.Occupy/Vacate/Normalize`; `RocketDesign.PartEntry/DecouplerEntry/ModuleEntry` (lồng bên trong); `MassCenterCalculator` là static | Slot module tách theo loại (quyết định #9) |
| A1 | A | `RocketPart.Create/Initialize`, `Tree`, `IsAttached`, `GetCellSize()`, property chuyển tiếp (`ModelId`, `PartGroup`, `Footprint`, ...) và definition có kiểu (`EngineDefinition`, `TankDefinition`, ...); `PodModule.Create/Initialize`, `Pod`, `SlotIndex`, `IsHealthy`, `GetCurrentMass()`; `CommandPodPart.AddModule` | Factory và truy cập tiện cho B, C |
| A1 | A | `PartTree.Add`, `PartTree.Link` (static), `GetSubtree(part, List)` không cấp phát; `GetSubtree` gồm cả decoupler của joint bị cắt; `Joint.Depth`, `Joint.IsSeparated` | Debris đủ part; mỗi joint chỉ tách một lần |
| A1 | A | `LiquidEnginePart.GetAvailableFuel()`, `CanStillFire()`; `FuelTankPart.GetFuelFraction()`; `HeatShieldPart.GetAblatorFraction()`; `BatteryModule.GetChargeFraction()`; `EnginePart.SeaLevelPressurePa` | HUD và điều kiện hết nhiên liệu của FlightDirector |
| A2 | A | `TestRocketFactory.GetPart(id)`, `GetModule(id)`, `DefaultPayloadMassKg` | Definition dự phòng khi chưa có .tres |
| H1 | C | `TelemetryFrame` thêm `AngularRateDegPerSec`, `FlightPathAngleDeg` (ngoài các field đã ghi ở C_CONTROL 4.3) | Estimator và SAS Prograde cần, lấy từ frame tick trước |
| H1 | C | `FlightControlSubsystem.Update(float deltaTime, TelemetryFrame lastFrame)`; thêm `Bind(PartTree)` cho subsystem, `AttitudeEstimator`, `ControlAllocator`, `ThrottleController`, `AnomalyResponseHandler`, và `StagingManager.Bind(PartTree, List<StageGroup>)` | Part tạo lúc chạy nên truyền bằng Bind (AGENTS 4.7) |
| H1 | C | `AttitudeEstimator.GetEnabledSasModes()` trả `SasModeMask` thay vì `List<SasMode>` | Theo quyết định #8 |
| H1 | C | `PilotInput` thêm `HasSasModeRequest`; `CruiseController` export `InputHandler InputHandler` (không đặt tên `Input`) | Tránh che `Godot.Input` |
| H1 | C | `BlackBoxTelemetryLogger` thêm `RecordFailure(FailureCause, RocketPart)`, `FlightTime`, `CruiseDaysSurvived`, `LandingSpeedMps`, `FailedPartGroup`, `MissionId`; `FlightDirector.OnBreakTriggered(RocketPart, FailureCause)` | Đủ field cho `FlightResult` |
| H1 | C | `CruiseHUD` thêm signal `TransferBurnRequested`, `PauseToggled(bool)`, `TimeScaleRequested(float)` và `ShowDeltaV(remaining, required)`; `VictoryScreen` signal `ContinueRequested`, `QuitRequested` | Nút của C_CONTROL 6 |
| H1 | C | PID gain (`Kp`, `Ki`, `Kd`, scale, `IntegralLimit`) chỉ nằm trong `FlightSettings`; `PIDStabilizer` export `Settings` | Một file .tres để cân bằng |
| H1 | C | Tạm thời: `tests/c/contract_stubs/` + hằng `C_STUB_A`, `C_STUB_B` trong csproj (đã gỡ ở M0) | Nhánh C build được trước khi skeleton A/B vào main |
| H1 | C | AGENTS 5: viết `Godot.Control` thay cho `Control` | Namespace `RocketGame.Control` che class `Godot.Control` |
| M0 | C | AGENTS 5: file dùng `Side` thêm `using Side = RocketGame.Core.Side;` | Godot cũng có enum `Side` (lỗi CS0104) |
