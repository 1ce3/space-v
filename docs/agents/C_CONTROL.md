# Hướng dẫn cho Dev C: Control system và tích hợp

Bạn là AI coding agent của **Dev C**. Đọc `AGENTS.md` ở gốc repo trước file này; mọi quy ước ở đó (đặt tên, `[Export]`, signal, thư mục, git, dấu và hệ toạ độ) đều áp dụng.

Dev C còn là **người tích hợp**: dựng project, giữ `main` luôn build được, chạy smoke test ở mỗi mốc.

## 1. Phạm vi

Bạn sở hữu và chỉ sửa:

- `project.godot`, `RocketGame.csproj`, `.sln`, `.gitignore`, `docs/API_CHANGES.md`, `docs/.gdignore`
- `core/` (code ở `AGENTS.md` mục 7)
- `control/` (flow, flight, piloting, cruise, ui)
- `presentation/flight/` (`FlightCameraRig`, `FlightVfxDirector`, `FlightAudioDirector`, `BodyEnvironmentVisual`)
- `data/settings/flight_settings.tres`
- `scenes/main.tscn`, `flight.tscn`, `debrief.tscn`, `victory.tscn`, `control/flow/game_flow_manager.tscn`
- `tests/c/`

Diagram cần đọc: `docs/design/diagrams/05_control_system.mmd`, `00_overview.mmd`, và để biết part có gì: `02b_building_runtime_parts.mmd`, `02c_building_pod_modules.mmd`, `03_physics_engine.mmd`.

## 2. Phụ thuộc

| Bạn cần | Từ ai | Khi nào | Trong lúc chờ |
| --- | --- | --- | --- |
| Lớp part và module thật, `TestRocketFactory.CreateMinimal()` | A | **H3** | Dựng flight scene, GameFlow trên stub |
| `PhysicsCoordinator.ExecutePhysicsStep` thật | B | **H5.5** | Stub của B trả lực 0; tạm cộng trọng lực + lực đẩy đơn giản trong `tests/c/` để thử ga và tách tầng |
| `FailureDetector`, `StructuralBreakHandler.SpawnDebrisRigidBody` | B | H8 | Tạm ẩn part bị cắt thay vì tạo debris |
| `BuildScreenRoot` phát `DesignConfirmed` | A | H10.5 | Sandbox bay thẳng bằng `TestRocketFactory` |
| `ResearchScreenRoot`, `ResearchCampaignEngine.ProcessMissionEnd` | B | H10.5 | Debrief hiện `FlightResult` thô |
| `CruiseSimulator` | B | H13 | `CruiseController` đếm ngày, chưa kiểm tra tài nguyên |

| Người khác cần | Thứ gì | Hạn chót |
| --- | --- | --- |
| A, B | Project mở được, `core/` đủ, input map, autoload đăng ký, nhánh git | **H0:45** |
| A, B | Stub mọi class trong 05; `main` build được | **M0 (H1.5)** |
| B | `VehicleRoot` gọi `ExecutePhysicsStep` mỗi tick để thử lực trong game thật | H5 |
| Cả nhóm | Smoke test và merge ở M1, M2, M3, M4 | Mỗi mốc |

## 3. Danh sách việc theo thứ tự

### C0. Dựng project (H0–H0:45, P0, hạn chót cứng H0:45)

- Project Godot 4.7 .NET tên `RocketGame` ở gốc repo, cây thư mục ở `AGENTS.md` mục 2, file `docs/.gdignore` (trống), `docs/API_CHANGES.md` (trống), `.gitignore`.
- 4 file Core ở `AGENTS.md` mục 7, chép nguyên văn.
- Project Settings: `physics/3d/physics_engine` = Jolt Physics; `physics/common/physics_ticks_per_second` = 60; `physics/3d/default_gravity` = 0; main scene = `scenes/main.tscn`.
- Input map đủ action ở mục 7.
- Ba autoload dạng scene (mỗi scene một node gốc mang script stub có `Instance`): `PartCatalog` → `building/editor/part_catalog.tscn`, `ResearchCampaignEngine` → `research/research_campaign_engine.tscn`, `GameFlowManager` → `control/flow/game_flow_manager.tscn`. A và B sẽ thay nội dung scene của họ.
- Tạo 3 nhánh `dev/*`.
- **Xong khi:** `dotnet build` sạch, mở project không lỗi, push `main`. Báo người dùng nhắn A và B.

