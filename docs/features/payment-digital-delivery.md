# Nhận tiền và tự động giao hàng số

Ngày bắt đầu/cập nhật: 2026-10-18 (Asia/Saigon). Trạng thái: **đã triển khai/kiểm chứng local; chờ nghiệm thu runtime/adapter thật**.
Mốc: bước 3 [Shop MVP](shop-mvp.md), [roadmap](../roadmap.md).

Cập nhật lát tiếp nối: [giao vật lý/COD](physical-shipping.md) bổ sung Shipment/ShipmentItem, xuất phần đã giữ và Source=ManualCOD sau khi COD đã giao đủ. Các cột/giới hạn và kết quả276/276 bên dưới mô tả lát payment ban đầu; hợp đồng hiện hành tổng thể ở [database](../commerce-database.md) và [API](../commerce-api.md).

## 1. Mục tiêu và phạm vi

Nhân viên xác nhận khoản chuyển khoản thực tế đã nhận đủ cho một đơn; hệ thống cấp mã/tài khoản Unique hoặc snapshot link Shared cho khách trong cùng transaction. Khách đăng nhập xem nội dung ở đơn của mình, email chỉ thông báo. Thanh toán và giao hàng là hai trạng thái độc lập.

| Trong mốc này | Để sau | Giả định |
|---|---|---|
| Chuyển khoản xác nhận thủ công có ACL/audit; chống ghi nhận/cấp phát trùng; giữ lại toàn bộ hàng khi trả muộn; retry đơn đã thu nhưng thiếu hàng | Gateway/webhook tự động, đối soát ngân hàng, trả từng phần/thừa tiền, refund, Shipping, thao tác gia hạn ngoài hệ thống | Một shop, VND nguyên đồng, nhận đúng toàn bộ tổng snapshot của đơn; nhân viên kiểm tra ngân hàng trước khi xác nhận |
| Unique giao theo số lượng; Shared lưu một bản nội dung/line; Physical giữ reservation; Extension để chờ nhân viên | Không coi nhận tiền là đã giao vật lý hoặc đã gia hạn | Không có provider đã được chọn/nghiệm thu; không nhận callback công khai tự khai đã trả tiền |

Tiêu chí chấp nhận:

- [x] Xác nhận đúng tiền với version/quyền; sai tiền, reference dùng lại hoặc operation đổi nội dung bị từ chối.
- [x] Unique qty2 nhận hai nội dung, Shared qty2 nhận một nội dung; sửa link kho sau đó không đổi nội dung đã giao.
- [x] Retry/cạnh tranh không thu hoặc cấp lần hai; hủy/expiry không nhả mã đã giao.
- [x] Trả muộn giữ lại đủ toàn bộ thì giao; thiếu một món thì ghi nhận đã trả, chờ xử lý và không giữ/cấp một phần mới; staff retry sau bổ sung hàng.
- [x] Khách chỉ đọc nội dung đơn của mình đã trả; ciphertext không lọt audit/outbox/email; đọc bí mật có audit, HTTP no-store.
- [x] Email bền vững qua outbox, lỗi SMTP không đảo kết quả nhận tiền/giao số; kiểm chứng hai DB, phân biệt adapter thật (SMTP mock, chưa nghiệm thu thật).

## 2. Thiết kế

### Domain và database

Payment sở hữu sổ khoản đã xác nhận, Ordering sở hữu snapshot giao số. Bản này không tạo PaymentAttempt giả khi chưa có giao dịch thực tế; attempt/gateway và refund bổ sung ở mốc riêng.

| Quan hệ | Cardinality | Cách lưu/ràng buộc/xóa |
|---|---|---|
| Order → PaymentReceipt | 1–0..1 trong mốc nhận đủ một lần | `Payments.PaymentReceipts.OrderId` FK Restrict + UQ; không phải giới hạn cho attempts tương lai |
| OrderItem → DigitalDelivery | 1–N (Unique: Quantity; Shared: một) | `Ordering.DigitalDeliveries`; UQ `(OrderItemId,Sequence)`; composite FK `(OrderItemId,OrderId,VariantId)` cùng snapshot |
| DigitalStockItem → DigitalDelivery | Unique 1–0..1; Shared 1–N | composite FK `(StockItemId,VariantId)`; filtered UQ StockItemId khi FulfillmentKind=2; Restrict |

