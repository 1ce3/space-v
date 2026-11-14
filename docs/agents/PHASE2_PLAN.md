# Giai đoạn 2: Túi đồ khoang lái, vật liệu, nhiên liệu nghiên cứu, sự kiện cruise

Tài liệu này dùng chung cho cả ba dev (A, B, C), cùng cách viết với `A_BUILDING.md`, `B_PHYSICS_RESEARCH.md`, `C_CONTROL.md`.
Đọc `AGENTS.md` trước; mọi quy ước ở đó (đặt tên, `[Export]`, signal, thư mục, git, không hard-code số liệu) vẫn áp dụng.
Mỗi thay đổi API ghi một dòng vào `docs/API_CHANGES.md` như cũ (tiền tố `A10`, `B9`, `C9`…).

## 0. Quyết định đã chốt

| # | Chủ đề | Quyết định |
| --- | --- | --- |
| 1 | Module trong khoang lái | Bỏ 3 danh sách ô (Avionics/Thermal/Power). Khoang lái có **lưới túi đồ n×m**; mọi module (IMU, pin, solar, tản nhiệt…) và thiết bị mới (liên lạc, cảm biến) đặt chung vào lưới. Nâng cấp khoang = lưới lớn hơn. Lưới xây tên lửa (màn Build) **không đổi**. |
| 2 | Hình dạng thiết bị | Chỉ hình chữ nhật (`GridSize`, ví dụ 1×1, 1×2, 2×2). Không xoay. |
| 3 | Vật liệu | Chọn **cho từng part** nhưng chỉ ở các part là **vỏ**: bình nhiên liệu, khoang lái, và **vỏ tách** (decoupler, có thêm hình vỏ liên tầng che khớp tách khi xếp dọc). Part khác (động cơ, dù, tấm chắn nhiệt, cánh, fairing) giữ đặc tính cố định. Bắt buộc có vật liệu (mặc định là nhôm). Có nút "áp cho cả tầng / cả tên lửa". |
| 4 | Nhiên liệu | Chỉ là node nghiên cứu: phải nghiên cứu loại nhiên liệu trước thì mới nghiên cứu được bình và động cơ dùng nó. Không có hệ nhiên liệu mới; vẫn 4 `FuelType` hiện có. |
| 5 | Sự kiện | Trọng tâm ở **chặng cruise** (bão mặt trời, bãi thiên thạch, lỗi thiết bị), thêm vài sự kiện nhỏ lúc **đốt thoát quỹ đạo**. Sự cố trong lúc bay (C7a) giữ nguyên. |
| 6 | Né thiên thạch | Diễn ra **ngay trong cảnh bay hiện tại**: cruise tạm dừng, bật lại vật lý, người chơi lái bằng A/D (xoay) + bấm động cơ chính trong chốc lát (tốn nhiên liệu = tốn delta-v), hoặc Q/E nếu có RCS. Có RCS thì được chọn **tự né**. |
| 7 | Sửa thiết bị | Thiết bị hỏng có nút **Khởi động lại**: tỉ lệ thành công theo cấp, mỗi lần thử tốn điện và có thời gian chờ. |

Ngoài phạm vi giai đoạn này: cây công nghệ 6 nhánh với tên thiết bị thật, 81 part mới, động cơ điện (ion/Hall), nhiên liệu lạ (ethanol, hypergolic, xenon, hạt nhân) trong `Gameplay Systems … Specification.pdf`. Có thể làm ở giai đoạn sau, dựa trên khung của giai đoạn này.

## 1. Mốc và thứ tự