### C1. Skeleton (H0:45–H1.5, P0)

- Mọi class trong 05 với đúng chữ ký (gồm bổ sung ở `AGENTS.md` mục 8 và mục 4 file này), thêm `FlightScene` (gốc flight scene) và `DebriefScreen`, `VictoryScreen`.
- Kéo `dev/a-*` và `dev/b-*` vào `main` khi họ xong skeleton; sửa lỗi build do ghép (chỉ sửa trong thư mục bạn; lỗi trong thư mục người khác thì báo họ).
- **Xong khi:** `main` build sạch với stub của cả 3 người (mốc **M0**).

### C2. GameFlow, flight scene, VehicleRoot cơ bản (H1.5–H3.5, P0)

- `GameFlowManager` (máy trạng thái, đổi scene thủ công, mục 4.1), `MissionRun`, `FlightScene`, bố cục `flight.tscn` (mục 4.2).
- `VehicleRoot.OnDesignConfirmed`, tạo collision shape, `RecalculatePhysicalProperties`, `FlightCameraRig` bám theo.
- Sandbox `tests/c/flight_sandbox.tscn`: nạp `TestRocketFactory.CreateMinimal()` thẳng vào flight scene, không qua build screen.
- **Xong khi:** sandbox hiện rocket đứng trên bệ với scene tạm của A; camera nhìn thấy nó.

### C3. Vòng tick, ga, tách tầng (H3.5–H6, P0, mốc M1)

- `VehicleRoot._IntegrateForces` theo mục 4.3; giữ trên bệ; cắt part trì hoãn và gọi debris.
- `InputHandler`, `ThrottleController`, `StagingManager`, `FlightHUD` tối thiểu (số liệu dạng chữ).
- **Xong khi (M1, cùng B):** trong sandbox, bấm Z rồi Space thì rocket cất cánh với lực của B, hết nhiên liệu thì rơi; với `CreateMarsStack()` bấm Space tách booster và tầng lõi, phần tách rơi thành debris. Merge cả 3 nhánh, chạy smoke test M1 (mục 8).

### C4. Chuỗi lái (H6–H8.5, P0)

- `AttitudeEstimator`, `AttitudeController` (5 mode), `PIDStabilizer`, `ControlAllocator`, thứ tự trong `FlightControlSubsystem.Update` (mục 5).
- **Xong khi:** SAS `StabilityAssist` giữ rocket không lắc khi thả phím; `Prograde` bám hướng vận tốc khi bay nghiêng; mất mọi sensor thì SAS tự về `Off`.

### C5. Kết thúc chuyến bay và vòng chặng 1 (H8.5–H10.5, P0, mốc M2)

- `FlightDirector` (pha, điều kiện kết thúc chặng 1), chạm đất bằng giải tích, nối `FailureDetector.BreakTriggered`, `BlackBoxTelemetryLogger`, `DebriefScreen`.
- Nối GameFlow với `BuildScreenRoot` (A) và `ResearchScreenRoot` (B).
- **Xong khi (M2):** chạy game từ `main.tscn`: xây rocket → phóng → lên quỹ đạo hoặc hỏng → debrief hiện RP nhận được → mua node ở research → về xưởng thấy part mới. Merge, smoke test M2.

### C6. Chặng 2 và chặng 3 (H10.5–H13.5, P0, mốc M3)

- `CruiseController`, `CruiseHUD` (mục 6), `VehicleRoot.SetSimulationFrozen`, `VehicleRoot.RestoreFromRun`, pha `Entry` / `Descent` / `Landed`, kiểm tra đáp, `VictoryScreen`, đổi môi trường Trái Đất / sao Hỏa trong `BodyEnvironmentVisual` (bản tối thiểu, mục 4.6).
- **Xong khi (M3, H14):** Mars stack mẫu đi hết 3 chặng và đáp được, màn Victory hiện. Smoke test M3.

