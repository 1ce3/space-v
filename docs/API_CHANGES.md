# API changes

Một dòng cho mỗi thay đổi hợp đồng (chỉ thêm, không đổi tên hay xoá). Định dạng:

`Giờ | Người | Thay đổi | Lý do`

| Giờ | Người | Thay đổi | Lý do |
| --- | --- | --- | --- |
| H1 | C | `TelemetryFrame` thêm `AngularRateDegPerSec`, `FlightPathAngleDeg` (ngoài các field đã ghi ở C_CONTROL 4.3) | Estimator và SAS Prograde cần, lấy từ frame tick trước |
| H1 | C | `FlightControlSubsystem.Update(float deltaTime, TelemetryFrame lastFrame)`; thêm `Bind(PartTree)` cho subsystem, `AttitudeEstimator`, `ControlAllocator`, `ThrottleController`, `AnomalyResponseHandler`, và `StagingManager.Bind(PartTree, List<StageGroup>)` | Part tạo lúc chạy nên truyền bằng Bind (AGENTS 4.7) |
| H1 | C | `AttitudeEstimator.GetEnabledSasModes()` trả `SasModeMask` thay vì `List<SasMode>` | Theo quyết định #8 |
| H1 | C | `PilotInput` thêm `HasSasModeRequest`; `CruiseController` export `InputHandler InputHandler` (không đặt tên `Input`) | Tránh che `Godot.Input` |
| H1 | C | `BlackBoxTelemetryLogger` thêm `RecordFailure(FailureCause, RocketPart)`, `FlightTime`, `CruiseDaysSurvived`, `LandingSpeedMps`, `FailedPartGroup`, `MissionId`; `FlightDirector.OnBreakTriggered(RocketPart, FailureCause)` | Đủ field cho `FlightResult` |
| H1 | C | `CruiseHUD` thêm signal `TransferBurnRequested`, `PauseToggled(bool)`, `TimeScaleRequested(float)` và `ShowDeltaV(remaining, required)`; `VictoryScreen` signal `ContinueRequested`, `QuitRequested` | Nút của C_CONTROL 6 |
| H1 | C | PID gain (`Kp`, `Ki`, `Kd`, scale, `IntegralLimit`) chỉ nằm trong `FlightSettings`; `PIDStabilizer` export `Settings` | Một file .tres để cân bằng |
| H1 | C | Tạm thời: `tests/c/contract_stubs/` + hằng `C_STUB_A`, `C_STUB_B` trong csproj | Nhánh C build được trước khi skeleton A/B vào main; C gỡ khi ghép M0 |
| H1 | C | AGENTS 5: viết `Godot.Control` thay cho `Control` | Namespace `RocketGame.Control` che class `Godot.Control` |