| Mốc | Nội dung | Ai mở khoá |
| --- | --- | --- |
| **P2-M0** Hợp đồng | Enum mới, field mới trên definition, interface và stub của mục 6 có trong repo, build sạch | **C9 (làm đầu tiên)** |
| **P2-M1** Lưới khoang lái chơi được | Đặt/gỡ module trên lưới ở màn Build, lưu/nạp design, Mars stack dùng lưới, mọi test cũ chạy lại xanh | A10, A11 |
| **P2-M2** Vật liệu | Chọn vật liệu cho vỏ, ảnh hưởng khối lượng/nhiệt/va đập; vỏ liên tầng hiện trên tên lửa | A12, B9 |
| **P2-M3** Sự kiện cruise | Bão mặt trời, lỗi thiết bị, sự kiện lúc thoát quỹ đạo, khởi động lại thiết bị | B10, B11, C10, C12 |
| **P2-M4** Né thiên thạch | Chế độ né trong cảnh bay, va chạm gây hư hại, tự né | B12, C11 |
| sau đó | **C8** đóng gói như kế hoạch cũ | C |

Ba người làm song song sau P2-M0. Ai cần thứ của người khác mà chưa có thì dùng stub ở mục 6.

## 2. Hệ thống 1: Lưới túi đồ trong khoang lái

### 2.1 Dữ liệu

- `ModuleSlotType` **giữ nguyên ý nghĩa nhưng đổi vai**: nay là **nhóm thiết bị**, không còn là loại ô. Thêm hai giá trị **ở cuối** (giữ số nguyên cũ): `Sensor = 3`, `Comms = 4`.
  Code đang dùng `SlotType == Avionics` để biết module nào mất điện khi brownout (PowerSolver, CruiseSimulator, AnomalyResponseHandler) **không phải sửa**.
- `ModuleDefinition` thêm:
  - `Vector2I GridSize` (mặc định 1×1)
  - `float RadiationResistance` (0..1, mặc định 0): giảm xác suất hỏng do bão mặt trời
  - `float RebootSuccessChance` (0..1): tỉ lệ khởi động lại thành công khi hỏng
  - `float RebootEnergyWh`: điện tốn cho mỗi lần thử
- `CommandPodPartDefinition`: thay `AvionicsSlots`, `ThermalSlots`, `PowerSlots` bằng `Vector2I InventorySize`. Giữ `GetSlotCount` tạm thời (trả `InventorySize.X * InventorySize.Y`) rồi xoá ở cuối A10.
- Lớp mới `InstrumentModuleDefinition : ModuleDefinition` (nhóm `Sensor` hoặc `Comms`):
  - `InstrumentKind Kind`: `Comms`, `RadiationSensor`, `FuelSensor`, `PressureSensor`
  - `float WarningLeadSeconds`: thời gian báo trước thiên thạch (Comms)
  - `float FlareWarningDays`: số ngày báo trước bão mặt trời (RadiationSensor)
  - `float LeakReduction` (0..1): giảm tốc độ rò (FuelSensor cho bình, PressureSensor cho vỏ khoang)
  - `bool AllowsAutoDodge`: được chọn tự né (cần thêm RCS)
- Runtime: `InstrumentModule : PodModule`. `PodModule.Create` thêm nhánh cho định nghĩa mới.

### 2.2 Đặt module

- `PodModule` thêm `Vector2I GridCell` (ô góc trên trái). Bỏ `SlotIndex`.
- `CommandPodPart` thêm `bool CanPlaceModule(ModuleDefinition def, Vector2I cell)` (trong lưới, không chồng lên module khác) và dùng nó trong `AddModule`.
- `RocketDesign.ModuleEntry`: thay `SlotType`/`SlotIndex` bằng `Vector2I Cell`. File design cũ (có `Slot`/`Index`) **nạp được**: xếp lần lượt từ ô (0,0) theo hàng, bỏ module nào không vừa và báo một dòng.
- `RocketAssembler` kiểm tra lưới thay cho kiểm tra số ô; lỗi bằng tiếng Việt như cũ ("Module X không vừa lưới khoang lái").

### 2.3 Giao diện (màn Build)

