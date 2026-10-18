# Nhận tiền và tự động giao hàng số

Ngày bắt đầu/cập nhật: 2026-10-18 (Asia/Saigon). Trạng thái: **đang triển khai**.
Mốc: bước 3 [Shop MVP](shop-mvp.md), [roadmap](../roadmap.md).

## 1. Mục tiêu và phạm vi

Nhân viên xác nhận khoản chuyển khoản thực tế đã nhận đủ cho một đơn; hệ thống cấp mã/tài khoản Unique hoặc snapshot link Shared cho khách trong cùng transaction. Khách đăng nhập xem nội dung ở đơn của mình, email chỉ thông báo. Thanh toán và giao hàng là hai trạng thái độc lập.

| Trong mốc này | Để sau | Giả định |
|---|---|---|
| Chuyển khoản xác nhận thủ công có ACL/audit; chống ghi nhận/cấp phát trùng; giữ lại toàn bộ hàng khi trả muộn; retry đơn đã thu nhưng thiếu hàng | Gateway/webhook tự động, đối soát ngân hàng, trả từng phần/thừa tiền, refund, Shipping, thao tác gia hạn ngoài hệ thống | Một shop, VND nguyên đồng, nhận đúng toàn bộ tổng snapshot của đơn; nhân viên kiểm tra ngân hàng trước khi xác nhận |
| Unique giao theo số lượng; Shared lưu một bản nội dung/line; Physical giữ reservation; Extension để chờ nhân viên | Không coi nhận tiền là đã giao vật lý hoặc đã gia hạn | Không có provider đã được chọn/nghiệm thu; không nhận callback công khai tự khai đã trả tiền |

Tiêu chí chấp nhận:

- [ ] Xác nhận đúng tiền với version/quyền; sai tiền, reference dùng lại hoặc operation đổi nội dung bị từ chối.
- [ ] Unique qty2 nhận hai nội dung, Shared qty2 nhận một nội dung; sửa link kho sau đó không đổi nội dung đã giao.
- [ ] Retry/cạnh tranh không thu hoặc cấp lần hai; hủy/expiry không nhả mã đã giao.
- [ ] Trả muộn giữ lại đủ toàn bộ thì giao; thiếu một món thì ghi nhận đã trả, chờ xử lý và không giữ/cấp một phần mới; staff retry sau bổ sung hàng.
- [ ] Khách chỉ đọc nội dung đơn của mình đã trả; ciphertext không lọt audit/outbox/email; đọc bí mật có audit, HTTP no-store.
- [ ] Email bền vững qua outbox, lỗi SMTP không đảo kết quả nhận tiền/giao số; kiểm chứng hai DB, phân biệt adapter thật.

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
| 1 | Chốt hồ sơ/quan hệ/nguồn nhận tiền | Tài liệu này, history, roadmap | Đã ghi thiết kế, chưa kiểm chứng code |
| 2 | Domain, persistence, API nhận tiền + giao số | Hai migrations; idempotency, late payment, ACL, owner reveal | Đã triển khai/kiểm chứng local48/48 core |
| 3 | Email outbox | Consumer retry được, email không mang secret | Chưa triển khai |
| 4 | Regression và review | Test domain/provider/race/rollback/HTTP/migration | Đang chạy Payment/core; regression toàn bộ sau email |
| 5 | Đồng bộ docs vận hành/lịch sử và commit nhỏ | Bằng chứng thật + phần còn mở | Cập nhật theo từng bước |

## 4. Nhật ký triển khai

2026-10-18 — Bước 1: đọc kế hoạch Ordering/kho số và chọn lát nhận tiền xác nhận thủ công. Chưa có code/test mới. Tiếp theo domain/persistence đồng thời với orchestration. [History](../history/2026-10-18-16-payment-delivery-design.md).

2026-10-18 — Bước 2: code domain/receipt/delivery/ACL/API và hai migrations; build Release 0 lỗi. Review bổ sung kind composite FK và xử lý crypto giữ Paid+NeedsReview. Bộ Payment/domain/provider/migration đang chạy. [History](../history/2026-10-18-17-payment-delivery-implementation.md).

## 5. Kiểm chứng và nghiệm thu

Core local48/48 đạt sau sửa assertion, xem [báo cáo từng lượt](../verification/2026-10-18-payment-delivery.md). Email/regression cuối còn đang làm. Linux/Docker/CI, ngân hàng/gateway và SMTP thật chưa nghiệm thu. Hạn chế: chưa xử lý thanh toán một phần, khoản chuyển thừa/trùng thực tế, hoàn tiền, shipping hay thao tác gia hạn; nhân viên cần đối chiếu thực tế trước ghi nhận.

## 6. Lịch sử commit

`5315fa4` — thiết kế nhận tiền và giao số trước code. Commit triển khai ghi khi tồn tại.

## 7. Còn lại

Hoàn tất các bước 2–5; provider tự động, manual renewal workflow, refund/shipping và nghiệm thu deployment thuộc mốc tiếp theo, không được suy ra từ trạng thái Paid.
