# Giao hàng vật lý và ghi nhận tiền COD

Ngày bắt đầu/cập nhật: 2026-10-18, Asia/Saigon. Trạng thái: **đã triển khai/kiểm chứng local326/326; chưa nghiệm thu deployment/carrier thật**.
Mốc: bước 4A [Shop MVP](shop-mvp.md), [roadmap](../roadmap.md).

## 1. Mục tiêu và phạm vi

Nhân viên lập lần xuất giao, chọn số lượng từng dòng vật lý, lưu đơn vị vận chuyển/mã theo dõi rồi xác nhận giao thành công. Shop có thể giao nhiều lần trong cùng đơn. Tồn được xuất đúng một lần từ phần đã giữ; khách xem tiến độ riêng. COD có bản ghi tiền thực nhận, không tự Paid khi đổi trạng thái giao.

| Trong lát này | Hoãn lại | Giả định |
|---|---|---|
| Dispatch trực tiếp, nhiều shipment/đơn, xác nhận delivered; ACL/version/idempotency/audit; chuyển reservation thành stock movement; thu đủ COD thủ công | Nháp đóng gói, hủy chuyến đã xuất, trả hàng/nhập bù/hoàn tiền, carrier API/webhook, workflow gia hạn | Một shop, MAIN, VND; nhân viên cập nhật theo kết quả thực tế, không gọi carrier ngoài trong transaction |

Tiêu chí chấp nhận:

- [x] Order 1–N Shipment; Shipment N–N OrderItem có số lượng, chỉ dòng Physical của đúng Order/SKU.
- [x] Dispatch trừ đồng thời OnHand và Reserved, thêm movement âm; retry/race không xuất hai lần hoặc vượt số đã mua.
- [x] Giao từng phần giữ phần còn lại; không hủy đơn đã xuất qua API nhả hàng cũ; không tự đưa hàng đã xuất về Available.
- [x] Prepaid phải Paid, không NeedsReview; COD có thể xuất trước trả; đã giao hết mà chưa thu vẫn Unpaid, chỉ Completed Order sau đủ tiền và hoàn tất mọi món.
- [x] Đơn chung không cấp lại mã số; ManualExtension không được đánh dấu hoàn tất từ giao vật lý/nhận COD.
- [x] Quyền Shipping.View/Dispatch/Deliver và Payment.Confirm ở controller/service; khách chỉ đọc shipment đơn của mình, no-store; mọi timestamp UTC.
- [x] Hai provider có migrations mới, PK/FK/check/unique và rollback/idempotency/HTTP được kiểm chứng; adapter thật ghi trạng thái riêng.

## 2. Thiết kế

### Domain và database

Shipping sở hữu Shipment và ShipmentItem. Inventory sở hữu balance/movement; Ordering giữ snapshot OrderItem và PhysicalStockReservation. Không dùng cascade để xóa lịch sử.

| Quan hệ | Cardinality | FK/unique/xóa |
|---|---|---|
| Order → Shipment | 1–0..N; Shipment bắt buộc có Order | Shipments.OrderId FK Restrict, index(OrderId,DispatchedAtUtc,Id), AK(Id,OrderId) |
| Shipment ↔ OrderItem | N–N qua ShipmentItems.Quantity | PK(ShipmentId,OrderItemId); composite FK (ShipmentId,OrderId) và (OrderItemId,OrderId,VariantId,FulfillmentKind) → đúng OrderItem; Kind=Physical1; Restrict |
| ShipmentItem → StockMovement | 1–1 | ShipmentItems.StockMovementId FK + UQ; mỗi lần xuất có movement riêng; Restrict |
| OrderItem → PhysicalStockReservation | Giữ 1–0..1 | Thêm ConsumedQuantity; lịch sử Quantity ban đầu không đổi, Remaining=Quantity-ConsumedQuantity; chống tái xuất qua domain/transaction |

| Bảng/cột | Kiểu/ràng buộc | Concurrency và lý do |
|---|---|---|
| Shipping.Shipments | Id GUID PK, OrderId GUID; OperationId GUID UQ; RequestHash ASCII64; Carrier100, TrackingNumber100; Status Dispatched1/Delivered2; DispatchedAtUtc UTC, DeliveredAtUtc?; DeliveryOperationId? GUID filtered UQ; audit/Version32 | Immutable request hash từ OrderId + carrier/tracking + item/qty đã sort, loại version; retry đúng trả kết quả cũ, đổi nội dung conflict |
| Shipping.ShipmentItems | ShipmentId/OrderId/OrderItemId/VariantId/StockMovementId GUID; FulfillmentKind=1; Quantity1..999 | Append-only, không update/delete; cùng parent/order/kind được FK/check bảo vệ; tổng Quantity qua mọi shipment <= OrderItem.Quantity cần khóa+domain |
| Ordering.PhysicalStockReservations | ConsumedQuantity int default0; State thêm Consumed3 | CHECK 0<=Consumed<=Quantity; Held1 còn phần chưa xuất, Released2 chỉ khi Consumed=0, Consumed3 khi hết; Remaining not mapped |
| Ordering.Orders | PhysicalDispatchStarted bool defaultfalse | Order.Version bảo vệ shipment/COD write và chống nhả/hủy sau khi đã xuất |
| Payments.PaymentReceipts | Source hỗ trợ thêm ManualCOD | UQ(Source,NormalizedReference) giữ nguyên; ManualCOD chỉ Order COD thuần vật lý đã giao hết, đúng đủ Total; một receipt/đơn giữ nguyên |

