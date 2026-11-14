# Hướng dẫn cho Dev B: Physics engine và Research

Bạn là AI coding agent của **Dev B**. Đọc `AGENTS.md` ở gốc repo trước file này; mọi quy ước ở đó (đặt tên, `[Export]`, signal, thư mục, git, dấu và hệ toạ độ) đều áp dụng.

## 1. Phạm vi

Bạn sở hữu và chỉ sửa:

- `physics/` (mọi solver, `PhysicsCoordinator`, `CruiseSimulator`, `HazardMonitor`, `FailureDetector`, `StructuralBreakHandler`, `StructuralStressEvaluator`, `DebrisBody`)
- `research/` (`TechTreeManager`, `ResearchCampaignEngine`, `MilestoneEvaluator`, `FailureAnalysisRegistry`, `SaveGame`, `ui/`)
- `data/bodies/`, `data/tech/`, `data/missions/`, `data/settings/physics_settings.tres`
- `scenes/research_screen.tscn`, `research/research_campaign_engine.tscn` (scene của autoload)
- `tests/b/`

Diagram cần đọc: `docs/design/diagrams/03_physics_engine.mmd`, `04_research_tech_tree.mmd`, và để biết part có gì: `01_shared_data.mmd`, `02b_building_runtime_parts.mmd`, `02c_building_pod_modules.mmd`.

**Physics là thư viện không trạng thái** (trừ các node chủ): `VehicleRoot` của C gọi `ExecutePhysicsStep` mỗi tick và tự áp lực lên rigid body. Bạn không viết rigid body của rocket, không đọc input.

## 2. Phụ thuộc

| Bạn cần | Từ ai | Khi nào | Trong lúc chờ |
| --- | --- | --- | --- |
| Code Core | C | H0:45 | Đọc tài liệu |
| Lớp part và module có field thật | A | **H3** | Viết công thức trên stub của A |
| `TestRocketFactory.CreateMinimal()` | A | **H3** | Không có thì harness dựng tay 3 part |
| `TestRocketFactory.CreateMarsStack()` | A | H6 | Dùng `CreateMinimal()` |
| `PartCatalog.Instance.GetDefinition(id)` (chỉ cho UI research lấy tên, icon) | A | H8 | Hiện `ModelId` |
| `VehicleRoot` gọi `ExecutePhysicsStep` | C | H5 | Harness số của riêng bạn (việc B2) |

| Người khác cần | Thứ gì | Hạn chót |
| --- | --- | --- |
| A, C | Stub của mọi class trong 03, 04 với đúng chữ ký; `ResearchCampaignEngine` autoload trả part starter và weight 20 | **M0 (H1.5)** |
| C | `ExecutePhysicsStep` thật: trọng lực, lực đẩy, khí động, mô-men của module | **H5.5** |
| C | `FailureDetector` + `StructuralBreakHandler.SpawnDebrisRigidBody` | H8 |
| A, C | Tech tree thật, nhiệm vụ `.tres`, research screen, `ProcessMissionEnd` | **H10.5** |
| C | `CruiseSimulator` | **H13** |

## 3. Danh sách việc theo thứ tự

### B0. Skeleton (H0:45–H1.5, P0)

- Mọi class trong 03 và 04 với đúng chữ ký (gồm các bổ sung ở `AGENTS.md` mục 8 và mục 4, 5 file này). Thân hàm trả giá trị an toàn (lực 0, `AtmosphereSample.Vacuum`).
- Viết **đầy đủ ngay** các Resource: `CelestialBodyDefinition`, `PhysicsSettings`, `TechNode`, `MissionDefinition`, `MissionSegmentDefinition`.
- `ResearchCampaignEngine` autoload chạy được dạng stub: `TechTree.GetUnlockedModels()` trả danh sách starter ở mục 6.1, `GetMaxBuildWeight()` trả 20, `CurrentMission` trả một `MissionDefinition` có `PayloadMassKg = 300`.
- **Xong khi:** `dotnet build` sạch, merge vào `main` trước H1.5.

### B1. Khí quyển, thiên thể, trọng lực (H1.5–H3, P0)

- `IAtmosphereModel`, `USSA76AtmosphereModel` (bảng ở mục 4.1), `ExponentialAtmosphereModel`, `OrbitalGravitySolver` (`Configure(body)`, `CalculateGravityForce`, `CalculateOrbitalVelocity`, `GetAltitude`).
- `data/bodies/earth_scaled.tres`, `mars_scaled.tres` (mục 4.2).
- **Xong khi:** sandbox `tests/b/atmosphere_test.tscn` in bảng mật độ, áp suất, nhiệt độ ở 0, 11, 20, 50, 80, 90 km cho Trái Đất và 0, 10, 50, 100 km cho sao Hỏa; trọng lực bề mặt in ra 9,81 và 3,71.