### C7. Sau M3 (H14–H16, P1 rồi P2)

- `AnomalyResponseHandler` cho các event B phát (mục 5.6), hiện trên HUD (P1).
- HUD đầy đủ: thanh tiến độ tới mục tiêu, danh sách stage, chỉ báo SAS và mode dùng được, `RadarAltitudeM`, `AblatorFraction` (P1).
- `FlightVfxDirector`, `FlightAudioDirector` (P2).

### C8. Đóng gói (H16–H18, P0)

- Từ H16 chỉ sửa lỗi. Export preset Windows Desktop, xuất build vào `export/`, chạy smoke test M4 trên bản build, ghi `README.md` cách chạy và điều khiển.

## 4. Chi tiết: game flow và VehicleRoot

### 4.1 `GameFlowManager` (autoload)

- `control/flow/game_flow_manager.tscn`, gốc mang script; `[Export] PackedScene BuildScreen, ResearchScreen, FlightScene, DebriefScene, VictoryScene`.
- **Đổi scene thủ công** để nối signal trước `_Ready`: `var root = scene.Instantiate(); <nối signal>; GetTree().Root.AddChild(root); GetTree().CurrentScene = root;` rồi `QueueFree()` scene cũ.
- Trạng thái và chuyển:

| Từ | Sự kiện | Tới | Việc làm |
| --- | --- | --- | --- |
| (khởi động) | | Build | |
| Build | `BuildScreenRoot.Building.DesignConfirmed(rocket)` | Flight | Tạo `MissionRun(ResearchCampaignEngine.Instance.CurrentMission, rocket)`; nạp flight scene; `Director.StartSegment(run)` |
| Build | `BuildScreenRoot.ResearchRequested` | Research | |
| Research | `ResearchScreenRoot.BuildRequested` | Build | |
| Flight | `FlightDirector.SegmentCompleted(run)` ở chặng 1 | Cruise | `run.Advance()`; `FlightScene.Cruise.StartCruise(run)` (cùng scene) |
| Cruise | `CruiseController.SegmentCompleted(run)` | Flight | `run.Advance()`; `Director.StartSegment(run)` |
| Flight, Cruise | `MissionEnded(result)` | Debrief | `reward = ResearchCampaignEngine.Instance.ProcessMissionEnd(result)`; đóng flight scene; `DebriefScreen.Show(result, reward)` |
| Debrief | `DebriefScreen.ContinueRequested` | Victory nếu `IsCampaignComplete()`, ngược lại Research | |

- `MissionRun : RefCounted`: `Mission`, `AssembledRocket Rocket`, `SegmentIndex`, `SegmentsCompleted`, `CurrentSegment` (property), `PartTree`, `Stages` (lấy từ `Rocket`), `ElapsedDays`, `BlackBoxTelemetryLogger BlackBox` (class thường); `Advance()` tăng `SegmentIndex` và `SegmentsCompleted`.

### 4.2 Bố cục `flight.tscn`

```
FlightScene (Node3D, script FlightScene: [Export] Director, Cruise, Vehicle)
├─ Environment (BodyEnvironmentVisual)
├─ Physics (PhysicsCoordinator của B, [Export] Settings)
├─ VehicleRoot (RigidBody3D)
│  ├─ FlightControlSubsystem (Node) với con InputHandler, AttitudeEstimator, AttitudeController,
│  │   PIDStabilizer, ControlAllocator, ThrottleController, AnomalyResponseHandler
│  └─ StagingManager (Node)
├─ FlightDirector (Node)
├─ CruiseController (Node)
├─ CameraRig (FlightCameraRig)
├─ VfxDirector, AudioDirector (P2)
└─ UI (CanvasLayer): FlightHUD, CruiseHUD
```

Mọi tham chiếu chéo giữa các node trên là `[Export]` kéo thả trong Inspector (ví dụ `VehicleRoot.Coordinator`, `FlightDirector.Vehicle`, `CruiseController.Hud`). Các lớp con của `FlightControlSubsystem` là Node để mang `[Export]` thông số.

### 4.3 `VehicleRoot`