ShipmentItem.StockMovementId tham chiếu ledger bất biến. Domain InventoryItem.ConsumeReserved tạo movement âm, giảm OnHand/Reserved trong cùng thao tác; không gọi Adjust để lách số đã giữ. Reservation.Consume tăng số đã xuất, không Release/Reacquire sau đã xuất. Order ghi nhận dispatch/fulfillment, chặn Cancel khi đã xuất. DTO không mang nội dung hàng số.

Thứ tự khóa vẫn ordering gate → ProductTypes → Products → InventoryItems theo ID. Lấy reservation/balance dưới khóa; mọi dòng phải hợp lệ trước commit; rollback bao gồm shipment, movement, balance, reservation, order và audit. Không I/O ngoài hoặc SMTP trong transaction. Payment xác nhận sau khi COD đã xuất không chạy lại luồng giữ toàn bộ tồn, chỉ tính hoàn tất từ các shipment hiện có; tránh giữ lại hàng đã bán.

Shipment chỉ Dispatched/Delivered: endpoint tạo là xuất giao thật, không phải nháp. Không được sửa/xóa lịch sử hoặc đổi số lượng đã xuất. DeliveryOperationId là mã retry riêng; lặp operation đúng shipment/state trả kết quả cũ, dùng cho shipment khác conflict; version cũ với operation mới conflict. Đơn đã giao hết vật lý chỉ hoàn tất fulfillment nếu các món số đã có đủ snapshot và không còn ManualExtension. COD chưa thu có thể FulfillmentCompleted nhưng Order vẫn AwaitingProcessing; sau receipt đầy đủ mới Order.Completed. Đơn mixed phải Paid trước dispatch.

Migration mới `PhysicalShipping` trên SQL Server/PostgreSQL, không sửa năm migrations cũ. Existing reservations backfill ConsumedQuantity0; Orders PhysicalDispatchStartedfalse. Down chặn shipment, consumed stock hoặc ManualCOD receipt trước DDL để không mất lịch sử; guard còn chặn receipts/digital deliveries/Paid/active holds trước đó để downgrade cả chuỗi không gỡ một phần rồi thất bại ở migration cũ. Ngay cả receipt không liên quan shipping cũng có thể chặn rollback; ưu tiên forward migration/restore backup. Theo chính sách baseline repo, không ETL/compatibility API cũ. Backup toàn DB + key/media theo runbook.

### API, quyền và vận hành

| Endpoint/use case | Hợp đồng | Quyền/phạm vi |
|---|---|---|
| POST /api/admin/orders/{id}/shipments | OrderVersion, OperationId, Carrier, TrackingNumber, Items[{OrderItemId,Quantity}] → ShipmentDto | Shipping.Dispatch, cùng service; Paid hoặc COD, không Cancelled/Expired/NeedsReview |
| POST /api/admin/orders/{id}/shipments/{shipmentId}/deliver | OrderVersion, Version, OperationId → ShipmentDto | Shipping.Deliver; server timestamp; không tự xác nhận tiền |
| GET /api/admin/orders/{id}/shipments | List ShipmentDto/Items | Shipping.View |
| GET /api/orders/{id}/shipments | List ShipmentDto/Items | Authenticated owner, đơn khác404; no-store |
| POST payment-confirmations hiện hữu | Thêm Source optional, mặc định ManualBankTransfer; ManualCOD là lựa chọn mới | Payment.Confirm; ManualCOD chỉ COD đã giao đủ; full amount/currency/idempotency giữ nguyên |

ShipmentDto gồm Id, OrderId, OrderVersion, Version, Carrier, TrackingNumber, Status, DispatchedAtUtc/DeliveredAtUtc và Items(OrderItemId,Quantity); không expose movement nội bộ hoặc secret. Timestamp shipment chuẩn hóa UTC microsecond trước khi trả response đầu để replay/đọc DB không khác độ chính xác giữa hai provider. Bank receipt mặc định giữ hash tương thích các request cũ; ManualCOD hash chứa Source để không replay khác nguồn. Chưa có carrier adapter mới để nghiệm thu.

