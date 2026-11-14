# API changes

Một dòng cho mỗi thay đổi hợp đồng (chỉ thêm, không đổi tên hay xoá). Định dạng:

`Giờ | Người | Thay đổi | Lý do`

| Giờ | Người | Thay đổi | Lý do |
| --- | --- | --- | --- |
| B0 | B | `PhysicsSettings.SampleMachDrag(float mach)`: trả hệ số cản theo Mach, dùng mặc định của guide nếu `MachDragCurve` null | Không phụ thuộc việc curve trong .tres nạp đúng |
| B0 | B | `ResearchCampaignEngine.AddRP(int)`, `SaveProgress()` (public) | Cộng RP cho debug/test; `TechTreeManager.PurchaseNode` gọi lưu game |