### B2. Lực đẩy, khí động, coordinator (H3–H5.5, P0, hạn chót cứng H5.5)

- `PropulsionSolver.Solve`, `AerodynamicSolver.Solve` (gồm fin, dù, che chắn), mô-men của reaction wheel và RCS, `PhysicsCoordinator.ExecutePhysicsStep` theo đúng thứ tự ở mục 4.3.
- Harness số `tests/b/physics_harness.tscn`: tự tích phân 2D (Euler bán ẩn, 60 Hz) rocket `CreateMinimal()`, ga tối đa, chương trình pitch đơn giản; in mỗi giây độ cao, tốc độ, khối lượng, q.
- **Xong khi:** harness cho rocket cất cánh, tắt máy khi hết nhiên liệu, rơi về và độ cao âm; nghiêng thân thì fin tạo mô-men đưa mũi về hướng vận tốc. Merge trước H5.5 và báo C.

### B3. Kết cấu, hỏng hóc, debris, nhiệt khi bay (H5.5–H8, P0)

- `StructuralStressEvaluator`, `StressReport`, `FailureDetector`, `StructuralBreakHandler`, `DebrisBody`, `ThermalSolver.CalculateHeatFlux` và `Step` (mục 4.4, 4.5).
- **Xong khi:** trong harness, joint của booster gãy khi bạn tạm hạ `AttachmentStrength` xuống 1 000 N; heat shield mòn ablator khi rơi đáy trước vào khí quyển sao Hỏa; tạm hạ `MaxAblatorKg` xuống 5 kg thì hết ablator, heat shield `Broken` và `BreakTriggered` phát với `Thermal`.

### B4. Research (H8–H10.5, P0, hạn chót H10.5)

- `TechTreeManager`, `ResearchCampaignEngine`, `MilestoneEvaluator`, `FailureAnalysisRegistry`, `RewardBreakdown`, `SaveGame` (JSON ở `user://save.json`).
- 14 node `data/tech/*.tres`, `data/missions/mars_mission.tres` và 3 segment (mục 6).
- `scenes/research_screen.tscn` (mục 6.4).
- **Xong khi:** mua node trừ RP và mở model; `ProcessMissionEnd` với một `FlightResult` giả trả `RewardBreakdown` đúng công thức; tắt game mở lại vẫn giữ RP và node đã mua.

### B5. Chặng hành trình (H10.5–H13, P0, hạn chót H13)

- `CruiseSimulator` (`Begin`, `GetRemainingDeltaV`, `ExecuteTransferBurn`, `StepDay`), `PowerSolver.SolveSteadyState`, `ThermalSolver.SolveSteadyState`, `CruiseStepResult`, `CruiseConditions` (mục 5).
- **Xong khi:** với Mars stack mẫu sau khi lên quỹ đạo, delta-v còn lại in ra lớn hơn 800 m/s; đi hết 210 ngày không có module nhiệt thì module rơi vào `Frozen`, có `insulation_i1` + `rhu_h1` thì không; không có solar hay RTG thì brownout.

### B6. Sau M3 (H14–H16, P1)

- `HazardMonitor` cho `CryoLeak`, `DebrisWarning`, `MaxQExceeded`, `GLimitExceeded`, `EngineFlameout`, `ComponentOverheat`, `PowerBrownout` (mục 4.6). `LightningStrike`, `SeuGlitch`, `WindShear` là P2.
- `PowerSolver.Step` mỗi tick khi bay (P1).
- Kiểm tra uốn (`Bending`) và gãy do G của pod (P1).
- Cân bằng số cùng A ở H15–H16. Từ H16 chỉ sửa lỗi.

## 4. Chi tiết: physics khi bay

### 4.1 Khí quyển

`USSA76AtmosphereModel`, độ cao tính bằng mét, trên 86 000 m trả `Vacuum`:

| Đáy lớp (m) | T đáy (K) | Gradient (K/m) | P đáy (Pa) |
| --- | --- | --- | --- |
| 0 | 288.15 | −0.0065 | 101325 |
| 11 000 | 216.65 | 0 | 22632.1 |
| 20 000 | 216.65 | +0.001 | 5474.89 |
| 32 000 | 228.65 | +0.0028 | 868.019 |
| 47 000 | 270.65 | 0 | 110.906 |
| 51 000 | 270.65 | −0.0028 | 66.9389 |
| 71 000 | 214.65 | −0.002 | 3.95642 |

