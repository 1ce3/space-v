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