- Bảng bên phải khi chọn khoang lái: vẽ lưới `InventorySize`, mỗi module là một khối chữ nhật có tên ngắn.
- Danh sách module đã mở khoá ở dưới lưới; **kéo thả** từ danh sách vào lưới, kéo khối ra ngoài để gỡ, chuột phải để gỡ. Ô hợp lệ tô xanh, không hợp lệ tô đỏ khi đang kéo.
- Tooltip (A9 đã có `GetFormattedTooltip`) hiện thêm kích thước ô, kháng bức xạ, tỉ lệ khởi động lại.
- Weight của module vẫn trừ vào ngân sách như cũ.

### 2.4 Số liệu khởi điểm (đề xuất, chỉnh trong `.tres`)

| Khoang | Lưới | Ghi chú |
| --- | --- | --- |
| `pod_mk1` | 2×2 | như cũ, tier 0 |
| `pod_mk2` | 3×3 | như cũ, tier 2 |
| `pod_mk3` (mới) | 4×3 | tier 3, mở bằng node mới `t3_pod_mk3` |

| Module | Lưới | Kháng bức xạ | Khởi động lại |
| --- | --- | --- | --- |
| `imu_g1` | 1×1 | 0.0 | 0.3 |
| `imu_g2` | 1×1 | 0.3 | 0.6 |
| `startracker_st1` | 1×2 | 0.5 | 0.7 |
| `wheel_rw1` | 1×1 | 0.2 | 0.5 |
| `rcs_r1` | 1×2 | 0.2 | 0.5 |
| `battery_b1` / `battery_b2` | 1×1 / 1×2 | 0.1 / 0.2 | 0.4 / 0.5 |
| `solar_s1` | 1×2 | 0.1 | 0.4 |
| `rtg_t1` | 2×2 | 0.8 | 0.9 |
| `insulation_i1` / `radiator_a1` / `rhu_h1` | 1×1 / 1×2 / 1×1 | 0.5 / 0.2 / 0.6 | 0.5 |

Thiết bị mới (6 cái, đủ để chơi; sau này mở rộng theo tài liệu 2):

| ModelId | Nhóm / Kind | Tier | Lưới | Weight | Tác dụng chính |
| --- | --- | --- | --- | --- | --- |
| `comm_beacon_b1` | Comms | 1 | 1×1 | 1 | báo thiên thạch trước 6 s |
| `comm_sband_s2` | Comms | 2 | 1×2 | 2 | báo trước 12 s |
| `comm_relay_r3` | Comms | 3 | 2×2 | 3 | báo trước 20 s, `AllowsAutoDodge` |
| `sensor_rad_d1` | Sensor / RadiationSensor | 1 | 1×1 | 1 | báo bão mặt trời trước 3 ngày |
| `sensor_fuel_c2` | Sensor / FuelSensor | 2 | 1×1 | 1 | rò bình nhiên liệu giảm 50 % |
| `sensor_press_p2` | Sensor / PressureSensor | 2 | 1×1 | 1 | rò áp suất vỏ khoang giảm 50 % |

Không có Comms: báo trước 3 s. Khởi động lại theo tier: T1 0.3, T2 0.5, T3 0.7, T4 0.85, T5 0.95.

## 3. Hệ thống 2: Vật liệu

### 3.1 Dữ liệu

- Lớp mới `MaterialDefinition : Resource` trong `building/data/`, file trong `data/materials/` (thư mục mới, chủ: A), tên file = `MaterialId`:
  - `string MaterialId`, `string DisplayName`, `int Tier`, `int BuildWeightCost` (cộng thêm vào weight của part)
  - `float MassMultiplier` (nhân `DryMass`)
  - `float MaxTempBonusK` (cộng vào `MaxSafeTempK`)
  - `float RadiationShielding` (0..1, chỉ có tác dụng khi là vỏ khoang lái)
  - `float ImpactToleranceMultiplier` (nhân `ImpactToleranceMps` và khả năng chịu va thiên thạch)
  - `float StrengthMultiplier` (nhân `MaxShear`, `MaxBending`, `AttachmentStrength`)
  - `Color Tint` (màu vỏ để nhìn ra vật liệu)
