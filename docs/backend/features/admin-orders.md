# Admin Orders — quản trị đơn hàng theo workspace

Ngày bắt đầu: 2026-10-21  
Cập nhật gần nhất: 2026-10-21  
Trạng thái: Chờ nghiệm thu runtime  
Khu vực phụ trách: BE  
Mốc roadmap: [BE roadmap](../roadmap.md#các-mốc-và-điều-kiện-hoàn-tất) — mốc 12  
Hồ sơ liên quan: [ordering checkout](ordering-checkout.md), [physical shipping](physical-shipping.md), [payment/digital delivery](payment-digital-delivery.md), [FE Admin Orders](../../../Boilerate.Frontend/docs/features/admin-orders.md)

## 1. Mục tiêu và phạm vi

Admin Orders mở rộng contract hiện có để danh sách đơn hàng là màn làm việc chính; detail đọc trong cùng workspace, có inspector, action bar và timeline an toàn. Không có API hoặc màn hình chỉnh sửa tổng quát cho Order/OrderItem.

Trong lát này đã triển khai:

- `GET /api/admin/orders` với search, status, payment status, fulfillment status và khoảng ngày; sort ổn định `CreatedOn DESC, Id DESC`.
- `OrderSummary` có người nhận, email, phone và các status quản trị.
- `GET /api/admin/orders/{id}/history` trả timeline đã lọc và safe summary, không trả raw audit JSON.
- Giữ ba command hiện có: cancel, confirm payment, retry fulfillment; không thêm `PUT/PATCH Order`.
- `OrderingDevelopmentSeeder` và `SeedData/ordering.json`, đăng ký sau Catalog seeder trong gate `Modules:Catalog`.

Hoãn lại: dispatch/deliver/refund/return/claim/renewal mutation trên detail, bulk action, digital delivery content và nghiệm thu browser/runtime thật.

## 1A. Invariant của module

> Sau checkout, `Order` và `OrderItem` là snapshot bất biến. Mã đơn, sản phẩm, SKU, option, đơn giá, số lượng, fulfillment kind, tổng tiền, người nhận và địa chỉ không được sửa trực tiếp. Chỉ lifecycle status được thay đổi qua command nghiệp vụ có kiểm soát.

- Domain/transaction/DB: Order snapshot được tạo từ checkout; aggregate chỉ mở các method lifecycle, `OrderItem` có private setters và `BaseDbContext` từ chối modified/deleted snapshot. DB checks giữ enum, money, quantity, closure và completion hợp lệ; không thêm đường cập nhật aggregate tổng quát.
- API/permission/version: admin list/detail/history yêu cầu `Permissions.Order.View`; cancel yêu cầu `Order.Cancel`; payment confirmation dùng `Permissions.Payment.Confirm`; fulfillment retry dùng `Permissions.Order.Fulfill`. Command stale version trả `409 Conflict` và không tự retry.
- FE: Order detail chỉ render snapshot read-only, không có Edit/inline edit; action chỉ hiện theo capability và bị disabled khi state không hợp lệ.
- Test/bằng chứng: `OrderingIntegrationTests` kiểm tra catalog thay đổi không làm đổi snapshot và DB context từ chối sửa `Order.Total`/`OrderItem.Quantity`; targeted seed tests chứng minh đăng ký và replay data. Full provider/runtime/E2E verification còn phải chạy.

## 1B. Seed và chiến lược replay

- File dữ liệu: `src/Infrastructure/Persistence/Initialization/SeedData/ordering.json`.
- Seeder: `OrderingDevelopmentSeeder`, chạy sau `CatalogDevelopmentSeeder`; chỉ Development và chỉ khi `Modules:Catalog` bật.
- Scenario: prepaid pending, COD awaiting processing, prepaid needs review, completed, cancelled, expired, multi-line và timeline nhiều action.
- Replay: operation key ổn định từ scenario key; nếu Order đã có key thì bỏ qua, không ghi đè Order/OrderItem operator đã chỉnh.
- Actor/quyền: dùng actor development hiện có, kiểm đủ các capability trước khi ghi; không cấp thêm permission.
- Test: kiểm JSON được copy vào output, scenario coverage, DI registration/order và replay contract; database/provider replay cần chạy trong môi trường có database thật.

## 2. Contract API

| Endpoint | Hợp đồng | Quyền | Ghi chú |
|---|---|---|---|
| `GET /api/admin/orders` | `AdminOrderQuery` → `Page<OrderSummary>` | `Order.View` | Query cũ `page/size` vẫn hoạt động; search theo number/email/phone. |
| `GET /api/admin/orders/{id}` | `OrderDto` snapshot + state | `Order.View` | Read-only; không trả protected digital content. |
| `GET /api/admin/orders/{id}/history` | `OrderHistoryEntryDto[]` | `Order.View` | Audit trail lọc theo Order/OrderItem/payment/shipment/after-sales/renewal; chỉ trả safe summary. |
| `POST /api/admin/orders/{id}/cancel` | request có `Version` | `Order.Cancel` | State transition có kiểm soát. |
| `POST /api/admin/orders/{id}/payment-confirmations` | payment confirmation request | `Payment.Confirm` | Trả DTO mới cùng version mới. |
| `POST /api/admin/orders/{id}/retry-fulfillment` | request có `Version` | `Order.Fulfill` | Trả DTO mới; không optimistic mutation ở FE. |

List filter dùng đúng enum BE: `PendingPayment`, `AwaitingProcessing`, `Cancelled`, `Expired`, `Completed`, `Closed`; payment `Unpaid/Paid`; fulfillment `Pending/NeedsReview/InProgress/Completed/Stopped`. `CreatedFrom > CreatedTo` bị từ chối.

## 3. Kế hoạch triển khai và trạng thái

| Bước | Đầu ra | Trạng thái |
|---|---|---|
| 1 | Query list, summary và safe history contract | Đã triển khai |
| 2 | Controller wiring, DI history và permission | Đã triển khai |
| 3 | Development seed 8 scenario, operation key và replay guard | Đã triển khai source; chưa chạy full database replay |
| 4 | Unit/integration/provider verification | Targeted seed 3/3 và BE build pass; full DB/provider suite chưa chạy |
| 5 | OpenAPI/browser/runtime acceptance | Còn lại |

## 4. Kiểm chứng và giới hạn

Chi tiết lệnh/kết quả nằm ở [verification report](../verification/2026-10-21-admin-orders.md). Tóm tắt:

- `dotnet build Boilerate.sln --no-restore`: pass, 0 error; lượt compile đầy đủ vẫn phát ra analyzer warnings nền của solution, không phải lỗi build.
- Targeted `DevelopmentSeedingTests`: 3/3 pass.
- Chưa xác nhận bằng database provider thật, authenticated browser, API deployment, Excel/Desktop hoặc E2E. Không đánh dấu production/deployment acceptance từ build local.

## 5. Công việc còn lại

- Chạy seed trên database trống và database đã có operator edits bằng SQL Server/PostgreSQL; xác nhận chạy lại không duplicate và không overwrite.
- Bổ sung integration tests cho list filters/stable ordering, history authorization/safe projection, version conflict và snapshot mutation.
- Sinh/đối chiếu OpenAPI nếu pipeline repository bật tài liệu từ source.
- Chạy E2E với actor development cho list/detail/commands/409/permission và xác nhận route query/back-forward.
- Khi các gate trên đạt, cập nhật verification và ghi một milestone history duy nhất.