Gradient L ≠ 0: `T = Tb + L(h − hb)`, `P = Pb (Tb / T)^(0.0341632 / L)`. L = 0: `P = Pb exp(−0.0341632 (h − hb) / Tb)`. `ρ = P / (287.053 T)`, `a = sqrt(1.4 × 287.053 × T)`. Hằng số đặt `const` có tên.

`ExponentialAtmosphereModel`: `ρ = ρ0 exp(−h/H)`, `P = P0 exp(−h/H)`, `T = SurfaceTemperatureK`, `a = SpeedOfSoundMps`; trên `AtmosphereTopM` trả `Vacuum`. `PhysicsCoordinator.Initialize` chọn model theo `UsesLayerTable`.

### 4.2 Thiên thể (`CelestialBodyDefinition`)

Field (đều `[Export]`): `BodyId`, `DisplayName`, `Mass`, `Radius`, `UsesLayerTable`, `SeaLevelDensity`, `SeaLevelPressure`, `ScaleHeight`, `SurfaceTemperatureK`, `SpeedOfSoundMps`, `SuttonGravesConstant`, `AtmosphereTopM`, `SolarFluxWm2`, `PackedScene EnvironmentScene`.

| Field | `earth_scaled` | `mars_scaled` |
| --- | --- | --- |
| `Radius` (m) | 600 000 | 320 000 |
| `Mass` (kg) | 5.2916e22 (g = 9,81) | 5.692e21 (g = 3,71) |
| `UsesLayerTable` | true | false |
| `SeaLevelDensity` (kg/m³) | 1.225 | 0.020 |
| `SeaLevelPressure` (Pa) | 101325 | 610 |
| `ScaleHeight` (m) | 8 500 | 11 100 |
| `SurfaceTemperatureK` | 288 | 210 |
| `SpeedOfSoundMps` | 340 | 230 |
| `SuttonGravesConstant` | 1.74e-4 | 1.9e-4 |
| `AtmosphereTopM` | 86 000 | 120 000 |
| `SolarFluxWm2` | 1361 | 590 |

Tâm hành tinh luôn tại `(0, -Radius)`. `GravitationalConstant` = 6.674e-11; tính `μ = G × Mass` bằng **double** rồi mới đổi về float. Lực trọng trường: `F = −μ m / |r|² × r̂`, r tính từ tâm hành tinh tới CoM.

### 4.3 `PhysicsCoordinator`