- Vật liệu được mở khoá bằng tech node như part: `UnlocksModelIds` chứa `MaterialId`. `PartCatalog` tra được vật liệu (thêm `GetMaterial(id)`).
- `PartDefinition` thêm `bool AcceptsMaterial` (true cho bình, khoang lái, decoupler) và `string DefaultMaterialId = "mat_al2024"`.

### 3.2 Runtime và lắp ráp

- `RocketPart` thêm `MaterialDefinition Material` (null với part không nhận vật liệu).
- Mọi chỗ đọc `DryMass`, `MaxSafeTempK`, `ImpactToleranceMps`, `MaxShear`, `MaxBending`, `AttachmentStrength` của part đọc qua **giá trị hiệu dụng** (`GetEffectiveDryMass()`, `GetEffectiveMaxSafeTempK()`…) trên `RocketPart`. A thêm các hàm này; B đổi các solver sang dùng chúng (B9).
- `RocketDesign.PartEntry` thêm `string MaterialId` (rỗng = mặc định). Design cũ nạp được.
- Weight của part = `BuildWeight + Material.BuildWeightCost`.

### 3.3 Vỏ liên tầng

- Decoupler giữa hai tầng có thêm **hình vỏ liên tầng** bao quanh khớp tách, rộng bằng tầng dưới (chỉ là hình, không thêm part). Khi tách, vỏ bay theo tầng dưới. Màu theo vật liệu của decoupler.

### 3.4 Vật liệu khởi điểm (đề xuất)

| MaterialId | Tier | Khối lượng × | Chịu nhiệt + | Chắn bức xạ | Va đập × | Bền × | Weight + |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `mat_al2024` (nhôm, mặc định) | 0 | 1.00 | 0 K | 0.10 | 1.0 | 1.0 | 0 |
| `mat_steel304` (thép) | 1 | 1.40 | +300 K | 0.25 | 1.5 | 1.3 | 0 |
| `mat_alli2195` (nhôm-lithi) | 1 | 0.85 | 0 K | 0.10 | 1.0 | 1.0 | +1 |
| `mat_cfrp` (sợi carbon) | 2 | 0.70 | −100 K | 0.05 | 0.8 | 1.1 | +1 |
| `mat_titanium` | 3 | 0.95 | +400 K | 0.20 | 1.4 | 1.2 | +2 |
| `mat_whipple` (vỏ chắn nhiều lớp) | 3 | 1.20 | +100 K | 0.35 | 2.5 | 1.0 | +2 |

## 4. Hệ thống 3: Nhiên liệu là node nghiên cứu

- Node mới, **không mở part nào** (`UnlocksModelIds` rỗng): `fuel_kerolox` (tier 0, mở sẵn từ đầu), `fuel_monoprop` (tier 1), `fuel_hydrolox` (tier 2), `fuel_methalox` (tier 3).
- Mọi node mở bình hoặc động cơ của một loại nhiên liệu thêm node nhiên liệu đó vào `PrerequisiteNodeIds`. Hệ tiên quyết hiện có làm phần còn lại; **không cần sửa code** nếu không muốn.
- Tuỳ chọn (P2): `TechNode` thêm `string Category` (`Fuel`, `Engine`, `Pod`, `Material`, `Instrument`…) để màn nghiên cứu tô màu theo nhóm.

## 5. Hệ thống 4: Sự kiện, né thiên thạch, sửa thiết bị

### 5.1 Enum (cuối danh sách, giữ số cũ)

`FlightEventType` thêm `SolarFlare = 10`, `MeteorField = 11`, `HullBreach = 12`, `InstrumentFailure = 13`.

### 5.2 Bộ điều phối sự kiện cruise (B)