Cấu hình: `AxisLockLinearZ`, `AxisLockAngularX`, `AxisLockAngularY` = true; `GravityScale` = 0; `CenterOfMassMode` = Custom; `CanSleep` = false; damp = 0 với `DampMode` Replace; `ContactMonitor` = false.

`OnDesignConfirmed(AssembledRocket rocket)`:

1. Mỗi part trong `PartTree.ActiveParts`: `AddChild(part)`, `part.Position = new Vector3(local.X, local.Y, 0)` với `local = part.GetLocalPosition()`.
2. Mỗi part một `CollisionShape3D` con **trực tiếp** của `VehicleRoot` (BoxShape3D theo footprint × `CellSize`), layer 0, mask 0; giữ `Dictionary<RocketPart, CollisionShape3D>`.
3. `Mass`, `CenterOfMass`, `Inertia = new Vector3(I, I, I)` từ `rocket.MassProperties`.
4. `Staging.Stages = rocket.Stages`; nối `PartTree.PartSevered`.
5. Đặt rocket sao cho đáy part thấp nhất ở độ cao 0 và cột giữa ở x = 0. `_onPad = true`.

`_IntegrateForces(PhysicsDirectBodyState3D state)`, mỗi tick theo thứ tự (khớp mục 9 tab thiết kế):

1. Nếu `_frozen` (chặng 2): `state.LinearVelocity = Vector3.Zero; state.AngularVelocity = Vector3.Zero;` rồi return.
2. Đọc trạng thái: CoM thế giới, vận tốc, `BodyRotationRad = Mathf.Atan2(basis.X.Y, basis.X.X)`, `AttitudeDeg`, tốc độ góc (độ/giây).
3. `FlightControl.Update(dt, _lastFrame)` ghi lệnh vào part và module.
4. `output = Coordinator.ExecutePhysicsStep(input)`.
5. **Trên bệ:** nếu `_onPad` và thành phần thẳng đứng của `NetForce` ≤ 0 thì giữ vận tốc = 0 và không áp lực; ngược lại `_onPad = false`.
6. `state.ApplyCentralForce(new Vector3(F.X, F.Y, 0))`, `state.ApplyTorque(new Vector3(0, 0, NetTorqueZ))`.
7. `Coordinator.StressEvaluator.EvaluateRecursiveLoad(input, output)`; `report = Coordinator.StressEvaluator.CheckYieldLimits()`; `Coordinator.Failures.MonitorStressThresholds(report)`; `Coordinator.Failures.MonitorThermalLimits(output.Thermal)`.
8. Khối lượng: `Mass -= ConsumedMass` (property của `RigidBody3D`) và `MassProps.TotalMass` cùng giảm mỗi tick; CoM và quán tính tính lại 10 lần/giây khi đang đốt (`[Export] MassRecalcHz`), và ngay khi có cắt part.
9. **Chạm đất (giải tích):** độ cao thấp nhất trong 4 góc footprint của mọi part = `RadarAltitudeM`. Nếu ≤ 0 và không `_onPad`: gọi `Director.OnGroundContact(partThấpNhất, |v|)`.
10. `_lastFrame = GetTelemetry()`; `Director.OnTick(_lastFrame)`; `Run.BlackBox.LogFlightMetrics(_lastFrame)`; `Anomalies.OnFlightEvents(output.Events)`.

**Cắt part trì hoãn:** `OnPartSevered(part, cause, impulseNs)` chỉ đưa vào hàng đợi và `CallDeferred(nameof(ProcessPendingSevers))`, vì không được xoá shape hay reparent node giữa callback physics. `ProcessPendingSevers`: lấy `parts = PartTree.GetSubtree(part)`, xoá collision shape của chúng, hướng xung = từ `part.ParentJoint.ParentPart` sang `part` (hệ thế giới), gọi `Coordinator.BreakHandler.SpawnDebrisRigidBody(parts, LinearVelocity, AngularVelocity, dir × impulseNs, GetParent<Node3D>(), body)`, `ApplyCentralImpulse(−dir × impulseNs)`, rồi `RecalculatePhysicalProperties()`.

