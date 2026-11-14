# AGENTS.md: quy tắc chung cho mọi AI coding agent của dự án

File này là nguồn sự thật chung cho cả 3 người. Đọc hết file này trước, rồi đọc file hướng dẫn riêng trong `docs/agents/` của người bạn đang làm cho:

| Người | Phụ trách | File hướng dẫn |
| --- | --- | --- |
| Dev A | Rocket building (dữ liệu part, editor, lắp ráp, rocket lúc bay, scene tạm của part) | `docs/agents/A_BUILDING.md` |
| Dev B | Physics engine và tech tree / research | `docs/agents/B_PHYSICS_RESEARCH.md` |
| Dev C | Control system, game flow, tích hợp, project setup | `docs/agents/C_CONTROL.md` |

Khi file này mâu thuẫn với diagram trong `docs/design/diagrams/`, **file này và file hướng dẫn riêng thắng** (chúng chứa các quyết định kỹ thuật mới hơn, xem mục 8). Khi hai file hướng dẫn riêng mâu thuẫn nhau, dừng lại và báo người dùng; không tự chọn.

---

## 1. Game và kiến trúc trong 1 phút

Game 2.5D trên Godot 4.7 (.NET, C#). Người chơi xây rocket trên grid 2D, phóng một robot từ Trái Đất tới sao Hỏa qua 3 chặng, và game kết thúc khi đáp thành công. Mỗi lần thử thất bại vẫn cho RP để mở khoá part tốt hơn (upgrade loop).

| Chặng | Cách chơi | Kết thúc khi |
| --- | --- | --- |
| 1. EarthAscent | Lái trực tiếp, mô phỏng vật lý mỗi tick | Đạt độ cao và tốc độ ngang của quỹ đạo chờ |
| 2. Cruise | Tua nhanh theo ngày, chỉ kiểm tra tài nguyên | Hết số ngày của hành trình |
| 3. MarsEdl | Lái trực tiếp trên sao Hỏa | Command pod chạm đất đủ chậm và không lật |

Bốn system, mỗi system một việc, chỉ nói chuyện qua các kiểu dữ liệu có tên:

```
Research (B) --ModelId mở khoá, MaxBuildWeight--> Building (A)
Building (A) --AssembledRocket--> Control (C)
Control (C)  --PhysicsStepInput--> Physics (B) --PhysicsStepOutput--> Control (C)
Control (C)  --FlightResult--> Research (B)
```

Luật phụ thuộc (không được phá):

1. **Research không biết lớp part.** `TechTreeManager` chỉ trả `ModelId` (string). Chỉ `PartCatalog` đổi ID thành definition.
2. **Building không gọi Control.** Nó chỉ phát signal `DesignConfirmed`.
3. **Chỉ Control ghi lệnh điều khiển** vào part và module (`SetThrottle`, `SetGimbal`, `SetDeflection`, `CommandTorque`, `CommandTranslate`, `Arm`, `TriggerSeparation`, ...). Physics chỉ đọc lệnh đó để tính lực, và chỉ ghi trạng thái vật lý (`CurrentFuel`, `TempK`, `State`, `StoredMomentumNms`, `MonopropRemainingKg`, `AblatorRemainingKg`).
4. **Chỉ hai đường được cắt cây part:** `StagingManager` (Jettison) và `StructuralBreakHandler` (Break), cả hai qua `PartTree.SeverSubtree`.
5. **Lớp hiển thị chỉ đọc.** Không lớp nào trong 4 system tham chiếu tới `PartVisual` hay node hiển thị.

---

## 2. Cấu trúc thư mục và quyền sở hữu

Mỗi thư mục có đúng một chủ. **Chỉ sửa file trong thư mục của mình.** Cần đổi gì trong thư mục người khác thì báo người đó (hoặc ghi vào `docs/API_CHANGES.md`, mục 9).

```
res://                              (gốc repo = gốc project Godot)
├─ project.godot                    C  (input map, autoload, physics settings)
├─ RocketGame.csproj / .sln         C
├─ core/                            C  (code ở mục 7, chỉ thêm, không đổi tên)
├─ building/                        A
│  ├─ data/       definitions (Resource)
│  ├─ runtime/    RocketPart + lớp con, PodModule + lớp con, PartTree, Joint, StageGroup
│  ├─ assembly/   RocketAssembler, AssembledRocket, MassCenterCalculator, TestRocketFactory, SampleDesigns
│  ├─ editor/     BuildingSystem, BuildGrid, BuildBudget, RocketDesign, PartCatalog, WindTunnelAnalyzer
│  └─ ui/         giao diện build screen
├─ physics/                         B  (solver, PhysicsCoordinator, CruiseSimulator, Hazard, Failure, StructuralStressEvaluator)
├─ research/                        B  (TechTreeManager, ResearchCampaignEngine, mission, SaveGame, ui/)
├─ control/                         C
│  ├─ flow/       GameFlowManager, MissionRun
│  ├─ flight/     FlightScene, FlightDirector, VehicleRoot, StagingManager, BlackBoxTelemetryLogger
│  ├─ piloting/   FlightControlSubsystem và các lớp con
│  ├─ cruise/     CruiseController
│  └─ ui/         FlightHUD, CruiseHUD, Debrief, Victory
├─ presentation/
│  ├─ parts/      A  (PartVisual và 8 lớp con)
│  └─ flight/     C  (FlightCameraRig, FlightVfxDirector, FlightAudioDirector, BodyEnvironmentVisual)
├─ data/
│  ├─ parts/ modules/                A  (.tres)
│  ├─ bodies/ tech/ missions/        B  (.tres)
│  └─ settings/                      build_settings.tres (A), physics_settings.tres (B), flight_settings.tres (C)
├─ scenes/
│  ├─ main.tscn, flight.tscn, debrief.tscn, victory.tscn   C
│  ├─ build_screen.tscn                                    A
│  └─ research_screen.tscn                                 B
├─ assets/                          nhóm model (theo tab "Hợp đồng model và đồ hoạ"); assets/parts/_placeholder/ do A
├─ tests/a/  tests/b/  tests/c/     sandbox scene riêng của từng người
└─ docs/                            (có file .gdignore để Godot bỏ qua)
```

**Một scene chỉ có một chủ.** Không mở rồi lưu scene của người khác (Godot ghi lại cả file, gây conflict khó gỡ).

### Autoload (đăng ký trong `project.godot` bởi C)

| Tên autoload | Scene (gốc mang script cùng tên) | Chủ |
| --- | --- | --- |
| `PartCatalog` | `building/editor/part_catalog.tscn` | A |
| `ResearchCampaignEngine` | `research/research_campaign_engine.tscn` | B |
| `GameFlowManager` | `control/flow/game_flow_manager.tscn` | C |

Autoload đăng ký **dạng scene** (không phải script) để dữ liệu và tham chiếu của chúng là `[Export]` chỉnh trong Inspector. Mỗi autoload có `public static T Instance { get; private set; }` gán trong `_EnterTree()`. Thứ tự autoload: `PartCatalog`, `ResearchCampaignEngine`, `GameFlowManager`.

---

## 3. Quy ước đặt tên

| Thứ | Quy ước | Ví dụ |
| --- | --- | --- |
| Namespace | `RocketGame.<System>` theo thư mục cấp 1 | `RocketGame.Building`, `RocketGame.Physics`, `RocketGame.Research`, `RocketGame.Control`, `RocketGame.Presentation`, `RocketGame.Core` |
| Class, struct, enum, interface | PascalCase; interface bắt đầu bằng `I` | `RocketAssembler`, `IFlightComponent` |
| File `.cs` | Trùng tên class, một class public mỗi file (Godot bắt buộc) | `RocketAssembler.cs` |
| Method, property public, const, enum member | PascalCase | `GetCurrentMass()`, `MaxThrustVac`, `FlightPhase.OrbitReached` |
| Field private | `_camelCase` | `_partTree` |
| Biến cục bộ, tham số | camelCase | `contactSpeed` |
| Bool | Bắt đầu bằng `Is`, `Has`, `Can` | `IsArmed`, `HasValidAttitude`, `CanOperate()` |
| Signal | `[Signal] public delegate void <Tên>EventHandler(...)`; tên ở thì quá khứ hoặc dạng sự kiện, đúng như diagram | `PartSeveredEventHandler` → event `PartSevered` |
| Hàm xử lý signal | `On<TênSignal>` | `OnPartSevered` |
| Scene, resource, asset | snake_case | `flight.tscn`, `engine_lv2.tres` |
| `ModelId`, `NodeId`, `BodyId`, `MissionId` | snake_case, trùng tên file `.tres` | `engine_lv2` → `data/parts/engine_lv2.tres` |
| Node trong scene | PascalCase | `VehicleRoot`, `ExhaustPoint` |
| Input action | `flight_<việc>`, `build_<việc>`, `ui_<việc>` | `flight_stage` |
| Đơn vị | SI; thêm hậu tố khi dễ nhầm | `AngleDeg`, `MassKg`, `SpeedMps`, `PowerW`, `TempK`, `MomentumNms`, `ImpulseNs` |
| Góc | Độ ở mọi API public, radian chỉ bên trong phép tính | `SetGimbal(float angleDeg)` |
| Hằng số vật lý | `const` trong `FlightMath` | `FlightMath.G0` |

Tên class, method, field trong diagram là **hợp đồng**: dùng đúng từng ký tự. Thêm thành viên private thoải mái; thêm thành viên public thì xem mục 9.

---

## 4. Quy tắc `[Export]` và tham chiếu node (bắt buộc)

**Mọi tham chiếu tới node bên ngoài script và mọi thông số chỉnh được phải là `[Export]`**, để chỉnh trong Inspector của Godot mà không sửa code.

1. **Node khác** (node anh em, node ở nhánh khác, node trong scene con): `[Export]` property có kiểu cụ thể. Gán bằng kéo thả trong Inspector.
   ```csharp
   [Export] public VehicleRoot Vehicle { get; set; }
   [Export] public FlightHUD Hud { get; set; }
   ```
2. **Không dùng đường dẫn chuỗi tới node bên ngoài**: không `GetNode("../../Hud")`, không `GetParent().GetParent()`, không `/root/...`. Autoload thì dùng `X.Instance`.
3. **Node con trong chính scene của mình**: ưu tiên `[Export]`. Được phép `GetNode<T>("%UniqueName")` với node đánh dấu "Access as Unique Name".
4. **Thông số chỉnh được** (gain, tốc độ, ngưỡng, thời gian, màu, scale, tốc độ tua...): `[Export]` với nhóm và hint, không viết số trần trong logic.
   ```csharp
   [ExportGroup("PID")]
   [Export(PropertyHint.Range, "0,10,0.01")] public float Kp { get; set; } = 2.0f;
   [Export(PropertyHint.Range, "0,5,0.01")]  public float Kd { get; set; } = 0.8f;
   ```
5. **Thông số dùng chung hoặc nhiều** gom vào một `Resource` settings (`.tres` trong `data/settings/`) rồi `[Export]` resource đó: `BuildSettings` (A), `PhysicsSettings` (B), `FlightSettings` (C).
6. **Dữ liệu nội dung** (part, module, tech node, nhiệm vụ, thiên thể) là `[GlobalClass] partial class ... : Resource` với mọi field `[Export]`, lưu thành `.tres`. **Không bao giờ viết stats của part trong code.**
7. **Node tạo lúc chạy** (part do `RocketAssembler` tạo, debris) không kéo thả được: chúng nhận phụ thuộc qua hàm `Bind(...)` / `Initialize(...)`. Đây là ngoại lệ duy nhất của quy tắc 1.
8. **Kiểm tra trong `_Ready()`**: mọi export bắt buộc phải khác null; thiếu thì báo lỗi rõ và tắt node đó.
   ```csharp
   public override void _Ready()
   {
	   if (!NodeGuard.Require(this, Vehicle, nameof(Vehicle))) return;
   }
   ```
9. Script cần hiện trong menu "Add Node" / "New Resource" thì gắn `[GlobalClass]`.

Kiểu export được: số, bool, string, enum, `[Flags]` enum, `Vector2/3`, `Color`, `Curve`, `PackedScene`, `Texture2D`, `Resource` con, node, và mảng/`Godot.Collections.Array<T>` của chúng. **Không export `Dictionary` hay mảng enum**: dùng `[Flags]` mask (mục 7) hoặc tách thành nhiều field.

---

## 5. Quy tắc code Godot C#

- Godot 4.7.2 .NET, C# của .NET 8. Mọi lớp kế thừa kiểu Godot phải là `partial`.
- **Signal:** dùng `[Signal]` cho mọi signal trong diagram. Tham số signal phải là kiểu Variant: số, bool, string, enum, `GodotObject`. Vì vậy các kiểu dữ liệu đi qua signal là class kế thừa `RefCounted`: `AssembledRocket`, `MissionRun`, `FlightResult`, `ActiveAnomaly`. Struct thuần C# chỉ đi qua lời gọi hàm, không qua signal.
- **Nối signal** bằng C# event (`part.PartSevered += OnPartSevered;`), tháo ra trong `_ExitTree()`. Phát bằng `EmitSignal(SignalName.PartSevered, part, (int)cause, impulse)`; tham số enum truyền thẳng được.
- **Solver và logic thuần** là class C# thường (không kế thừa Node), được node chủ khởi tạo và truyền settings vào. Chỉ thứ cần sống trong scene tree mới là Node.
- **Physics** chỉ chạy trong `VehicleRoot._IntegrateForces` (60 Hz). UI và hình ảnh cập nhật trong `_Process`. Input được chốt trong `_Process` / `_UnhandledInput` và đọc ở tick physics.
- **Không cấp phát mỗi tick** trong vòng lặp physics nếu tránh được: dùng lại `List<T>` bằng `Clear()`.
- **Không static mutable** ngoài `Instance` của autoload.
- **Lỗi:** không ném exception qua ranh giới system. Dùng `GD.PushError` (lỗi cấu hình, dữ liệu sai) và `GD.PushWarning` (thiếu thứ tuỳ chọn), rồi trả giá trị an toàn.
- **Log:** `GD.Print($"[Building] ...")` với tiền tố là tên system. Log mỗi tick phải nằm sau cờ `[Export] public bool DebugLog`.
- **Comment và XML doc tiếng Anh.** Mỗi public member có `/// <summary>` một dòng. Ghi đơn vị vào summary nếu tên chưa có hậu tố.
- **Hàm ngắn:** quá ~40 dòng thì tách hàm private.
- Build bằng `dotnet build` ở gốc repo trước khi commit. **`main` luôn phải build được.**

---

## 6. Quy trình làm việc cho agent

1. Đọc file này, file hướng dẫn riêng, và các diagram `.mmd` mà file hướng dẫn chỉ ra.
2. Làm **đúng thứ tự** danh sách việc trong file hướng dẫn. Không nhảy sang việc P1/P2 khi việc P0 trước đó chưa xong.
3. Mỗi việc: viết code → `dotnet build` → chạy sandbox scene của việc đó → kiểm tra mục "Xong khi" → commit.
4. Không đổi chữ ký public của class người khác. Không sửa file ngoài thư mục của mình.
5. Thiếu thứ từ người khác (chưa có hoặc đang là stub): **viết mock trong `tests/<bạn>/` hoặc dùng stub sẵn có**, không chờ, không tự viết hộ trong thư mục của họ.
6. Cần một public API chưa có trong hợp đồng: thêm vào code của mình nếu thuộc thư mục mình, rồi ghi một dòng vào `docs/API_CHANGES.md` và báo người dùng nhắn cho cả nhóm.
7. Không thêm package NuGet, addon hay plugin nếu người dùng chưa đồng ý.

### Git

- Nhánh: `dev/a-building`, `dev/b-physics`, `dev/c-control`. `main` là nhánh tích hợp do C quản lý.
- Commit nhỏ, thông điệp: `[A] building: derive stages from decoupler depth`.
- Kéo `main` về nhánh mình ít nhất mỗi 2 giờ, và **bắt buộc** trước mỗi mốc tích hợp (M0 tới M4).
- Merge vào `main` ngay khi một việc "Xong khi" đạt và build sạch; không giữ nhánh quá nửa ngày.
- `.gitignore` bỏ `.godot/`, `bin/`, `obj/`, `*.user`, `export/`.

---

## 7. Code Core dùng chung (C tạo ở giờ 0, mọi người dùng nguyên văn)

### `core/Enums.cs`

```csharp
using System;

namespace RocketGame.Core;

public enum FuelType { Monopropellant, Kerolox, Hydrolox, Methalox }
public enum ComponentState { Idle, Active, Overheated, Frozen, Broken }
public enum PartGroup { CommandPod, Fairing, Parachute, HeatShield, CoreTank, LiquidEngine, SolidBooster, AeroSurface, Structural }
public enum Side { Top, Bottom, Left, Right }
public enum ModuleSlotType { Avionics, Thermal, Power }
public enum AcsDeviceKind { Imu, StarTracker, ReactionWheel, RcsThruster }
public enum ThermalDeviceKind { PassiveInsulation, ActiveRadiator, RadioisotopeHeater }
public enum PowerDeviceKind { Battery, SolarArray, Rtg }
public enum SasMode { Off, StabilityAssist, HoldPitch, Prograde, Retrograde }
public enum SeverCause { Jettison, Break }
public enum FailureCause { None, Shear, Bending, JointOverload, Thermal, Collision, TargetNotReached, InsufficientDeltaV, TippedOver, Aborted }
public enum FlightEventType { LightningStrike, CryoLeak, DebrisWarning, SeuGlitch, WindShear, MaxQExceeded, GLimitExceeded, EngineFlameout, ComponentOverheat, PowerBrownout }
public enum FlightPhase { PreLaunch, PoweredAscent, Coast, OrbitReached, Entry, Descent, Landed, Failed }
public enum GameState { Build, Flight, Debrief, Cruise, Victory, Research }
public enum MissionOutcome { Success, Failed, Aborted }
public enum SegmentKind { EarthAscent, Cruise, MarsEdl }
public enum ParachuteState { Stowed, Deployed, Cut }

/// <summary>Export-friendly set of PartGroup values. Bit i = PartGroup with value i.</summary>
[Flags]
public enum PartGroupMask
{
	None = 0, CommandPod = 1 << 0, Fairing = 1 << 1, Parachute = 1 << 2, HeatShield = 1 << 3,
	CoreTank = 1 << 4, LiquidEngine = 1 << 5, SolidBooster = 1 << 6, AeroSurface = 1 << 7, Structural = 1 << 8,
}

/// <summary>Export-friendly set of SasMode values (Off is always available).</summary>
[Flags]
public enum SasModeMask { None = 0, StabilityAssist = 1 << 0, HoldPitch = 1 << 1, Prograde = 1 << 2, Retrograde = 1 << 3 }

/// <summary>Export-friendly set of FlightEventType values. Bit i = FlightEventType with value i.</summary>
[Flags]
public enum FlightEventMask
{
	None = 0, LightningStrike = 1 << 0, CryoLeak = 1 << 1, DebrisWarning = 1 << 2, SeuGlitch = 1 << 3, WindShear = 1 << 4,
	MaxQExceeded = 1 << 5, GLimitExceeded = 1 << 6, EngineFlameout = 1 << 7, ComponentOverheat = 1 << 8, PowerBrownout = 1 << 9,
}

public static class MaskExtensions
{
	public static PartGroupMask ToMask(this PartGroup g) => (PartGroupMask)(1 << (int)g);
	public static bool Has(this PartGroupMask m, PartGroup g) => (m & g.ToMask()) != 0;
	public static SasModeMask ToMask(this SasMode s) => s == SasMode.Off ? SasModeMask.None : (SasModeMask)(1 << ((int)s - 1));
	public static bool Has(this SasModeMask m, SasMode s) => s == SasMode.Off || (m & s.ToMask()) != 0;
	public static FlightEventMask ToMask(this FlightEventType t) => (FlightEventMask)(1 << (int)t);
	public static bool Has(this FlightEventMask m, FlightEventType t) => (m & t.ToMask()) != 0;
}
```

### `core/CoreTypes.cs`

```csharp
using Godot;

namespace RocketGame.Core;

/// <summary>A cell edge: the cell and which of its sides.</summary>
public readonly record struct Edge(Vector2I Cell, Side Side);

/// <summary>Mass properties in the vehicle-local frame (meters, kg, kg*m^2).</summary>
public struct MassProperties
{
	public float TotalMass;
	public Vector2 CenterOfMass;
	public float MomentOfInertia;
}

/// <summary>Air state at one altitude.</summary>
public struct AtmosphereSample
{
	public float Density;       // kg/m^3
	public float Pressure;      // Pa
	public float Temperature;   // K
	public float SpeedOfSound;  // m/s
	public static AtmosphereSample Vacuum => new() { Density = 0f, Pressure = 0f, Temperature = 3f, SpeedOfSound = 1f };
}

/// <summary>Something physics detected that control may need to react to.</summary>
public struct FlightEvent
{
	public FlightEventType Type;
	public float Severity;      // 0..1
	public string SourceId;     // ModelId of the part/module involved, or empty
}

/// <summary>The single result of one whole flight attempt (all segments). RefCounted so it can travel through signals.</summary>
[GlobalClass]
public partial class FlightResult : RefCounted
{
	public string MissionId { get; set; } = "";
	public MissionOutcome Outcome { get; set; }
	public int SegmentsCompleted { get; set; }
	public float MaxAltitude { get; set; }
	public float MaxSpeed { get; set; }
	public float MaxQ { get; set; }
	public float MaxG { get; set; }
	public float FlightTime { get; set; }
	public float CruiseDaysSurvived { get; set; }
	public float LandingSpeedMps { get; set; }
	public FailureCause FailureCause { get; set; }
	public string FailedModelId { get; set; } = "";
	/// <summary>PartGroup of the failed part, or -1. Used for the novelty key.</summary>
	public int FailedPartGroup { get; set; } = -1;
}
```

### `core/FlightMath.cs`

```csharp
using Godot;

namespace RocketGame.Core;

/// <summary>Shared 2D flight geometry. World 2D: X = downrange at the pad, Y = up at the pad, planet center at (0, -Radius).</summary>
public static class FlightMath
{
	public const float G0 = 9.80665f;
	public const float StefanBoltzmann = 5.670374e-8f;

	public static Vector2 LocalUp(Vector2 position, Vector2 planetCenter) => (position - planetCenter).Normalized();

	/// <summary>Local horizontal direction, pointing +X at the pad.</summary>
	public static Vector2 LocalEast(Vector2 position, Vector2 planetCenter)
	{
		Vector2 up = LocalUp(position, planetCenter);
		return new Vector2(up.Y, -up.X);
	}

	public static float Altitude(Vector2 position, Vector2 planetCenter, float planetRadius)
		=> (position - planetCenter).Length() - planetRadius;

	public static float HorizontalSpeed(Vector2 velocity, Vector2 position, Vector2 planetCenter)
		=> velocity.Dot(LocalEast(position, planetCenter));

	public static float VerticalSpeed(Vector2 velocity, Vector2 position, Vector2 planetCenter)
		=> velocity.Dot(LocalUp(position, planetCenter));

	public static float WrapDeg(float deg)
	{
		deg %= 360f;
		if (deg > 180f) deg -= 360f;
		if (deg <= -180f) deg += 360f;
		return deg;
	}

	/// <summary>Body attitude vs the local horizon: 90 = nose straight up, 0 = nose toward local east. bodyRotationRad = VehicleRoot.Rotation.Z.</summary>
	public static float AttitudeDeg(float bodyRotationRad, Vector2 position, Vector2 planetCenter)
	{
		Vector2 up = LocalUp(position, planetCenter);
		float horizonUpDeg = Mathf.RadToDeg(Mathf.Atan2(up.Y, up.X));
		float bodyUpDeg = 90f + Mathf.RadToDeg(bodyRotationRad);
		return WrapDeg(90f + bodyUpDeg - horizonUpDeg);
	}

	/// <summary>Velocity direction vs the local horizon, same convention as AttitudeDeg. Returns 90 when nearly at rest.</summary>
	public static float FlightPathAngleDeg(Vector2 velocity, Vector2 position, Vector2 planetCenter)
	{
		if (velocity.LengthSquared() < 0.01f) return 90f;
		Vector2 up = LocalUp(position, planetCenter);
		float horizonUpDeg = Mathf.RadToDeg(Mathf.Atan2(up.Y, up.X));
		float velDeg = Mathf.RadToDeg(Mathf.Atan2(velocity.Y, velocity.X));
		return WrapDeg(90f + velDeg - horizonUpDeg);
	}

	/// <summary>Vehicle-local vector to world, given VehicleRoot.Rotation.Z.</summary>
	public static Vector2 BodyToWorld(Vector2 local, float bodyRotationRad) => local.Rotated(bodyRotationRad);

	/// <summary>2D cross product = torque about +Z.</summary>
	public static float Cross(Vector2 r, Vector2 f) => r.X * f.Y - r.Y * f.X;
}
```

### `core/NodeGuard.cs`

```csharp
using Godot;

namespace RocketGame.Core;

public static class NodeGuard
{
	/// <summary>Returns false and disables the node when a required export is missing.</summary>
	public static bool Require(Node owner, GodotObject value, string exportName)
	{
		if (value != null) return true;
		GD.PushError($"[{owner.GetType().Name}] Missing required export '{exportName}' on {owner.GetPath()}");
		owner.ProcessMode = Node.ProcessModeEnum.Disabled;
		return false;
	}
}
```

### Quy ước dấu và hệ toạ độ (mọi người phải theo)

| Thứ | Quy ước |
| --- | --- |
| Thế giới 2D | X = hướng bay xa (downrange) tại bệ phóng, Y = lên. Tâm hành tinh tại `(0, -Radius)`. Bệ phóng (Trái Đất) và bãi đáp (sao Hỏa) tại gốc toạ độ. Trong Godot 3D: `(x, y, 0)`. |
| Hệ thân rocket (vehicle-local) | Gốc = góc dưới trái của ô `(0,0)` trên grid; +Y = mũi rocket; đơn vị mét = ô × `CellSize`. |
| Ô grid | `Cell.Y` tăng **lên trên**. `RocketPart.Cell` = ô dưới trái của footprint. |
| `GetGridPosition()` | Tâm footprint, đơn vị ô (ví dụ ô (2,3), footprint (1,2) → (2.5, 4.0)). Decoupler: trung điểm cạnh. |
| `GetLocalPosition()` | `GetGridPosition() * CellSize`, đơn vị mét, hệ thân. Physics dùng hàm này. |
| Mô-men `TorqueZ > 0` | Ngược chiều kim đồng hồ nhìn từ camera (+Z), tức mũi ngả về −X, `AttitudeDeg` tăng. |
| Lệnh chuẩn hoá `u ∈ [-1, 1]` | `u > 0` yêu cầu mô-men dương. |
| Phím pitch | `flight_pitch_right` (D) = mũi về +X = mô-men âm. Lệnh tay `u = -PitchAxis`. |
| `SetGimbal(+θ)` | Vector lực đẩy = trục +Y của thân xoay **theo chiều kim đồng hồ** θ độ: tạo mô-men dương cho động cơ nằm dưới CoM. |
| `SetDeflection(+δ)` | Lực khí động của fin có thành phần +X hệ thân: mô-men dương cho fin nằm dưới CoM. |
| `CommandTorque(+)` | Mô-men dương. `CommandTranslate(+1)` = lực về +X hệ thân. |
| Góc thái độ | `FlightMath.AttitudeDeg`: 90 = thẳng đứng, 0 = nằm ngang hướng đông, −90 = chúc thẳng xuống. |

---

## 8. Quyết định kỹ thuật khi code (bổ sung so với tài liệu thiết kế)

Các quyết định này mới hơn diagram; nếu diagram nói khác, làm theo bảng này.

| # | Quyết định | Ảnh hưởng |
| --- | --- | --- |
| 1 | **Một `flight.tscn` cho cả 3 chặng.** Chặng 2 là lớp phủ trong flight scene: `VehicleRoot.Freeze = true`, ẩn HUD bay, hiện `CruiseHUD`. Sang chặng 3, `RestoreFromRun` đặt lại chính `VehicleRoot` đó tại điểm vào khí quyển sao Hỏa. Part không bao giờ rời scene tree giữa các chặng. | C |
| 2 | **Không dùng va chạm của Jolt.** Mọi `CollisionShape3D` của rocket và debris có `collision_layer = 0`, `collision_mask = 0` (chỉ để Godot không cảnh báo). Chạm đất tính bằng giải tích: điểm thấp nhất của part có độ cao ≤ 0. Ở `PreLaunch` rocket có `Freeze = true` cho tới lần bấm Stage đầu tiên. | B, C |
| 3 | **Hành tinh thu nhỏ.** Trái Đất R = 600 km, g = 9,81; sao Hỏa R = 320 km, g = 3,71. Mọi số nằm trong `.tres` của `CelestialBodyDefinition`, code không chứa số nào của hành tinh. | B |
| 4 | `PhysicsStepInput` thêm `float BodyRotationRad` (= `VehicleRoot.Rotation.Z`). | B, C |
| 5 | `PartTree.SeverSubtree(RocketPart part, SeverCause cause, float impulseNs = 0)`; signal `PartSevered(RocketPart part, SeverCause cause, float impulseNs)`. Xung tách hướng từ part cha sang part con. | A, B, C |
| 6 | `VehicleRoot.MassPropertiesChanged()` không có tham số; ai cần thì đọc `Vehicle.MassProps`. | C |
| 7 | Kiểu đi qua signal là `RefCounted`: `AssembledRocket` (A), `MissionRun` (C), `FlightResult` (Core), `ActiveAnomaly` (C). | Mọi người |
| 8 | Mảng enum đổi thành mask: `AllowedTop`, `AllowedBottom`, `AllowedSides` (`PartGroupMask`, Sides dùng chung cho Left và Right); `EnabledSasModes` (`SasModeMask`); `EnabledHazards` (`FlightEventMask`). | A, B |
| 9 | `CommandPodPartDefinition.ModuleSlots` tách thành `AvionicsSlots`, `ThermalSlots`, `PowerSlots` (int). | A |
| 10 | `CelestialBodyDefinition` thêm `SurfaceTemperatureK`, `SpeedOfSoundMps`, `SuttonGravesConstant`, `AtmosphereTopM`. | B |
| 11 | `StructuralStressEvaluator` do **B** viết (đổi chủ để cân tải); file vẫn ở `physics/`. | B |
| 12 | Luật trạng thái nhiệt (`Overheated` ở > `MaxSafeTempK`, hồi ở < 0,9×; `Broken` ở > 1,25×; `Frozen` ở < `MinSafeTempK`) do **A** viết trong `ThermalStateRules.Evaluate(IFlightComponent)`; physics gọi sau mỗi lần đổi `TempK`. | A, B |
| 13 | Bản P0 của `ControlAllocator`: một lệnh chuẩn hoá `u` áp cho mọi cơ cấu đang dùng được (gimbal = u × range, fin = u × max, wheel và RCS = u). Phân bổ theo thứ tự ưu tiên là P1. | C |
| 14 | `RocketDesign` tự lưu `user://current_design.json`; `SaveGame` (B) lưu `user://save.json` và không chứa design. | A, B |
| 15 | `CellSize` mặc định 2,0 m trong `build_settings.tres`. Grid mặc định 9 × 16 ô. | A |
| 16 | `StressReport` thuộc namespace Physics (không ở Core) vì nó tham chiếu `RocketPart`. | B |
| 17 | Field `MassProperties Mass` của `VehicleRoot` đổi tên thành **`MassProps`**, vì `RigidBody3D` đã có property `Mass`. | C |
| 18 | `StructuralStressEvaluator.EvaluateRecursiveLoad(PhysicsStepInput input, PhysicsStepOutput output)` rồi `CheckYieldLimits()` trả `StressReport`. | B, C |
| 19 | `PhysicsCoordinator` giữ và công khai 4 đối tượng: `Failures` (`FailureDetector`), `BreakHandler` (`StructuralBreakHandler`), `StressEvaluator`, `Hazards` (`HazardMonitor`). `FailureDetector` báo Break qua C# event `BreakTriggered(RocketPart, FailureCause)`. | B, C |
| 20 | `TechTreeManager.GetUnlockedModels()` không có tham số; `PartCatalog` tự lọc theo loại. | A, B |
| 21 | `PartTree.SeverSubtree` **giữ nguyên** `ParentJoint` của part bị cắt (chỉ gỡ nó khỏi `Children` của cha), để người nghe signal biết part cha. | A, C |
| 22 | Ba màn hình có node gốc riêng phát signal, không gọi Control: `BuildScreenRoot` (A: `[Export] BuildingSystem Building`, signal `ResearchRequested`), `ResearchScreenRoot` (B: signal `BuildRequested`), `DebriefScreen` (C). `GameFlowManager` nối các signal đó khi tạo scene. | A, B, C |
| 23 | UI của research được đọc `PartCatalog` để lấy tên và icon của model; logic research thì không. | B |

---

## 9. Khi cần đổi hợp đồng

1. Chỉ **thêm** (member mới, tham số mặc định); không đổi tên, không xoá thứ người khác đang dùng.
2. Ghi một dòng vào `docs/API_CHANGES.md`: `H7 | B | PhysicsStepOutput thêm float SolarFluxWm2 | lý do`.
3. Báo người dùng để họ nhắn cho cả nhóm. Người bị ảnh hưởng kéo `main` trước mốc kế tiếp.

## 10. Mức ưu tiên

- **P0**: cần cho vòng chơi Build → 3 chặng → Victory. Làm hết P0 trước mọi thứ khác.
- **P1**: làm thiết kế có chiều sâu (module, sự cố, SAS đủ mode, hiệu ứng chính). Chỉ làm sau mốc M3.
- **P2**: đánh bóng. Chỉ làm nếu còn giờ sau M4.

Mốc chung (giờ tính từ lúc cả nhóm bắt đầu, khoảng 18 giờ làm việc):

| Mốc | Giờ | Chạy được gì |
| --- | --- | --- |
| M0 Skeleton | H1.5 | `main` build được với stub của mọi class trong diagram |
| M1 Bay thử | H6 | Rocket mẫu bay trong flight scene với lực thật, ga, tách tầng, rơi |
| M2 Vòng chặng 1 | H10.5 | Research → Build → phóng → quỹ đạo hoặc hỏng → Debrief → nhận RP → mua node |
| M3 Tới sao Hỏa | H14 | Cả 3 chặng chạy, đáp được, màn Victory |
| M4 Đóng băng | H16 | Hết P1 đã chọn, cân bằng xong, chỉ còn sửa lỗi; H18 xuất bản build |