- Lớp mới `CruiseEventDirector` trong `physics/`, gọi từ `CruiseSimulator`.
- Lúc bắt đầu cruise: chọn `N = RandiRange(CruiseEventsMin, CruiseEventsMax)` sự kiện (mặc định 2..4), rải vào các ngày ngẫu nhiên cách nhau ít nhất `CruiseEventMinGapDays` (mặc định 20). Loại theo trọng số trong `PhysicsSettings`: `SolarFlare` 40, `MeteorField` 40, `InstrumentFailure` 20.
- Báo trước: nếu có `RadiationSensor`, `SolarFlare` được báo `FlareWarningDays` ngày trước (event `SolarFlare` với `Severity = 0` là "cảnh báo", `Severity = 1` là "đang xảy ra").
- `StepDay()` trả event trong `CruiseStepResult.Events` như hiện nay.

### 5.3 Bão mặt trời (B tính, C hiển thị)

- Mỗi module trong khoang: xác suất hỏng
  `p = FlareBaseDamageChance × (1 − RadiationShielding của vật liệu khoang lái) × (1 − RadiationResistance của module)`
  (`FlareBaseDamageChance` mặc định 0.5). Module tắt nguồn trước bão (người chơi chọn "Tắt thiết bị" khi được báo trước) dùng `p × 0.3`.
- Module hỏng: `State = Broken`, `IsPowered = false`.
- Kết quả trả về danh sách module hỏng (`CruiseStepResult.DamagedModules`).

### 5.4 Lỗi thiết bị ngẫu nhiên (B)

`InstrumentFailure`: một module ngẫu nhiên hỏng (`Broken`), không phụ thuộc vật liệu.

### 5.5 Sự kiện lúc đốt thoát quỹ đạo (B)

Trong `ExecuteTransferBurn()`: xác suất `TransferBurnEventChance` (0.3) xảy ra một trong hai: động cơ tắt giữa chừng (`EngineFlameout`, người chơi đốt lại, tốn thêm 5 % delta-v) hoặc rò bình (`CryoLeak` như C7a, bịt bằng V).

### 5.6 Né thiên thạch (C)

Luồng:
1. `CruiseController` nhận `MeteorField`: dừng tua, HUD đếm ngược `WarningLeadSeconds` của Comms tốt nhất (không có Comms: 3 s).
2. Nếu có RCS **và** một Comms `AllowsAutoDodge`: hỏi "Tự né? (Y/N)". Có thì tàu tự né (tốn monoprop), bỏ qua bước 3.
3. **Chế độ né** trong cảnh bay: bỏ đóng băng vật lý; tắt trọng lực và khí động (khung toạ độ quán tính); camera lùi ra. Node mới `AsteroidField` sinh đá dọc một dải ngang bằng `FastNoiseLite` (mật độ theo noise, kích thước 2–8 m, tốc độ tương đối 20–60 m/s, đi từ phải sang trái). Thời lượng `DodgeDurationSeconds` (45).
   - A/D xoay, Z/X/Shift/Ctrl bấm động cơ chính (nhiên liệu tiêu thật, delta-v giảm thật).
   - Q/E đẩy ngang nếu có RCS.
4. Hết giờ: đóng băng lại, tính lại delta-v, cruise chạy tiếp.

Va chạm (B tính hậu quả, C phát hiện va chạm):
- Tốc độ va `v` so với `ImpactToleranceMps × ImpactToleranceMultiplier` của part bị trúng:
  - dưới ngưỡng: trầy, không hậu quả
  - trên ngưỡng: bình nhiên liệu → rò (như `CryoLeak`); khoang lái → `HullBreach` (rò áp suất: mỗi `HullBreachFailSeconds` một module ngẫu nhiên hỏng cho tới khi bịt bằng V; `PressureSensor` giảm tốc độ); động cơ → hỏng
  - trên 3 lần ngưỡng: part vỡ (`Break` như hiện nay)
- `FuelSensor` / `PressureSensor` giảm tốc độ rò theo `LeakReduction`.

### 5.7 Khởi động lại thiết bị (C giao diện, B/A runtime)