`RecalculatePhysicalProperties()`: `MassProps = MassCenterCalculator.Calculate(PartTree.ActiveParts)` → đặt `Mass`, `CenterOfMass`, `Inertia` của rigid body; phát `MassPropertiesChanged()`. (Field `MassProperties` của `VehicleRoot` tên là `MassProps`, không phải `Mass`, vì `RigidBody3D` đã có `Mass`.)

`SetSimulationFrozen(bool frozen)`: bật hoặc tắt `_frozen`.

`RestoreFromRun(MissionRun run)` (đầu chặng 3): `Coordinator.Initialize(run.CurrentSegment)`; đặt CoM tại `(0, EntryAltitudeM)` trong hệ của sao Hỏa (tâm `(0, −Radius)`); vận tốc = `EntrySpeedMps × (cos γ, sin γ)` với γ = `EntryAngleDeg`; xoay thân cho mũi trùng hướng vận tốc (người chơi tự quay đáy về trước); tốc độ góc 0; `_onPad = false`; `SetSimulationFrozen(false)`; `Environment.SetBody(body)`.

`GetTelemetry()` trả `TelemetryFrame` (struct) đủ field trong diagram 05, cộng `HorizontalSpeedMps`, `HeatFlux`, `Phase`, `RadarAltitudeM`, `AblatorFraction`, `ElapsedDays`.

### 4.4 `FlightDirector`

- `StartSegment(MissionRun run)`: chặng EarthAscent → `Coordinator.Initialize(segment)`, `Vehicle.OnDesignConfirmed(run.Rocket)`, pha `PreLaunch`; chặng MarsEdl → `Vehicle.RestoreFromRun(run)`, pha `Entry`, mở lại input.
- Chuyển pha: `PreLaunch` → `PoweredAscent` khi `StageActivated(0)`; `PoweredAscent` ↔ `Coast` theo có động cơ nào `IsIgnited`; `Entry` → `Descent` khi dù `Deployed` hoặc một động cơ cháy lần đầu.
- **Qua chặng 1:** `AltitudeM ≥ TargetAltitudeM` và tốc độ ngang ≥ `TargetSpeedMps` → pha `OrbitReached`, cắt ga, khoá input, phát `SegmentCompleted(run)`.
- **Hỏng:**

| Điều kiện | `FailureCause` |
| --- | --- |
| `FailureDetector.BreakTriggered(part, cause)` | `cause` (ghi part vào black box) |
| `OnGroundContact` ở chặng 1 sau khi rời bệ | `MonitorImpact` gãy thì `Collision`; không gãy thì `TargetNotReached` |
| Chặng 1: không động cơ nào cháy được nữa (không `IsIgnited`, không động cơ lỏng `IsArmed` còn nhiên liệu và lần đánh lửa, không stage nào còn nạp động cơ) và tốc độ thẳng đứng < 0 | `TargetNotReached` |
| Chặng 3: `OnGroundContact` và `MonitorImpact` gãy | `Collision` |
| Chặng 3: chạm đất không gãy nhưng nghiêng `|AttitudeDeg − 90| > MaxLandingTiltDeg`, hoặc pod không còn trong cây | `TippedOver` |
| `flight_abort` | `Aborted` |

- **Đáp thành công:** chặng 3, chạm đất, không gãy, pod còn, nghiêng trong giới hạn → pha `Landed`, `SetSimulationFrozen(true)`, `LandingSpeedMps` vào black box, phát `MissionEnded` với `Outcome = Success`.
- Mọi lần kết thúc: khoá input, `result = run.BlackBox.BuildResult(outcome)` (điền `SegmentsCompleted`, `MissionId`, `FailureCause`, `FailedModelId`, `FailedPartGroup`), phát `MissionEnded(result)` đúng một lần.

### 4.5 `StagingManager`

- `ActivateNextStage()`: stage 0 chỉ gọi `Arm()` trên `EnginesToArm`. Stage sau: `TriggerSeparation()` trên một decoupler của mỗi joint trong stage, rồi `Arm()` các `EnginesToArm`, phát `StageActivated(index)`. Hết stage → `StagingWarning("No decouplers available to jettison")`, trả false.
- `JettisonFairing()`: còn `FairingPart` thì `Jettison()`, không thì `StagingWarning`.
- `ToggleParachute()`: dù `Stowed` → `Deploy()` (false thì `StagingWarning("Chưa thể mở dù: còn fairing")`); `Deployed` → `Cut()`.
- `CurrentStageIndex` sống qua cả 3 chặng (cùng scene).