`PhysicsCoordinator : Node`, có `[Export] PhysicsSettings Settings`. Nó tự tạo các solver (class C# thường) trong `_Ready`, và công khai 4 property cho C dùng: `Failures` (`FailureDetector`), `BreakHandler` (`StructuralBreakHandler`), `StressEvaluator` (`StructuralStressEvaluator`), `Hazards` (`HazardMonitor`).

`Initialize(MissionSegmentDefinition segment)`: đọc `segment.Body`, chọn khí quyển, `Configure` trọng lực, bật hazard theo `segment.EnabledHazards`.

`PhysicsStepInput` (struct): `Position` (CoM, thế giới), `LinearVelocity`, `BodyRotationRad`, `PitchAngleDeg`, `AngularVelocityDegPerSec`, `MassProperties Mass`, `PartTree Parts`, `DeltaTime`.

`PhysicsStepOutput` (struct): `NetForce`, `NetTorqueZ`, `ConsumedMass`, `GravityForce` (thêm, cho stress), `AltitudeMeters`, `CurrentGForce`, `DynamicPressure`, `MachNumber`, `HeatFlux`, `AtmosphereSample Air` (thêm, cho HUD), `List<PartLoad> PartLoads`, `List<FlightEvent> Events`, `ThermalStepResult Thermal` (thêm). Dùng lại list bằng `Clear()`, không cấp phát mới mỗi tick.

Thứ tự trong `ExecutePhysicsStep`:

1. `alt`, `air = atmosphere.Sample(alt)`. Với mỗi part tính `r = (part.GetLocalPosition() − Mass.CenterOfMass).Rotated(BodyRotationRad)`.
2. **Che chắn:** `up = Vector2.Up.Rotated(rot)` (chú ý: trong Godot `Vector2.Up` là (0, −1); dùng `new Vector2(0, 1)`). Nếu tốc độ > 1 m/s: mũi trước khi góc giữa `up` và `v` < `ShieldingConeDeg`; đáy trước khi góc giữa `−up` và `v` < `ShieldingConeDeg`. Đặt `IsShielded` cho `ShieldedParts` của fairing (mũi trước) và heat shield (đáy trước) còn trong cây; còn lại `false`.
3. **Lực đẩy** (`PropulsionSolver.Solve`), mục 4.3.1.
4. **Khí động** (`AerodynamicSolver.Solve`), mục 4.3.2.
5. **Trọng lực** tại CoM.
6. **Module:** reaction wheel và RCS, mục 4.3.3.
7. **Nhiệt** (`ThermalSolver.Step`), mục 4.5.
8. **Hazard** (`HazardMonitor.Evaluate`, P1).
9. Gộp `NetForce`, `NetTorqueZ` (dùng `FlightMath.Cross(r, F)`), `PartLoads` (lực đẩy + khí động trên từng part), `CurrentGForce = |NetForce − GravityForce| / (m × G0)`, `DynamicPressure = ½ρv²`, `MachNumber = v / a`.

Physics **đọc** lệnh (`Throttle`, `GimbalAngleDeg`, `DeflectionAngleDeg`, `CommandedTorque`, `TranslateCommand`, `ChuteState`, `IsArmed`, `IsIgnited`) và **ghi** trạng thái vật lý (`CurrentFuel`, `RemainingPropellant`, `BurnProgress`, `TempK` qua `ApplyThermalLoad`, `AblatorRemainingKg` qua `Ablate`, `StoredMomentumNms`, `IsSaturated`, `MonopropRemainingKg`, `IsShielded`, `State` của dù). Ngoại lệ duy nhất về lệnh: hết nhiên liệu thì physics gọi `Shutdown()`.

#### 4.3.1 Lực đẩy

- Động cơ đang `IsIgnited`, không `Broken`. Hệ số ga lỏng: `f = Throttle > 0 ? Lerp(MinThrottle, 1, Throttle) : 0`.
- `ratio = clamp(air.Pressure / 101325, 0, 1)`; `F = Lerp(MaxThrustVac, GetMaxThrustSeaLevel(), ratio) × f`; `ṁ = GetMassFlowRate() × f`.
- Lỏng: rút `ṁ × dt` chia đều cho các `FeedTanks` còn nhiên liệu; thiếu thì `Shutdown()` và thêm event `EngineFlameout`.
- Rắn: `BurnProgress += ṁ × dt / PropellantMass`; nhân `F` với `BurnCurve.Sample(BurnProgress)`; trừ `RemainingPropellant`; hết thì `IsIgnited = false` và `EngineFlameout`.
- Hướng: `new Vector2(0, 1).Rotated(−Mathf.DegToRad(GimbalAngleDeg)).Rotated(rot)`, đặt tại vị trí động cơ. Cộng `ṁ × dt` vào `ConsumedMass`.

#### 4.3.2 Khí động

- `q = ½ρ|v|²`; luồng khí tương đối = `−v̂`. Bỏ qua part có `IsShielded`.
- Lực cản mỗi part: `α` = góc giữa `up` và `v`; `A = FrontalArea × DragCoefficient × |cos α| + SideArea × SideDragCoefficient × |sin α|`; nhân `MachDragCurve.Sample(Mach)`; `F = −q × A × v̂`, đặt tại part.
- Fin (`AeroSurfacePart`): `perp` = thành phần của `−v̂` vuông góc với `up`; `F = q × LiftCoefficient × PlanformArea × (perp + sin(δ) × right)`, với `right = new Vector2(1, 0).Rotated(rot)` và δ = `DeflectionAngleDeg` (0 với fin tĩnh); kẹp độ lớn ở `q × Cl × S × sin(FinStallDeg)`.
- Dù `Deployed`, không `Broken`: `F = −q × DeployedDragArea × v̂` tại vị trí dù. Ở tick đầu tiên dù chuyển sang `Deployed`, nếu `q > MaxDeployDynamicPressure` hoặc `Mach > MaxDeployMach` thì đặt dù `Broken` (không có lực). Giữ `HashSet` các dù đã kiểm tra.
- `AeroResult`: `Force`, `TorqueZ`, `CenterOfPressure`, `DynamicPressure`, `MachNumber`. `WindTunnelAnalyzer` của A gọi cùng hàm này.

#### 4.3.3 Module điều khiển tư thế

- `CommandedTorque` của module là lệnh chuẩn hoá −1..1 (C ghi).
- `ReactionWheelModule` (`CanOperate()`): `τ = CommandedTorque × ControlAuthority`; `StoredMomentumNms += τ × dt`; khi `|StoredMomentumNms| ≥ MaxMomentumStorageNms` thì `IsSaturated = true` và bỏ phần mô-men cùng chiều; `DumpMomentum()` (C gọi) đưa về 0 nếu RCS đang bù.
- `RcsThrusterModule` (`IsEnabled`, còn monoprop, `CanOperate()`): `τ = CommandedTorque × ControlAuthority`; lực tịnh tiến `TranslateCommand × ControlAuthority` theo `right`; monoprop tiêu thụ `(|τ| / RcsLeverArmM + |F|) / (RcsIsp × G0) × dt`.

### 4.4 Kết cấu và hỏng hóc

`StructuralStressEvaluator` (P0 đơn giản). C gọi `EvaluateRecursiveLoad(PhysicsStepInput input, PhysicsStepOutput output)` rồi `CheckYieldLimits()` (trả `StressReport`) mỗi tick:

1. `a = (NetForce − GravityForce) / TotalMass`.
2. Với mỗi joint (con `c`, cây con `S = Parts.GetSubtree(c)`): `F_ext` = tổng `PartLoads` trong S; `Ft = m_S × a − F_ext` (lực part cha truyền cho cây con). Xoay về hệ thân: `lateral = Ft.Rotated(−rot).X`.
3. Joint có decoupler hoặc tự tách và `|Ft| > Strength` → vi phạm `JointOverload` tại `c`. `|lateral| > c.MaxShear` → `Shear` tại `c`. (P1: `|lateral| × khoảng cách joint–CoM của S > MaxBending` → `Bending`.)
4. `StressReport`: `MaxStressRatio`, `List<StressViolation>` với `Part`, `Cause`, `Ratio`.

`FailureDetector` (class thường):

- `MonitorStressThresholds(report)` → vi phạm đầu tiên gọi `TriggerBreak`. `MonitorThermalLimits(thermal)` → part `RocketPart` nào `Broken` thì `TriggerBreak(part, Thermal)` (module `Broken` không gây Break). `MonitorImpact(part, contactSpeed)` → `contactSpeed > part.ImpactToleranceMps` thì `TriggerBreak(part, Collision)`; trả bool.
- `TriggerBreak(part, cause)`: phát C# event `public event Action<RocketPart, FailureCause> BreakTriggered` **trước**, rồi gọi `BreakHandler.HandleBreak(part, cause)` = `part.Tree.SeverSubtree(part, SeverCause.Break)`. Mỗi tick tối đa 1 Break.

`StructuralBreakHandler.SpawnDebrisRigidBody(IReadOnlyList<RocketPart> parts, Vector3 linearVelocity, Vector3 angularVelocity, Vector3 impulse, Node3D worldParent, CelestialBodyDefinition body)`: C gọi cho **mọi** lần cắt (Jettison và Break). Tạo `DebrisBody` (`RigidBody3D`, `GravityScale = 0`, collision layer/mask 0), reparent các part sang nó giữ nguyên `GlobalTransform`, đặt khối lượng, vận tốc, áp `impulse`, gọi `PartVisual.OnSevered` nếu part có visual con. `DebrisBody` tự áp trọng lực hướng tâm hành tinh trong `_IntegrateForces` và tự `QueueFree()` khi độ cao < 0 hoặc cách camera hiện tại quá `DebrisDespawnDistanceM`.

### 4.5 Nhiệt khi bay

- `CalculateHeatFlux(ρ, v) = SuttonGravesConstant × sqrt(ρ / NoseRadiusM) × v³` (W/m²).
- **Nhận nhiệt:** đáy trước và heat shield còn trong cây → `leftover = heatShield.Ablate(q_h × heatShield.FrontalArea × HeatPartition × dt)`, rồi `heatShield.ApplyThermalLoad(leftover)`. Ngược lại: part không bị che có hình chiếu `r · v̂` lớn nhất nhận `q_h × FrontalArea × HeatPartition × dt`.
- **Làm nguội** mọi part mỗi tick: `ApplyThermalLoad(−ε σ SideArea (T⁴ − T_air⁴) × dt)` với `ε = PartEmissivity`, `T_air = air.Temperature` (chân không: 3 K). Động cơ đang cháy nhận thêm `EngineHeatW × f × dt` (P1).
- `ThermalStateRules.Evaluate` (của A) tự chạy trong `ApplyThermalLoad`. `ThermalStepResult`: `MaxTempRatio` (lớn nhất `TempK / MaxSafeTempK`), `MassAblatedKg`, `OverheatedComponents`, `BrokenComponents`.

### 4.6 Hazard (P1)

`HazardMonitor.Evaluate` chỉ phát loại có trong `EnabledHazards` của segment, mỗi loại tối đa một lần cho tới khi C báo đã xử lý (`HazardMonitor.Acknowledge(FlightEventType)`):

| Loại | Khi nào | Tác động do physics tự làm |
| --- | --- | --- |
| `MaxQExceeded` | `q > MaxQLimit` | Không (cảnh báo) |
| `GLimitExceeded` | G > `MaxGForce` của pod | Không (cảnh báo) |
| `EngineFlameout` | Động cơ tắt vì hết nhiên liệu | Đã tắt ở 4.3.1 |
| `CryoLeak` | Mỗi giây xác suất `CryoLeakChancePerSec` khi đang có tank Hydrolox/Methalox | Đặt `LeakRateKgPerSec` của tank; rút nhiên liệu mỗi tick |
| `DebrisWarning` | Xác suất mỗi giây khi độ cao > 80 km | Sau `DebrisWarningSeconds` không né (vận tốc ngang tương đối chưa đổi > 5 m/s) thì `TriggerBreak(pod, Collision)` |
| `ComponentOverheat` | Một component vừa thành `Overheated` | Không |
| `PowerBrownout` | Pin về 0 và cung < cầu | Module avionics `IsPowered = false` |

`RandomNumberGenerator` có `[Export] ulong HazardSeed` để tái hiện lỗi.

## 5. Chi tiết: chặng hành trình

`CruiseSimulator` (class thường, C tạo và giữ):

- `Begin(PartTree parts, MissionSegmentDefinition segment, CelestialBodyDefinition origin, CelestialBodyDefinition destination)`.
- `GetRemainingDeltaV()`: động cơ = các `LiquidEnginePart` đang `IsArmed`, không `Broken`; `Isp` = trung bình `IspVacuum` có trọng số theo `MaxThrustVac`; nhiên liệu = tổng `CurrentFuel` của hợp các `FeedTanks`; `m0` = tổng khối lượng cây; `Δv = Isp × G0 × ln(m0 / (m0 − fuel))`. Không có động cơ thì 0.
- `ExecuteTransferBurn()`: `false` nếu `Δv < RequiredDeltaVMps` hoặc không động cơ nào còn `RemainingIgnitions > 0`. Ngược lại rút `m0 × (1 − exp(−RequiredDeltaVMps / (Isp × G0)))` kg từ feed tank, trừ 1 lần đánh lửa của mỗi động cơ dùng, trả `true`.
- `StepDay()`:
  1. `day++`; `flux = Lerp(origin.SolarFluxWm2, destination.SolarFluxWm2, day / DurationDays)`.
  2. `PowerSolver.SolveSteadyState(components, flux)`: cung = solar `IsDeployed` và `CanOperate()` → `NominalOutputW × flux / 1361`; RTG → `NominalOutputW × (1 − DegradationRatePerYear × MissionElapsedYears)`. Cầu = `PowerDrawW` của module đang chạy + `PowerDrawW` của pod × `CruiseDutyFactor`. Chênh lệch × 24 Wh nạp hoặc rút từ pin (kẹp 0..dung lượng). Pin về 0 mà cung < cầu → brownout: module avionics `IsPowered = false`.
  3. `ThermalSolver.SolveSteadyState(components, flux)`: `Q_in = flux × Absorptivity × PodCrossAreaM2 + Σ WasteHeatW + Σ HeaterOutputW (RHU) + công suất điện đang tiêu thụ`; `ε = BaseEmissivity × (1 − ReflectiveEfficiency của lớp cách nhiệt đang chạy)`; radiator đang mở cộng `MaxHeatRejectionW` vào mức tản; `T = (Q_in / (ε σ PodRadiatingAreaM2))^(1/4)`. Đặt `TempK` của pod và mọi module bằng `T` rồi gọi `ThermalStateRules.Evaluate`.
  4. IMU: `AccumulatedDriftDeg += DriftRateDegPerMin × CruiseDriftMinutesPerDay`; có star tracker đang chạy thì đặt về 0. RTG: `MissionElapsedYears += 1 / 365`.
  5. Hazard theo ngày (P1): `DebrisWarning`, `SeuGlitch` với xác suất mỗi ngày trong settings.
  6. Trả `CruiseStepResult`: `Day`, `DeltaVRemaining`, `PowerBalanceW`, `BatteryFraction`, `LowestTempRatio`, `IsBrownout`, `Events`, `BrokenComponents`.

Mục tiêu cân bằng: không có `insulation_i1` hoặc `rhu_h1` thì module đóng băng ở nửa sau hành trình; không có `solar_s1` hoặc `rtg_t1` thì brownout trong vài ngày đầu.

**Lưu ý cân bằng cho B6.** Mô phỏng thô với số hiện tại cho thấy lúc vào khí quyển sao Hỏa ở 1 000 m/s, tổng nhiệt vào heat shield chỉ khoảng 5 MJ (ablator chứa 12 MJ), và part ở mũi gần như không nóng lên. Vậy heat shield hiện quan trọng vì **lực cản** (không có nó thì dù không mở được và lander rơi ở hàng trăm m/s), chưa phải vì nhiệt. Muốn nhiệt thành mối đe doạ thật thì tăng `SuttonGravesConstant` của `mars_scaled` như một hệ số game, rồi chỉnh `MaxAblatorKg` theo.

## 6. Chi tiết: research

### 6.1 Tech tree

`research/research_campaign_engine.tscn` là scene của autoload: gốc `ResearchCampaignEngine`, con `TechTreeManager` (Node). Mọi dữ liệu là `[Export]` trên hai node này: `TechNode[] AllNodes`, `MissionDefinition Mission`, `string[] StarterModelIds`, `int BaseBuildWeight = 20`.

Starter: `pod_mk1`, `tank_s1`, `engine_lv1`, `booster_s1`, `decoupler_d1`, `fairing_n1`, `fin_f1`, `structural_s1`, `imu_g1`, `battery_b1`.

| NodeId | Tier | RP | Cần trước | Mở | Weight + |
| --- | --- | --- | --- | --- | --- |
| `t1_heavy` | 1 | 30 | – | `tank_m1`, `booster_m1` | |
| `t1_avionics` | 1 | 30 | – | `imu_g2`, `wheel_rw1` | |
| `t1_frame` | 1 | 25 | – | | 5 |
| `t2_main_engine` | 2 | 60 | `t1_heavy` | `engine_lv2` | |
| `t2_upper_stage` | 2 | 80 | `t2_main_engine` | `engine_vac1`, `tank_h1` | |
| `t2_pod` | 2 | 60 | `t1_avionics` | `pod_mk2` | |
| `t2_power` | 2 | 50 | `t1_avionics` | `solar_s1`, `battery_b2` | |
| `t2_frame` | 2 | 50 | `t1_frame` | | 8 |
| `t3_thermal` | 3 | 70 | `t2_power`, `t2_pod` | `insulation_i1`, `radiator_a1`, `rhu_h1` | |
| `t3_edl` | 3 | 90 | `t2_upper_stage` | `heatshield_h1`, `chute_p1` | |
| `t3_lander` | 3 | 100 | `t3_edl` | `engine_land1`, `tank_lm1` | |
| `t3_navigation` | 3 | 80 | `t1_avionics` | `startracker_st1`, `rcs_r1` | |
| `t3_frame` | 3 | 90 | `t2_frame` | | 10 |
| `t4_rtg` | 4 | 120 | `t3_thermal` | `rtg_t1` | |

API (bổ sung so với diagram: `GetUnlockedModels()` không còn tham số `category`; `PartCatalog` tự lọc):

- `IReadOnlyCollection<string> GetUnlockedModels()` = starter ∪ `UnlocksModelIds` của node đã mua.
- `IsUnlocked(modelId)`, `GetMaxBuildWeight()` = `BaseBuildWeight` + tổng `BuildWeightBonus` đã mua.
- `GetNode(nodeId)`, `CanPurchase(nodeId)` (chưa mua, đủ node trước, đủ RP), `PurchaseNode(nodeId)` (gọi `ResearchCampaignEngine.SpendRP`, thêm vào `UnlockedNodes`, phát `NodeUnlocked(nodeId)`, lưu game).

### 6.2 Nhiệm vụ

`data/missions/mars_mission.tres`: `MissionId = mars_mission`, `PayloadMassKg = 300`, `PayloadScene` (để trống), `Segments` = 3 file:

| File | `Kind` | `Body` | Thông số | `RewardRP` |
| --- | --- | --- | --- | --- |
| `seg_earth_ascent.tres` | EarthAscent | `earth_scaled` | `TargetAltitudeM` 90 000, `TargetSpeedMps` 2 200 (tốc độ ngang) | 60 |
| `seg_cruise.tres` | Cruise | `earth_scaled` (origin) | `RequiredDeltaVMps` 800, `DurationDays` 210 | 80 |
| `seg_mars_edl.tres` | MarsEdl | `mars_scaled` | `EntryAltitudeM` 100 000, `EntrySpeedMps` 1 000, `EntryAngleDeg` −12, `MaxLandingTiltDeg` 15 | 0 |

`EnabledHazards` để `None` cho P0; bật ở B6.

### 6.3 RP và lưu game

- `ProcessMissionEnd(FlightResult r)` → `RewardBreakdown` (class thường): `SegmentRP` = tổng `RewardRP` của `r.SegmentsCompleted` segment đầu; `MilestoneRP` = `min(40, floor((r.MaxAltitude − BestAltitude) / 2000))` + `min(20, floor((r.MaxSpeed − BestSpeed) / 100))` (âm thì 0), rồi cập nhật kỷ lục; `NoveltyRP` = 15 nếu `Outcome == Failed` và khoá `"{FailureCause}:{PartGroup}"` chưa có trong `DiscoveredFailures`. Cộng vào `CurrentRP`, phát `RPChanged`. `Outcome == Success` → `IsCampaignComplete = true`. Gọi `SaveGame.Save()`.
- Mọi hằng số trong công thức (40, 2000, 20, 100, 15) là `[Export]` trên `ResearchCampaignEngine`.
- `SaveGame`: `CurrentRP`, `UnlockedNodes`, `DiscoveredFailures`, `CompletedSegments`, `IsCampaignComplete`, `BestAltitude`, `BestSpeed`; JSON qua `FileAccess` ở `user://save.json`; `Load()` trong `_Ready` của autoload; thêm `ResetSave()` cho debug.

### 6.4 Research screen (P0)

- `ResearchScreenRoot : Control` (gốc `research_screen.tscn`), signal `BuildRequested()`. GameFlowManager (C) nối signal này; screen không gọi gì của Control.
- Nhãn RP; các node xếp theo cột `Tier`; mỗi thẻ có tên, giá, danh sách model sẽ mở (tên và icon lấy từ `PartCatalog.Instance.GetDefinition(id)`, chỉ ở UI), nút "Mua" bật khi `CanPurchase`. Màu thẻ đã mua / mua được / khoá là `[Export] Color`.
- Nút "Vào xưởng" phát `BuildRequested`.

## 7. `PhysicsSettings` (`data/settings/physics_settings.tres`)

| Field | Mặc định | Dùng ở |
| --- | --- | --- |
| `SideDragCoefficient` | 1.0 | 4.3.2 |
| `Curve MachDragCurve` | 1.0 ở Mach 0, 1.8 ở Mach 1.1, 1.2 ở Mach 3 | 4.3.2 |
| `FinStallDeg` | 25 | 4.3.2 |
| `ShieldingConeDeg` | 30 | 4.3 bước 2 |
| `MaxQLimit` | 40 000 Pa | 4.6 |
| `NoseRadiusM` | 1.0 | 4.5 |
| `HeatPartition` | 0.5 | 4.5 |
| `PartEmissivity` | 0.8 | 4.5 |
| `EngineHeatW` | 20 000 | 4.5 (P1) |
| `RcsIsp`, `RcsLeverArmM` | 220, 1.0 | 4.3.3 |
| `DebrisDespawnDistanceM` | 3 000 | 4.4 |
| `CruiseDutyFactor` | 0.3 | 5 |
| `Absorptivity`, `BaseEmissivity` | 0.3, 0.8 | 5 |
| `PodCrossAreaM2`, `PodRadiatingAreaM2` | 3.1, 12 | 5 |
| `CruiseDriftMinutesPerDay` | 1.0 | 5 |
| `CryoLeakChancePerSec`, `DebrisChancePerSec`, `DebrisWarningSeconds` | 0.002, 0.001, 8 | 4.6 |
| `DebrisChancePerDay`, `SeuChancePerDay` | 0.005, 0.003 | 5 |

## 8. Không làm

- Không viết `RigidBody3D` của rocket, không áp lực vào Jolt, không đọc input (C làm).
- Không đổi field, không thêm hàm vào lớp part của A; cần gì thì báo A.
- Không quyết định khi nào chặng hay nhiệm vụ kết thúc (`FlightDirector`, `CruiseController` của C làm). Bạn chỉ trả số liệu và event.
- Không viết debrief hay màn Victory (C làm). Bạn trả `RewardBreakdown`.