| Bảng/cột | Kiểu và ràng buộc | Ownership/concurrency |
|---|---|---|
| PaymentReceipts | Id GUID PK; OrderId, OperationId GUID; RequestHash ASCII64; Source ASCII32=`ManualBankTransfer`; Reference/NormalizedReference ASCII100; Amount decimal(18,2), Currency3=`VND`; ReceivedAtUtc UTC; audit và Version32 | Append-only; UQ OperationId, UQ(Source,NormalizedReference), UQ OrderId; tiền không âm/nguyên đồng; amount đúng Order.Total kiểm tra domain/service trong transaction |
| DigitalDeliveries | Id GUID PK; OrderId/OrderItemId/VariantId/StockItemId GUID; Sequence int1..999; FulfillmentKind 2/3; ProtectedContent max16000; audit và Version32 | Append-only; nội dung được bảo vệ bằng Data Protection purpose riêng gắn DeliveryId; không lưu plaintext; qty/kind đối chiếu order và stock trong transaction |
| Orders | PaymentStatus Unpaid1/Paid2; FulfillmentStatus Pending1/NeedsReview2/InProgress3/Completed4; PaidAtUtc nullable; Status thêm Completed5 | Version của Order bảo vệ admin write; chỉ Paid có PaidAtUtc; Completed yêu cầu Paid và hoàn tất fulfillment |
| DigitalStockItems | State thêm Delivered4; giữ ReservedForOrderItemId vĩnh viễn khi đã giao Unique | Không enable/release/delete mã đã giao; chỉ Available/Disabled được không có owner |

PK/FK/index giữ kiểu GUID hiện hữu; FK không cascade sang lịch sử. Cả hai composite FK của delivery và alternate key đích còn chứa `FulfillmentKind`, chặn gán Unique thành Shared để vượt unique index. Receipt/delivery không update/delete qua SaveChanges. Audit chỉ metadata, không ciphertext/contact; đọc nội dung có TrailType.Read. Toàn bộ nội dung được giải mã/mã hóa snapshot trước khi thay đổi giữ hàng; lỗi crypto giữ Paid + NeedsReview và ghi metadata `DigitalContentUnavailable`, không cấp/giữ thêm một phần.

Thứ tự khóa: commerce ordering gate → ProductTypes → Products → balance MAIN theo ID. Nhận tiền/đơn/giữ hàng/giao số/audit commit cùng UoW; không SMTP/storage/network trong transaction. Sau khóa, preflight toàn bộ availability trước thay đổi giữ hàng; released physical hold được tái sử dụng, Unique đã giữ đúng owner tiếp tục dùng. Đơn Cancelled nhận tiền chỉ chờ xử lý, không tự hồi sinh. Expired hoặc pending quá hạn có thể tiếp tục nếu đủ; snapshot giá không tính lại theo Catalog mới. Sản phẩm archive không xóa cam kết đã đặt; cấp từ SKU/kho hiện hữu, Shared phải còn Available. Thiếu hàng không rollback khoản tiền đã được xác nhận. Retry fulfillment riêng chỉ dành cho Paid+NeedsReview, không được xử lý đơn Cancelled.

Migrations mới `PaymentDigitalDelivery` cho SQL Server/PostgreSQL, không sửa migrations cũ. Down từ chối khi có receipt/delivery/Unique đã giao; backup và restore đồng bộ DB + Data Protection keys. Theo chính sách repo dùng database mới; không ETL API cũ.

### API, quyền và vận hành

| Use case/endpoint dự kiến | Hợp đồng | Quyền/lỗi |
|---|---|---|
| POST `/api/admin/orders/{id}/payment-confirmations` | Version, OperationId, Reference, Amount, Currency, ReceivedAtUtc → OrderDto | `Payment.Confirm` ở controller và service; replay cùng operation trả kết quả, đổi payload conflict; received UTC không tương lai |
| POST `/api/admin/orders/{id}/retry-fulfillment` | Version → OrderDto | `Order.Fulfill`; đã trả và NeedsReview; đủ hàng mới giao |
| GET `/api/orders/{id}/digital-deliveries` | Delivery ID/item/sequence/content, không trả secret trong OrderDto | authenticated owner only, Paid; no-store, audit đọc; đơn người khác 404 |
| Order GET/list | thêm PaymentStatus/FulfillmentStatus/PaidAtUtc | vẫn owner hoặc admin Order.View; không chứa nội dung hàng số |

Email: `DigitalDeliveryReady` outbox v1, payload chỉ OrderId; worker tải email/number từ Order, gửi thông báo xem Đơn hàng của tôi, không gửi mã/mật khẩu. SMTP chạy ngoài transaction, retry/dead-letter/replay dùng worker hiện có. SMTP at-least-once nên crash sau gửi trước ack có thể lặp thông báo, không cấp lại hàng. Không đánh dấu SMTP hoặc ngân hàng/gateway đã nghiệm thu chỉ vì test mock/build pass.