### 4.6 Môi trường tối thiểu (P0 trong C6)

`BodyEnvironmentVisual`: nếu `body.EnvironmentScene` có thì instance nó; nếu không, tự tạo một dải đất phẳng (hộp mỏng ±50 km quanh x = 0 ở y = 0), một khối bệ phóng, và `WorldEnvironment` có màu trời `[Export]` cho từng `BodyId`. `UpdateSky(altitude, density)` nội suy màu trời sang đen theo `density / SeaLevelDensity`. Dải đất phẳng chỉ để nhìn; chạm đất vẫn tính theo độ cao thật.

## 5. Chi tiết: chuỗi lái

Thứ tự trong `FlightControlSubsystem.Update(dt, lastFrame)`:

1. `input = InputHandler.Poll()`.
2. `Anomalies.HandleInput(input)` (P1; P0 bỏ qua).
3. Lệnh rời: `StagePressed` → `ActivateNextStage()`; `FairingPressed` → `JettisonFairing()`; `ChutePressed` → `ToggleParachute()`; `RcsTogglePressed` → đảo `IsEnabled` của mọi RCS; `DeployTogglePressed` → đảo `SetDeployed` của solar và `SetExtended` của radiator; `SasTogglePressed`, `RequestedSasMode` → `AttitudeController.SetMode`.
4. `Estimator.Update(trueAttitudeDeg, trueRateDegPerSec, dt)`.
5. `u = AttitudeController.ComputeDemand(input, dt)`.
6. `Allocator.Allocate(u, lastFrame.DynamicPressure)`.
7. `ThrottleController.Update(input, lastFrame.GForce, dt)`.

### 5.1 `InputHandler`

Đọc trục bằng `Input.GetAxis` mỗi lần `Poll()`; phím bấm một lần (`Stage`, `Fairing`, ...) chốt trong `_UnhandledInput` vào bản `PilotInput` đang chờ, `Poll()` trả và xoá chúng. `SetInputEnabled(false)` làm `Poll()` trả input rỗng.

### 5.2 `ThrottleController`

- `Throttle += ThrottleAxis × ThrottleRatePerSec × dt`; `ThrottleFull` → 1; `ThrottleCut` → 0; kẹp 0..1; `IsFrozen` thì giữ nguyên.
- G limiter (`GLimiterEnabled`): G > `MaxGForce` của pod thì `ClampThrottleForGLimit` hạ ga.
- Với mỗi `LiquidEnginePart` đang `IsArmed`: `Throttle > 0` và chưa cháy → `Ignite()`; `Throttle == 0` và đang cháy → `Shutdown()`; luôn `SetThrottle(Throttle)`.

### 5.3 `AttitudeEstimator`

- Sensor = `AttitudeSensorModule` trong pod đang `CanOperate()`. Không có → `HasValidAttitude = false`, ước lượng = giá trị cuối.
- Chọn sensor có `SensorAccuracyArcsec` nhỏ nhất. Star tracker bị mù (`IsBlinded`) khi độ cao < `StarTrackerMinAltitudeM`; star tracker chạy được thì đặt `AccumulatedDriftDeg` của mọi IMU về 0.
- IMU tích trôi: `AccumulatedDriftDeg += DriftRateDegPerMin / 60 × dt`.
- `Estimated = true + drift + nhiễu Gauss (σ = accuracy / 3600 độ)`, qua bộ lọc thông thấp với hằng số thời gian `ResponseLatencyMs`.
- `GetEnabledSasModes()` = OR của `EnabledSasModes` mọi sensor đang chạy.

### 5.4 `AttitudeController` và `PIDStabilizer`