### Nhật ký quyết định

| Ngày | Quyết định | Lý do |
|---|---|---|
| 2026-10-18 | Chọn dispatch trực tiếp, giữ mô hình partial shipment | Luồng shop nhỏ gọn nhưng không ép một Order chỉ một lần giao |
| 2026-10-18 | COD thu đủ sau khi giao toàn bộ, tách với delivered | Không suy luận tiền từ vận chuyển; partial collection/refund để lát riêng |

## 3. Kế hoạch từng bước

| Bước | Công việc | Đầu ra | Trạng thái |
|---|---|---|---|
| 1 | Chốt thiết kế, docs/backend/history | Quan hệ/API/quy tắc này | Đã ghi trước code |
| 2 | Domain + persistence + migrations + domain tests | State/ledger/constraints; build được | Đạt; domain24/24, shipping migration4/4 local |
| 3 | Service/API/ACL + integration | Dispatch/delivered/owner/COD end-to-end | API và22 integration cases đạt trên hai DB |
| 4 | Review/regression hai DB | Race/rollback/replay/permission/migration | Sau sửa precision, regression326/326 đạt; giữ bằng chứng lượt đầu fail |
| 5 | Đồng bộ docs/bằng chứng/commit | Các lát coherent, không khẳng định deployment | Đã đồng bộ docs/backend/history/runbook; commit theo thiết kế/domain/API |

## 4. Nhật ký triển khai

2026-10-18 — đọc roadmap/MVP/payment và code hiện tại; lập thiết kế trước triển khai. Chưa chạy test mới. [History bước20](../history/2026-10-18-20-physical-shipping-design.md).

2026-10-18 — model/mappings/domain tests đã viết; service bổ sung dispatch/delivered/COD và tránh re-reserve hàng COD đã xuất. Chưa kiểm chứng; [history bước21](../history/2026-10-18-21-shipping-domain-database.md).

2026-10-18 — Host build0 lỗi/1690 warnings; integration22 cases đã viết, migrations đang scaffold. [History bước22](../history/2026-10-18-22-shipping-api-cod.md) ghi ACL/idempotency/COD và chặn retry full-stock sau dispatch; chưa coi tests đã đạt.

2026-10-18 — scaffold MSSQL20261018151738/PostgreSQL20261018151746 đạt; build tests0 lỗi/2192 warnings. Bộ targeted shipping/baseline/ordering-payment migrations đang chạy; [báo cáo](../verification/2026-10-18-physical-shipping.md).

2026-10-18 — targeted62/62 đạt (domain23, integration22, shipping migration4 và baseline/ordering/payment regression phù hợp); bắt đầu regression toàn bộ trừ Container/Recovery/Template. Chưa có carrier/SMTP/gateway thật được nghiệm thu.

2026-10-18 — regression đầu phát hiện precision timestamp PostgreSQL khác response đầu; [history bước23](../history/2026-10-18-23-shipment-timestamp-precision.md) ghi kế hoạch sửa trước code. Domain chuẩn hóa microsecond, thêm ca ticks cố định và clock replay tái hiện, không nới assertion. Chờ lượt cũ kết thúc để build/test lại.

2026-10-18 — build sau sửa0 lỗi/2806 warnings,3/3 kiểm precision và regression326/326 đạt. Hoàn tất local phần4A; đồng bộ hợp đồng/roadmap/history, tách commit thiết kế/domain/API. Gia hạn/refund và deployment/carrier thật vẫn mở.

## 5. Kiểm chứng và nghiệm thu

Build/scaffold, targeted62/62, precision3/3 và regression cuối326/326 đã đạt; lượt đầu324/325 và nguyên nhân/sửa được giữ trong [báo cáo](../verification/2026-10-18-physical-shipping.md). Không cộng test trùng giữa các lượt. Chưa có carrier tự động/SMTP/gateway/Linux deployment được nghiệm thu từ thay đổi này.

## 6. Lịch sử commit

`27d9e2b` — thiết kế shipping/COD trước code. `39e8d0e` — domain/persistence/migrations và kiểm chứng targeted. API/ACL/COD và fix precision được chốt cùng docs/bằng chứng sau regression326/326; hash tra bằng `git log --follow -- docs/backend/history/2026-10-18-22-shipping-api-cod.md`, không ghi hash dự đoán.

## 7. Còn lại

Gia hạn có claim/verification/history, refund/returns và các gate deployment/adapter ở mốc tiếp nối. Shipping này không tự đóng toàn bộ MVP bước4.