- HUD cruise và HUD bay liệt kê module `Broken` kèm nút **Khởi động lại**.
- Mỗi lần thử: trừ `RebootEnergyWh` khỏi pin (không đủ điện thì không thử được); thành công với xác suất `RebootSuccessChance` → `State = Idle`, `IsPowered = true`; thất bại → chờ `RebootCooldown` (cruise: 5 ngày; bay: 10 s).
- Hàm runtime: `PodModule.TryReboot(RandomNumberGenerator rng)` trả `true/false` (A thêm), việc trừ điện do `PowerSolver.TryDrawEnergy(pod, wh)` (B thêm).

## 6. Hợp đồng chung và stub (C9 tạo ở P2-M0)

| Thứ | Chủ | Stub ở P2-M0 |
| --- | --- | --- |
| `ModuleSlotType.Sensor/Comms`, `FlightEventType` mới, `InstrumentKind` | C (`core/`) | đầy đủ |
| `ModuleDefinition.GridSize/RadiationResistance/RebootSuccessChance/RebootEnergyWh`, `InstrumentModuleDefinition`, `CommandPodPartDefinition.InventorySize` | A | field có, giá trị mặc định |
| `MaterialDefinition`, `PartDefinition.AcceptsMaterial/DefaultMaterialId`, `RocketPart.Material` + `GetEffective…()` | A | `GetEffective…()` trả giá trị gốc |
| `PartCatalog.GetMaterial(id)` | A | trả null |
| `PodModule.TryReboot(rng)` | A | trả false |
| `CruiseEventDirector`, `CruiseStepResult.DamagedModules` | B | không sinh event |
| `PowerSolver.TryDrawEnergy(pod, wh)` | B | trả true |
| `MeteorImpact.Resolve(part, speedMps)` (hậu quả va chạm) | B | không làm gì |
| `AsteroidField`, chế độ né trong `CruiseController` | C | — |

## 7. Danh sách việc

### Dev A (Building)

- **A10. Lưới khoang lái (P0, P2-M1):** mục 2.1, 2.2; sửa `RocketDesign` (kèm nạp design cũ), `RocketAssembler`, `BuildingSystem` (`InstallModule(modelId, cell)`, `RemoveModule(cell)`, signal `ModuleChanged(cell)`), `SampleDesigns.CreateMarsStack` dùng ô lưới; sửa test `tests/a/` theo API mới.
- **A11. Giao diện túi đồ (P0, P2-M1):** mục 2.3.
- **A12. Vật liệu (P0, P2-M2):** mục 3.1–3.3 phần building: lớp, `data/materials/` 6 file, chọn vật liệu cho part ở bảng bên phải + nút áp cả tầng/cả tên lửa, `GetEffective…()`, vỏ liên tầng, màu vỏ theo vật liệu.
- **A13. Nội dung (P1, P2-M1/M3):** cập nhật 12 module `.tres` và 3 khoang theo bảng 2.4; thêm 6 thiết bị và `pod_mk3`; `PodModule.TryReboot`.
- **A14. Test (P0):** test lưới (vừa / chồng / ngoài lưới), nạp design cũ, khối lượng theo vật liệu, `TryReboot` với seed cố định.

### Dev B (Physics và Research)

- **B9. Vật liệu trong vật lý (P0, P2-M2):** đổi mọi chỗ đọc khối lượng, nhiệt, giới hạn kết cấu, va đập sang `GetEffective…()`; chạy lại `hazard_test`, `structure_thermal_test`, `physics_harness`.
- **B10. Cây nghiên cứu (P0, P2-M2):** node nhiên liệu (mục 4), node vật liệu (`t1_steel`, `t1_alli`, `t2_cfrp`, `t3_titanium`, `t3_whipple`), node thiết bị (`t1_comms`, `t2_comms`, `t3_relay`, `t1_rad_sensor`, `t2_sensors`), `t3_pod_mk3`; cân bằng giá RP với tổng 140 RP của chiến dịch; màn nghiên cứu cuộn ngang nếu thiếu chỗ.
- **B11. Sự kiện cruise (P0, P2-M3):** `CruiseEventDirector` (5.2), bão mặt trời (5.3), lỗi thiết bị (5.4), sự kiện lúc thoát quỹ đạo (5.5), `PowerSolver.TryDrawEnergy`. Mọi hằng số trong `PhysicsSettings`.
- **B12. Hậu quả va chạm thiên thạch (P0, P2-M4):** `MeteorImpact.Resolve` (5.6), `HullBreach` và giảm rò theo cảm biến.
- **B13. Test (P0):** test cho director (seed cố định: số sự kiện, khoảng cách ngày), công thức bão, va chạm.

