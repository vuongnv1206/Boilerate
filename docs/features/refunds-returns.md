# Hoàn tiền và nhận hàng trả về — MVP4C

Bắt đầu/cập nhật: 2026-10-19, Asia/Saigon. Trạng thái: đã có code, đang kiểm chứng local. Tiếp nối [gia hạn4B](manual-renewals.md), [MVP](shop-mvp.md), [roadmap](../roadmap.md).

## 1. Mục tiêu và phạm vi

Nhân viên ghi nhận khoản tiền đã hoàn thực tế và hàng vật lý đã nhận lại. Không gọi ngân hàng/carrier trong transaction hoặc tự coi refund là đã nhận lại hàng. Đây là công cụ ghi nhận, không tự quyết điều kiện được đổi trả của shop.

| Trong lát này | Hoãn lại | Quyết định tối giản |
|---|---|---|
| Refund từng phần/toàn bộ sau receipt, cap đã thu; full refund đóng giao mới; return theo shipment với số lượng nhập kho | Gateway hoàn tự động, refund attempts/approval, RMA của khách, đổi hàng, phân bổ dòng giá/thuế | Nhân viên kiểm chứng tiền/hàng ở ngoài rồi ghi; reference chỉ là mã chứng từ, không chứa tài khoản/credential |
| Nhận cả hàng đã xuất chưa Delivered/COD chưa thu; tùy chọn đóng phần chưa xuất khi COD chưa thu đã nhận đủ phần đã xuất | Tự suy return từ tracking, thu hồi quyền số hoặc đảo gia hạn ngoài | Return/refund độc lập; số tiền VND nguyên đồng nhập có quyền, không tự tính từ quantity |

Tiêu chí local:

- [ ] Refund đúng receipt/order/currency, amount>0, tổng không vượt đã thu; retry/race không trùng/vượt cap.
- [ ] Full refund đóng fulfillment, nhả đúng phần chưa xuất/chưa giao; Paid giữ lịch sử đã thu. Partial refund không tự dừng giao.
- [ ] PendingVerification không bị refund mở khóa; Verify kết quả thật không hồi sinh Closed; Unique Delivered không quay về Available.
- [ ] Return đúng shipment/order/SKU, tổng không vượt đã xuất; restock0..Quantity, movement dương đúng balance gốc, nguyên tử.
- [ ] COD chưa thu chỉ đóng phần còn lại khi đã nhận đủ phần đã xuất và chọn rõ trong return cuối; không tạo tiền giả.
- [ ] Owner/ACL/no-store/audit, rollback/race/replay, migrations và regression cả SQL Server/PostgreSQL.

## 2. Thiết kế

### Domain/database

Payment sở hữu Refund; Shipping sở hữu PhysicalReturn/PhysicalReturnItem. Không tạo PaymentAttempt giả cho khoản hoàn nhân viên đã thực hiện.

| Quan hệ | Cardinality | FK/constraints |
|---|---|---|
| PaymentReceipt→Refund | 1–0..N | Refund(ReceiptId,OrderId,Currency)→receipt AK(Id,OrderId,Currency), Restrict |
| Order→PhysicalReturn | 1–0..N | FK OrderId Restrict; return AK(Id,OrderId) |
| PhysicalReturn→PhysicalReturnItem | 1–1..N ở domain | PK(ReturnId,ShipmentId,OrderItemId); FK(ReturnId,OrderId)→root AK |
| ShipmentItem→PhysicalReturnItem | 1–0..N | FK(ShipmentId,OrderItemId,OrderId,VariantId)→shipment item AK, đúng physical/order/SKU |
| StockMovement→PhysicalReturnItem | 1–0..1 tùy chọn | StockMovementId? FK, filtered UQ khi nonnull; null iff RestockQuantity=0 |