### Nhật ký quyết định

| Ngày | Quyết định | Lý do |
|---|---|---|
| 2026-10-18 | Bắt đầu bằng chuyển khoản nhân viên xác nhận; exact full amount | Hoàn thiện luồng shop nhỏ khi chưa chọn provider, giữ nguồn xác nhận có quyền và audit |
| 2026-10-18 | Receipt và delivery append-only, snapshot mã hóa | Không giao lại hoặc thay lịch sử khi nội dung kho Shared đổi |

## 3. Kế hoạch từng bước

| Bước | Công việc | Đầu ra | Trạng thái |
|---|---|---|---|
| 1 | Chốt hồ sơ/quan hệ/nguồn nhận tiền | Tài liệu này, history, roadmap | Hoàn tất hồ sơ trước code |
| 2 | Domain, persistence, API nhận tiền + giao số | Hai migrations; idempotency, late payment, ACL, owner reveal | Đã triển khai/kiểm chứng local48/48 core |
| 3 | Email outbox | Consumer retry được, email không mang secret | Đã triển khai/kiểm local8/8 email +2 producer rollback |
| 4 | Regression và review | Test domain/provider/race/rollback/HTTP/migration | Regression276/276 đạt; UTC2/2, chưa nghiệm thu runtime/adapter |
| 5 | Đồng bộ docs vận hành/lịch sử và commit nhỏ | Bằng chứng thật + phần còn mở | Đã đồng bộ docs/history/runbook; commits theo lát |

## 4. Nhật ký triển khai

2026-10-18 — Bước 1: đọc kế hoạch Ordering/kho số và chọn lát nhận tiền xác nhận thủ công. Chưa có code/test mới. Tiếp theo domain/persistence đồng thời với orchestration. [History](../history/2026-10-18-16-payment-delivery-design.md).

2026-10-18 — Bước 2: code domain/receipt/delivery/ACL/API và hai migrations; build Release 0 lỗi. Review bổ sung kind composite FK và xử lý crypto giữ Paid+NeedsReview. Bộ Payment/domain/provider/migration đang chạy. [History](../history/2026-10-18-17-payment-delivery-implementation.md).

2026-10-18 — Bước 3: sau core48/48 và commit `533b681`, nối producer email trong transaction và consumer ngoài transaction. Test SMTP mock/lease/retry/replay đang chờ chạy; chưa nghiệm thu SMTP thật. [History](../history/2026-10-18-18-digital-delivery-email.md).

2026-10-18 — Bước 4–5: build cuối0 lỗi, regression276/276 đạt, gồm rollback sau EF flush trước commit và email retry/replay/lease. Đồng bộ docs và chia commit theo core/email; fix timestamp UTC phát hiện khi review được tách commit kế tiếp, cùng kết quả test đã ghi trong báo cáo.

2026-10-18 — Review bổ sung: [fix timestamp UTC](../history/2026-10-18-19-order-timestamp-utc.md) cho PaidAtUtc/ExpiresAt và CreatedOn của danh sách. SQL Server datetime2 trả Unspecified được chuẩn hóa Kind=Utc, giữ nguyên instant và JSON có `Z` như PostgreSQL. Hai ca roundtrip đã đạt trong regression276/276; không đổi DB hoặc lịch sử.

## 5. Kiểm chứng và nghiệm thu

Core local48/48 đạt sau sửa assertion; regression cuối276/276 đạt, xem [báo cáo từng lượt](../verification/2026-10-18-payment-delivery.md). Linux/Docker/CI, ngân hàng/gateway và SMTP thật chưa nghiệm thu. Hạn chế: chưa xử lý thanh toán một phần, khoản chuyển thừa/trùng thực tế, hoàn tiền, shipping hay thao tác gia hạn; nhân viên cần đối chiếu thực tế trước ghi nhận.

## 6. Lịch sử commit

`5315fa4` — thiết kế nhận tiền và giao số trước code. `533b681` — core nhận tiền/giao số và migrations. `b7afe1a` — email outbox và kiểm chứng rollback. Commit fix UTC tra từ history bước19; không ghi hash dự đoán.

## 7. Còn lại

Phần code/local của lát payment đã hoàn tất. Giao vật lý/COD được triển khai riêng ở [lát shipping](physical-shipping.md); provider tự động, manual renewal workflow, refund/returns và nghiệm thu deployment/SMTP/carrier thật vẫn thuộc mốc tiếp theo, không được suy ra từ trạng thái Paid hoặc test mock.