- `SetMode(mode)`: trả false nếu mode không có trong `GetEnabledSasModes()`. Mất sensor thì tự về `Off`.
- `ComputeDemand`: có `PitchAxis ≠ 0` → `u = −PitchAxis` (ở `HoldPitch` thì dịch `TargetPitchDeg` thay vì lệnh thẳng). Không có phím:

| Mode | Mục tiêu |
| --- | --- |
| `Off` | `u = 0` |
| `StabilityAssist` | Lúc thả phím chốt `TargetPitchDeg` = góc hiện tại, rồi giữ |
| `HoldPitch` | `TargetPitchDeg` |
| `Prograde` | `FlightMath.FlightPathAngleDeg` |
| `Retrograde` | `WrapDeg(FlightPathAngleDeg + 180)` |

- `PIDStabilizer.Compute(errorDeg, rateDegPerSec, dt)`: `u = Kp × error / ErrorScaleDeg + Ki × ∫ − Kd × rate / RateScaleDegPerSec`, kẹp −1..1, kẹp tích phân. Gain và scale là `[Export]`, mặc định ở `flight_settings.tres`. `Reset()` khi đổi mode và khi nhận `MassPropertiesChanged()`.

### 5.5 `ControlAllocator` (P0)

- Một lệnh `u` cho mọi cơ cấu dùng được, có đảo dấu theo vị trí so với CoM (`sign = part.GetLocalPosition().Y < Vehicle.MassProps.CenterOfMass.Y ? 1 : −1`):
  - Động cơ đang cháy: `SetGimbal(sign × u × GimbalRangeDeg)`.
  - `ControlFinPart`: `SetDeflection(sign × u × MaxDeflectionDeg)`.
  (CoM lấy từ `Vehicle.MassProps.CenterOfMass`.)
  - `ReactionWheelModule` `CanOperate()`: `CommandTorque(u)`; nếu `IsSaturated` và có RCS bật thì `DumpMomentum()`.
  - `RcsThrusterModule` `IsEnabled`: `CommandTorque(u)`.
- Phân bổ theo thứ tự ưu tiên và giới hạn mô-men (P1).

### 5.6 `AnomalyResponseHandler` (P1)

| Event | Hậu quả khi bỏ mặc | Phím gỡ | Việc làm |
| --- | --- | --- | --- |
| `LightningStrike` | `IsTelemetryGlitched`, module avionics mất điện, SAS `Off` | `flight_sce_to_aux` | Bật lại điện avionics, hết glitch |
| `CryoLeak` | Tank mất nhiên liệu | `flight_seal_leak` | `SealLeak()` trên tank đang rò |
| `DebrisWarning` | Hết `TimeRemaining` thì B gây Break | `flight_rcs_left/right` | `CommandTranslate(axis)` của RCS |
| `SeuGlitch` | Safe mode: SAS `Off`, `ThrottleController.IsFrozen` | `flight_sas_toggle` | Thoát safe mode |
| `MaxQExceeded`, `GLimitExceeded`, `ComponentOverheat`, `PowerBrownout`, `EngineFlameout` | Chỉ cảnh báo trên HUD | – | – |

Xử lý xong thì `Coordinator.Hazards.Acknowledge(type)` và phát `AnomalyResolved`. `ActiveAnomaly : RefCounted`.

## 6. Chi tiết: chặng hành trình (`CruiseController`)

