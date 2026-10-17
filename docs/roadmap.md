# Lộ trình backend shop

Cập nhật: 2026-10-17. Lộ trình được cập nhật cùng code và [nhật ký phát triển](history/README.md). Trạng thái dưới đây mô tả bằng chứng hiện có; không coi cấu hình CI hoặc test adapter giả là nghiệm thu môi trường thật.

## Phạm vi nền tảng

Một shop, hàng vật lý, giá VND nguyên đồng, một kho MAIN không bán âm. Dùng modular monolith, .NET 10/MediatR/EF Core, một database có schema nghiệp vụ; hỗ trợ SQL Server và PostgreSQL. DDD tập trung tại Product, ProductType và InventoryItem; handler/service điều phối quyền và transaction.

Product thuộc đúng một ProductType và có 0–N Category qua bảng nối N–N. Category phục vụ phân loại, ProductType quyết định bộ trường. Chi tiết tại [thiết kế database](commerce-database.md).

Tiến độ từng bước của mốc đầu nằm tại [hồ sơ Catalog v1](features/catalog-v1.md).

## Các mốc và điều kiện hoàn tất

| Mốc | Đầu ra | Trạng thái hiện tại | Điều kiện còn lại |
|---|---|---|---|
| 0. Foundation | Identity, Function–Action ACL, sessions, audit, migration/bootstrap/preflight | Có triển khai và kiểm thử trong repo | Giữ regression khi bổ sung capability/use case |
| 1. Chốt thiết kế commerce | Cardinality, từ điển bảng/cột, FK/unique/check/index, transaction/xóa, API | Đã có [database](commerce-database.md) và [API](commerce-api.md) | Cập nhật cùng mọi thay đổi schema/hành vi tiếp theo |
| 2. Runtime triển khai | Linux CI, Docker non-root, volume media/keys, health, proxy/restart, migration riêng | Code và cổng kiểm tra CI đã có; **chưa nghiệm thu Linux/Docker/CI thật trong môi trường hiện tại** | Docker daemon hoạt động; chạy container trên cả hai DB; lưu run/log kết quả |
| 3. Transaction/media/outbox | Media lifecycle, DB outbox, lease/retry/dead-letter/replay và cleanup | Đã triển khai; có regression trên hai DB | Nghiệm thu container/volume/restart thật; đo tải sweeper khi backlog lớn |
| 4. Catalog/giá/publication | ProductType/typed fields, Category N–N, options/SKU, giá, Draft/Published/Archived | Đã triển khai; có kiểm thử domain/API/database | Duy trì kiểm chứng hai provider khi mở rộng; xem giới hạn trong API docs |
| 5. Inventory/public API | Balance MAIN, movement bất biến/idempotency, typed filters, Published-only/media | Đã triển khai; có kiểm thử concurrency và quyền | Chưa có reservation; bổ sung cùng checkout ở mốc sau |
| 6. Hoàn thiện và nghiệm thu mốc đầu | Review/regression, template đổi tên, module flag, OpenAPI, vận hành/backup | Review và một phần kiểm chứng đã có | Đóng các mục runtime/adapter còn mở; thực hành backup/restore và ghi bằng chứng |
| 7. Ordering/checkout | Cart, checkout, Order/OrderItem snapshot, reservation/idempotency | **Chưa triển khai; chưa tạo bảng** | Chốt hợp đồng và luồng giữ/nhả hàng, thiết kế rồi mới tạo migrations |
| 8. Payment | Attempts/refunds, webhook bền vững và deduplication, đối soát | **Chưa triển khai; chưa chọn provider** | Chốt provider và state machine; kiểm thử sandbox/thật theo phạm vi cho phép |
| 9. Shipping | Shipment/ShipmentItem có số lượng, giao từng phần, trạng thái riêng | **Chưa triển khai; chưa chọn provider** | Chốt carrier, callback/idempotency và cách đồng bộ với Order |

Các mốc 2–6 có phần triển khai song song. Tồn tại code ở mốc sau không làm các điều kiện nghiệm thu của mốc trước tự hoàn tất.

## Việc ưu tiên tiếp theo

1. **Nghiệm thu runtime:** chạy Linux CI và container non-root trên SQL Server/PostgreSQL; kiểm tra volume media/Data Protection keys, health, proxy tin cậy và restart. Ghi rõ image, môi trường, lệnh và kết quả.
2. **Vận hành và khôi phục:** thực hành backup/restore DB cùng media/keys trên môi trường thử; kiểm tra outbox sau khôi phục, retry/dead-letter/replay và quyền operator.
3. **Adapter đang bật:** nghiệm thu riêng các adapter cần dùng thực tế. SMTP/cloud storage/Redis/OAuth/Hangfire ngoài process chỉ được xác nhận khi có cấu hình và bằng chứng chạy tương ứng.
4. **Checkout:** viết hồ sơ tính năng theo [mẫu](templates/feature-plan.md), chốt reservation, giá snapshot và ranh giới Order/Payment/Shipment trước khi triển khai.

Docker daemon chưa hoạt động là giới hạn của lần nghiệm thu hiện tại; cần kiểm tra lại điều kiện đó khi tiếp tục, không coi đây là trạng thái cố định của dự án.

## Hướng database cho các mốc sau

| Quan hệ | Cardinality/hợp đồng dự kiến |
|---|---|
| Order → OrderItem | 1–N; giữ snapshot tên/SKU/giá/thông tin lúc mua |
| Order → PaymentAttempt | 1–N; cho phép thanh toán lại |
| PaymentAttempt → Refund | 1–N; tổng hoàn không vượt số đã thu |
| Order → Shipment | 1–N; chừa khả năng giao từng phần |
| Shipment ↔ OrderItem | N–N qua ShipmentItem có số lượng |

Đây là hướng thiết kế đã chốt cho bước lập kế hoạch tiếp theo, chưa phải schema đã tồn tại. Payment sở hữu attempts/refunds; Order, Payment và Shipment có trạng thái riêng. Reservation cần công thức `Available = OnHand - Reserved` cùng quy tắc hết hạn/nhả hàng/idempotency được thiết kế riêng.

## Cách theo dõi từ đây

Mỗi tính năng tạo hồ sơ trong `docs/features/` theo [quy trình](development-workflow.md), cập nhật bước đang làm và bằng chứng ngay khi có. [Nhật ký](history/README.md) lưu quyết định, kết quả và commit thực tế. Tài liệu hợp đồng hiện hành giữ mô tả cuối cùng; nhật ký giữ lý do và trình tự thay đổi.

Các lần kiểm chứng commerce trước đây được lưu tại [báo cáo kiểm chứng](verification/2026-10-17-commerce.md) và [review ngày 2026-10-17](commerce-review.md); raw evidence cục bộ tại `artifacts/commerce-review/report.md` khi còn trong workspace. Lần kiểm chứng mới phải ghi kết quả riêng, không thay thế âm thầm kết quả cũ.
