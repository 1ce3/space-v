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
| B2 | B | `PropulsionSolver.Solve(..., List<PartLoad> partLoads = null, List<FlightEvent> events = null)`, `AerodynamicSolver.Solve(..., List<PartLoad> partLoads = null)` thêm tham số mặc định; `AerodynamicSolver.Reset()` | Coordinator gom tải từng part cho kiểm tra kết cấu; WindTunnelAnalyzer (A) gọi như cũ |
| B2 | B | `PhysicsSettings.FrontalDragOnlyExposed` (bool, mặc định false = đúng guide B 4.3.2) | Công tắc cân bằng: true thì chỉ part có mặt trước không bị part khác che mới tính lực cản mặt trước |
| A0 | A | `ParachutePartDefinition.AttachmentStrength` | Bảng mục 7 có số này; dùng làm `Joint.Strength` của joint tự tách |
| A0 | A | `BuildingSystem.ModuleChanged(ModuleSlotType, int)`, `RemoveModule(ModuleSlotType, int)`, `[Export] Node3D PartsRoot`; `PartCatalog.GetUnlocked<T>()`, `GetDecouplerPlaceholderScene()`; `BuildGrid.Occupy/Vacate/Normalize`; `RocketDesign.PartEntry/DecouplerEntry/ModuleEntry` (lồng bên trong); `MassCenterCalculator` là static | Slot module tách theo loại (quyết định #9) |
| A1 | A | `RocketPart.Create/Initialize`, `Tree`, `IsAttached`, `GetCellSize()`, property chuyển tiếp (`ModelId`, `PartGroup`, `Footprint`, ...) và definition có kiểu (`EngineDefinition`, `TankDefinition`, ...); `PodModule.Create/Initialize`, `Pod`, `SlotIndex`, `IsHealthy`, `GetCurrentMass()`; `CommandPodPart.AddModule` | Factory và truy cập tiện cho B, C |
| A1 | A | `PartTree.Add`, `PartTree.Link` (static), `GetSubtree(part, List)` không cấp phát; `GetSubtree` gồm cả decoupler của joint bị cắt; `Joint.Depth`, `Joint.IsSeparated` | Debris đủ part; mỗi joint chỉ tách một lần |
| A1 | A | `LiquidEnginePart.GetAvailableFuel()`, `CanStillFire()`; `FuelTankPart.GetFuelFraction()`; `HeatShieldPart.GetAblatorFraction()`; `BatteryModule.GetChargeFraction()`; `EnginePart.SeaLevelPressurePa` | HUD và điều kiện hết nhiên liệu của FlightDirector |
| A2 | A | `TestRocketFactory.GetPart(id)`, `GetModule(id)`, `DefaultPayloadMassKg` | Definition dự phòng khi chưa có .tres |
| A3 | A | `BuildGrid`: `Parts`, `Decouplers`, `IsInside`, `AddDecoupler`, `RemoveDecoupler`, `GetDecouplerAt`, `GetDecouplersBetween`, `GetOrphanDecouplers`, `Clear`; static `GetOtherCell`, `Offset`, `Opposite`, `GetFootprintCells` | RocketAssembler (A4) và BuildingSystem (A6, A7) dùng |
| A3 | A | `RocketDesign.AddPart/AddDecoupler/AddModule/Clear/IsEmpty`, `SaveTo(path)`, `LoadFrom(path)`, `FormatVersion`; JSON `{version, parts[{id,x,y}], decouplers[{id,x,y,side}], modules[{id,slot,index}]}` | Test lưu/đọc không ghi đè design thật |
| A3 | A | 30 `.tres` trong `data/parts/`, `data/modules/` và `data/settings/build_settings.tres`; `PartCatalog` nạp `.tres` lúc `_EnterTree`, thêm `[Export] DebugLog` (làm sớm phần dữ liệu của A5) | Test A3, A4 cần definition thật; B cần trước H8 |
| A4 | A | `RocketAssembler.ValidateConnections(design, int maxWeight)` (hằng `MaxWeightFromTechTree`, `NoWeightLimit`), `LinkShielding(tree)`, `GetDepth(part)`, `LastGrid`; assembler đặt `RocketPart.Position` = `GetLocalPosition()` (z = 0); `TestRocketFactory.CreateMarsStack()` cần autoload `PartCatalog` | Test và build screen kiểm lỗi không phụ thuộc tech tree; C không phải tự đặt vị trí part |
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
| B3 | B | `ThermalSolver.Step(PartTree, float heatFlux, float dt, Vector2 velDirBody = default, Vector2 com = default, float airTemperatureK = 3f)` thêm tham số mặc định | Cần hướng bay để biết đáy trước / mũi trước, và nhiệt độ khí để tính bức xạ |
| B3 | B | Luật: heat shield hết ablator mà vẫn bị đốt nóng thì `Broken` (cháy thủng) → `FailureDetector.MonitorThermalLimits` gây Break `Thermal` | Đúng mục "Xong khi" B3; nếu chỉ dồn nhiệt dư vào TempK thì shield gần như không bao giờ hỏng |
| B4 | B | `ResearchScreenRoot`: export `RPLabel`, `WeightLabel`, `StatusLabel`, `Columns`, `Background`, `HeaderPanel`, `RPBadge`, `WeightBadge`, màu và kích thước giao diện, `DebugStartRP`; public `TryPurchase(nodeId)`, `Refresh()` | Màn hình research (guide B 6.4); `TryPurchase` dùng chung cho nút và sandbox |