- Node trong `flight.tscn`, `[Export] VehicleRoot Vehicle`, `CruiseHUD Hud`, `FlightHUD FlightHud`, `InputHandler InputHandler` (không đặt tên `Input` để khỏi che `Godot.Input`), `StagingManager Staging`, `float DaysPerSecond = 3.5`.
- `StartCruise(run)`: `Vehicle.SetSimulationFrozen(true)`; ẩn `FlightHud`, hiện `Hud`; `_sim = new CruiseSimulator()`; `_sim.Begin(run.PartTree, run.CurrentSegment, mission.Segments[0].Body, mission.Segments[2].Body)`.
- **Trước cú đốt:** HUD hiện `GetRemainingDeltaV()` cạnh `RequiredDeltaVMps`. Người chơi được bấm Stage (bỏ tầng cạn; tính lại delta-v), deploy (solar, radiator), và `flight_throttle_full` hoặc nút "Đốt chuyển quỹ đạo" → `ExecuteTransferBurn()`. `false` → `MissionEnded` với `InsufficientDeltaV`.
- **Tua:** `_Process` đọc input qua `InputHandler.Poll()`; cộng `delta × DaysPerSecond`, mỗi ngày nguyên gọi `StepDay()`, đưa kết quả cho `Hud.ShowResources`, cộng `run.ElapsedDays`.
- **Sự cố:** `Events` không rỗng → `SetPaused(true)` và chuyển cho `AnomalyResponseHandler`; tiếp tục khi xử lý xong (P1). Pod `Broken` → `MissionEnded` (`Thermal`); va mảnh vỡ → `MissionEnded` (`Collision`).
- **Xong:** `ElapsedDays ≥ DurationDays` → ghi `CruiseDaysSurvived`, phát `SegmentCompleted(run)`.
- `CruiseHUD` (P0): thanh ngày, delta-v, công suất ròng, % pin, `LowestTempRatio`, nút "Đốt", "Tạm dừng", "×1 / ×4".

## 7. Input map (C tạo ở C0)

| Action | Phím mặc định |
| --- | --- |
| `flight_pitch_left`, `flight_pitch_right` | A, D |
| `flight_throttle_up`, `flight_throttle_down` | Shift, Ctrl |
| `flight_throttle_full`, `flight_throttle_cut` | Z, X |
| `flight_stage` | Space |
| `flight_fairing` | F |
| `flight_chute` | P |
| `flight_sas_toggle` | T |
| `flight_sas_mode_1` … `flight_sas_mode_4` | 1, 2, 3, 4 (StabilityAssist, HoldPitch, Prograde, Retrograde) |
| `flight_rcs_toggle` | R |
| `flight_rcs_left`, `flight_rcs_right` | Q, E |
| `flight_deploy_toggle` | G |
| `flight_sce_to_aux` | C |
| `flight_seal_leak` | V |
| `flight_abort` | Esc (giữ 1,5 s) |
| `flight_zoom_in`, `flight_zoom_out` | Lăn chuột lên, xuống |
| `build_place`, `build_remove` | Chuột trái, chuột phải |
| `build_toggle_decoupler` | Tab |
| `build_cancel` | Esc |

## 8. Smoke test ở mỗi mốc

Chạy từ `main` vừa merge, ghi kết quả vào kênh của nhóm:

| Mốc | Kiểm tra |
| --- | --- |
| M0 | `dotnet build` sạch; mở project không lỗi đỏ; ba autoload có `Instance` |
| M1 | Sandbox: rocket cất cánh với lực của B; ga đổi lực đẩy; Space tách booster và tầng lõi của Mars stack; debris rơi và biến mất |
| M2 | Từ `main.tscn`: build → phóng → (quỹ đạo hoặc hỏng) → debrief đúng RP → mua node → part mới trong palette; tắt mở lại vẫn giữ RP |
| M3 | Mars stack: chặng 1 → đốt chuyển quỹ đạo → 210 ngày → vào khí quyển sao Hỏa → quay đáy, mở dù, bỏ heat shield, đốt hãm → Landed → Victory |
| M4 | Như M3 trên bản build xuất ra; không lỗi đỏ trong log; thất bại ở mỗi chặng đều về debrief đúng |

## 9. `FlightSettings` (`data/settings/flight_settings.tres`)

`ThrottleRatePerSec` 0.5; `Kp` 2.0, `Ki` 0.1, `Kd` 0.8, `ErrorScaleDeg` 10, `RateScaleDegPerSec` 10; `MassRecalcHz` 10; `StarTrackerMinAltitudeM` 30 000; `CruiseDaysPerSecond` 3.5; khoảng cách camera min/max 30 / 400 m; màu trời theo `BodyId`. Mọi node đọc settings qua một `[Export] FlightSettings Settings`.

## 10. Không làm

- Không viết công thức lực, nhiệt, điện, delta-v (B làm). Bạn chỉ áp kết quả.
- Không sửa lớp part hay thứ tự stage (A làm). Cần gì thì báo A.
- Không viết logic RP hay tech tree (B làm).