| Bảng/cột | Keys/check/index | Audit/concurrency |
|---|---|---|
| Payments.Refunds | Id GUID PK; ReceiptId/OrderId; OperationId UQ; RequestHash ASCII64; Source ASCII32 ManualBankTransfer/ManualCash; Reference/NormalizedReference ASCII100; UQ(Source,NormalizedReference); Amount decimal(18,2)>0 whole; Currency3='VND'; Reason1000; RefundedAtUtc; index(OrderId,RefundedAtUtc,Id) | Append-only CommerceEntity; Version32 inherited không là update contract |
| Shipping.PhysicalReturns | Id GUID PK; OrderId; OperationId UQ; RequestHash64; Reference/NormalizedReference ASCII100 UQ; Reason1000; ReceivedAtUtc; CloseRemainingCod bool; AK(Id,OrderId); index(OrderId,ReceivedAtUtc,Id) | Append-only CommerceEntity root, Version32; không draft/update/delete |
| Shipping.PhysicalReturnItems | ReturnId,ShipmentId,OrderItemId,OrderId,VariantId; Quantity1..999; RestockQuantity0..Quantity; StockMovementId? | Append-only link, audit metadata; FK/PK ở trên |

Order enum thêm Status.Closed6, FulfillmentStatus.Stopped5. Check Closed iff Stopped, Closed cần Paid hoặc CashOnDelivery; Completed vẫn cần Paid+Completed fulfillment. Không đổi Paid thành Unpaid khi hoàn. Tổng đã hoàn/khả năng hoàn còn lại tính từ ledger, không lưu nguồn chuẩn thứ hai trên Order. Full refund bao gồm phí ship nếu xác nhận đủ toàn bộ số đã thu.

Reservation thêm HoldState.Closed4: giữ Quantity/ConsumedQuantity, Remaining=0 khi Closed. CloseRemainder trả số chưa xuất trước khi đóng; balance chỉ giảm Reserved tương ứng. Released2 vẫn chỉ cancel/expiry chưa từng xuất; Reacquire từ Closed bị chặn. Check State4 cần ConsumedQuantity<Quantity. Không backfill/sửa lịch sử.

Transaction: ordering Gate→sorted ProductType/Product→balances; check Order.Version. SUM refunds<=receipt.Amount và SUM returns<=ShipmentItem.Quantity kiểm dưới Gate, không check constraint giả cho aggregate. Restock dùng balance của movement xuất gốc, tăng OnHand, không đổi Reserved/Consumed; caller không chọn balance/movement. Hàng không bán được dùng RestockQuantity0, vẫn có return history.

Full refund chuyển Closed/Stopped; nhả Held physical Remaining và chỉ Unique State=Reserved; claim renewal chưa Start release về Ready có history, PendingVerification giữ khóa tới Verify. Chặn dispatch/retry/provision/Claim/Start/SetTarget mới. Deliver/Verify vẫn ghi sự thật nhưng reconcile không hồi sinh Closed. Owner vẫn đọc lịch sử/nội dung đã giao/target. Bank payment đến sau COD Closed vẫn ghi Paid, giữ Closed/Stopped để xử lý refund; ManualCOD vẫn cần đủ Delivered.

Return.CloseRemainingCod=true chỉ khi COD Unpaid, có shipment và tổng nhận lại gồm lần này đủ mọi quantity đã xuất; đóng đơn/nhả phần chưa xuất cùng transaction. Flag=false không đóng; flag dành cho lần nhận cuối. Return được ghi sau full refund và cho Dispatched/Delivered, không sửa shipment/tự refund. Snapshot đơn/giao là lịch sử ban đầu, hậu mãi là ledger độc lập.

OperationId unique toàn bảng; hash actor/order/nội dung chuẩn hóa, bỏ Version. Replay đúng trả bản ghi cũ/trạng thái order hiện tại trước check Version; khác actor/payload/order conflict. Reference unique chặn chứng từ lặp bằng operation mới. API không chuyển tiền; mất response phải đối chiếu chứng từ/replay, không chuyển thêm do lỗi mạng.

SaveChanges chặn update/delete cả ba entity; mọi FK Restrict. Audit ghi metadata/chứng từ không nhạy cảm; không email/outbox mới. Migration thứ8 RefundsReturns hai provider, giữ bảy migrations cũ; mở check Order/reservation, thêm AK/FK/tables. Down guard trước DDL khi có hậu mãi/Closed hoặc history/holds bị các migration trước chặn. Không ETL/backfill nghiệp vụ; ưu tiên forward migration/restore.