### Dev C (Control)

- **C9. Hợp đồng (P0, P2-M0, làm đầu tiên):** enum `core/`, stub ở mục 6, build sạch, ghi `API_CHANGES.md`.
- **C10. Sự cố theo thiết bị (P0, P2-M3):** `AnomalyResponseHandler` đọc thiết bị trong khoang (báo trước, tự xử lý); nhận event cruise; khởi động lại thiết bị (5.7).
- **C11. Né thiên thạch (P0, P2-M4):** chế độ né, `AsteroidField`, tự né, nối `MeteorImpact.Resolve`.
- **C12. HUD (P0, P2-M3):** HUD cruise (cảnh báo bão + "Tắt thiết bị", danh sách thiết bị hỏng + nút khởi động lại, đếm ngược thiên thạch, hỏi tự né); HUD bay đầy đủ (C7b cũ: tiến độ, danh sách tầng, tấm chắn nhiệt).
- **C13. Ghép và test (P0):** smoke test mỗi mốc (mục 8), test tự động cho chế độ né (seed cố định).
- **C8. Đóng gói** như kế hoạch cũ.

## 8. Smoke test mỗi mốc

| Mốc | Kiểm |
| --- | --- |
| P2-M0 | Build sạch; mọi test cũ (`control_chain_test`, `edl_landing_test`, `tests/c/runs/*`) vẫn xanh |
| P2-M1 | Màn Build: kéo IMU, pin vào lưới pod_mk1 2×2; không đặt được khối 2×2 khi đã có 1 module; lưu, thoát, nạp lại đúng vị trí; Mars stack bay `edl_landing_test` ra Victory |
| P2-M2 | Đổi bình sang thép: khối lượng tăng 40 %, weight như cũ; vỏ liên tầng hiện và bay theo tầng khi tách |
| P2-M3 | Cruise 210 ngày gặp 2–4 sự kiện; có cảm biến bức xạ thì được báo trước; module hỏng khởi động lại được, tốn điện |
| P2-M4 | Gặp bãi thiên thạch: không Comms báo 3 s, có `comm_relay_r3` + RCS thì hỏi tự né; va chạm làm rò bình hoặc thủng vỏ; né xong cruise chạy tiếp, delta-v giảm đúng phần đã đốt |

## 9. Không làm trong giai đoạn này

- Cây 6 nhánh với tên thiết bị thật, 81 part/module mới, gói 71 tech node.
- Động cơ điện, nhiên liệu ngoài 4 `FuelType` hiện có.
- Thiết bị hình không phải chữ nhật, xoay thiết bị.
- Vật liệu cho động cơ, dù, tấm chắn nhiệt, cánh, fairing.

## 10. Câu hỏi còn mở

1. Trong chế độ né, động cơ chính có được dùng khi tàu đang ở chặng cruise với tầng đẩy đã tách hết không (chỉ còn động cơ hạ cánh)? Nếu không có lực đẩy nào thì chỉ còn xoay để giảm diện tích va.
2. Có cần lưu tiến độ giữa chừng cruise (thoát game lúc đang cruise) không?
3. Màu và biểu tượng thiết bị trong lưới: dùng chữ viết tắt (IMU, BAT…) trước, icon sau?