### API/quyền/vận hành

| Endpoint | Input/response | Quyền |
|---|---|---|
| GET /api/orders/{orderId}/after-sales | Receipt/refund totals, Closed flag, refunds/returns metadata | Authenticated owner, sai owner404 |
| GET /api/admin/orders/{orderId}/after-sales | Cùng summary/history | AfterSales.View |
| POST /api/admin/orders/{orderId}/refunds | OrderVersion,OperationId,Reference,Amount,Currency,RefundedAtUtc,Reason,Source→RefundResult(Refund,OrderVersion,Closed) | Refund.Confirm; ghi đã hoàn, không chuyển tiền |
| POST /api/admin/orders/{orderId}/returns | OrderVersion,OperationId,Reference,Reason,CloseRemainingCod,Items[{ShipmentId,OrderItemId,Quantity,RestockQuantity}]→ReturnResult(Return,OrderVersion,Closed) | Return.Receive; restock>0 thêm Inventory.Adjust |

No-store; ACL controller/service, không suy từ Payment.Confirm/Shipping.Deliver. RefundedAtUtc UTC không tương lai/không trước receipt sau khi cùng chuẩn hóa microsecond (SQL Server receipt cũ có thể giữ tick100ns); ReceivedAtUtc dùng clock server microseconds. Kiểm amount nguyên đồng trước hash/replay, tránh làm tròn payload lẻ thành hash giống khoản hợp lệ. Đăng ký/cấp capability qua operator CLI; restart không auto-grant. Backup DB/key/media cùng recovery point; restore không đảo tiền/nhập hàng bên ngoài. Chưa nghiệm thu adapter/runtime thật.

### Quyết định

Ngày19 chọn ledger khoản đã hoàn thay attempt, giữ Paid; full refund dừng việc mới, partial không tự dừng/thu hồi nội dung; return riêng theo shipment. Đây là đề xuất triển khai trong phạm vi shop nhỏ được yêu cầu, không áp đặt thời hạn/lý do đủ điều kiện đổi trả.

## 3. Lộ trình từng bước

| Bước | Đầu ra | Trạng thái |
|---|---|---|
| 1 | Thiết kế trước code | Đã ghi |
| 2 | Domain/DB/migrations/guards/tests nền |32 domain đạt;6 migration đạt sau sửa setup/assertion test |
| 3 | Service/API/ACL/dừng fulfillment/return |32 integration đạt local trên hai DB; chuẩn bị commit riêng |
| 4 | Hai DB rollback/race/replay/privacy/regression | Targeted có kết quả, regression chung đang chạy |
| 5 | Docs/history/runbook/roadmap/commit nhỏ | Cập nhật song song |

## 4. Nhật ký/kiểm chứng

2026-10-19: đọc source/roadmap và review thiết kế; [history](../history/2026-10-19-04-refunds-returns-design.md) trước code. Chưa chạy tests lát này;398/398 renewal là bằng chứng lịch sử riêng.

2026-10-19: thêm domain/mappings/API, scaffold migration8 hai DB; [history nền](../history/2026-10-19-05-aftersales-domain-database.md), [báo cáo](../verification/2026-10-19-aftersales.md). Host/test builds đạt; targeted đầu86/90,4 lỗi chuẩn bị/assertion migration đã sửa, đang kiểm lại. Không coi32 integration pass là nghiệm thu tiền/return bên ngoài.

2026-10-19: build sau sửa và rerun migration6/6 đạt; không đổi production guards để né failures. Regression chung đang chạy; chưa đánh dấu toàn bộ lát được nghiệm thu.

## 5. Commit/còn lại

`935cd97`: thiết kế trước code. Các commit tiếp nối chỉ ghi hash sau khi tồn tại. Gateway/refund attempts, RMA frontend, đổi hàng và runtime/adapters ở lát riêng; không tự nghiệm thu toàn bộ MVP từ code này.
