# Lộ trình backend shop

Cập nhật: 2026-10-18. Lộ trình được cập nhật cùng code và [nhật ký phát triển](history/README.md). Trạng thái dưới đây mô tả bằng chứng hiện có; không coi cấu hình CI hoặc test adapter giả là nghiệm thu môi trường thật.

## Phạm vi nền tảng

Code Catalog v1 hiện có phục vụ một shop, hàng vật lý, giá VND nguyên đồng, một kho MAIN không bán âm. Dùng modular monolith, .NET 10/MediatR/EF Core, một database có schema nghiệp vụ; hỗ trợ SQL Server và PostgreSQL. DDD tập trung tại Product, ProductType và InventoryItem; handler/service điều phối quyền và transaction.

Mục tiêu phát triển vừa được người dùng xác nhận: **shop tự cấu hình loại sản phẩm, trường thông tin, options/SKU và chọn cách bán/giao được hỗ trợ**, tham khảo trải nghiệm Webflow. Quần áo, điện thoại, laptop, PC, khóa học và tài khoản là ví dụ kiểm tra độ linh hoạt; PC đã được làm rõ chỉ là ý tưởng, chưa yêu cầu nghiệp vụ build/tồn linh kiện ở MVP. **Mỗi shop dùng một installation riêng, không yêu cầu multi-tenant**. Xem [phạm vi linh hoạt](business/product-flexibility.md) và [business discovery](features/business-discovery.md); chưa coi mục tiêu mới là đã được Catalog v1 đáp ứng đầy đủ.

Luồng hàng số đã chốt ở mức kịch bản nghiệp vụ: mã/tài khoản Unique, link Shared và gói Extension. Nội dung có sẵn được giao sau thanh toán qua Đơn hàng của tôi/email; gia hạn do nhân viên xử lý, lưu kết quả thực tế đã xác minh và tách với trạng thái nhận tiền. Kết quả chưa rõ phải PendingVerification, không được gia hạn lại trước khi đối chiếu. Không yêu cầu LMS/API cấp quyền. Chi tiết và các quyết định còn mở nằm trong [quy tắc giao hàng số](business/digital-fulfillment.md); chưa triển khai hoặc cam kết toàn bộ các kịch bản vào MVP.

Product thuộc đúng một ProductType và có 0–N Category qua bảng nối N–N. Category phục vụ phân loại, ProductType quyết định bộ trường. Chi tiết tại [thiết kế database](commerce-database.md).

Đã chốt hành trình mua: vật lý và hàng số dùng chung giỏ/đơn, thanh toán một lần, từng món giao riêng. Đơn có hàng số phải trả trước toàn bộ; COD chỉ cho đơn thuần vật lý. Tạo đơn thành công thì giữ hàng; trả trước mặc định 30 phút/cấu hình. Trả sau hạn kiểm tra giữ lại đủ toàn bộ hoặc chờ nhân viên xử lý/hoàn tiền. Chưa triển khai checkout/fulfillment; lỗi/hủy/hoàn từng phần, giữ COD và nguồn xác nhận tiền xử lý ở bước liên quan.

Tiến độ từng bước của mốc đầu nằm tại [hồ sơ Catalog v1](features/catalog-v1.md).

Theo chỉ dẫn mới nhất ngày 2026-10-18, ưu tiên **shop nhỏ, đủ luồng mua/quản trị thông thường**. Dừng vòng hỏi sâu và chuyển sang [kế hoạch MVP gọn](features/shop-mvp.md); [phỏng vấn](features/business-discovery.md) giữ các quyết định và chi tiết còn mở. Chưa bắt đầu code checkout; các điều kiện nghiệm thu runtime/adapter trước vận hành thật vẫn còn hiệu lực.

## Các mốc và điều kiện hoàn tất

| Mốc | Đầu ra | Trạng thái hiện tại | Điều kiện còn lại |
|---|---|---|---|
| 0. Foundation | Identity, Function–Action ACL, sessions, audit, migration/bootstrap/preflight | Có triển khai và kiểm thử trong repo | Giữ regression khi bổ sung capability/use case |
| 1. Chốt thiết kế commerce | Cardinality, từ điển bảng/cột, FK/unique/check/index, transaction/xóa, API | Đã có [database](commerce-database.md) và [API](commerce-api.md) | Cập nhật cùng mọi thay đổi schema/hành vi tiếp theo |
| 2. Runtime triển khai | Linux CI, Docker non-root, volume media/keys, health, proxy/restart, migration riêng | Local key/proxy gate 5/5; image gate đã tăng kiểm operator CLI và recreate volume; **chưa nghiệm thu Linux/Docker/CI thật** | Docker daemon/Linux host hoạt động; chạy image trên cả hai DB; lưu run/log kết quả |
| 3. Transaction/media/outbox | Media lifecycle, DB outbox, lease/retry/dead-letter/replay và cleanup | Đã triển khai; có regression trên hai DB | Nghiệm thu container/volume/restart thật; đo tải sweeper khi backlog lớn |
| 4. Catalog/giá/publication | ProductType/typed fields, Category N–N, options/SKU, giá, Draft/Published/Archived | Đã triển khai; có kiểm thử domain/API/database | Duy trì kiểm chứng hai provider khi mở rộng; xem giới hạn trong API docs |
| 5. Inventory/public API | Balance MAIN, movement bất biến/idempotency, typed filters, Published-only/media | Đã triển khai; có kiểm thử concurrency và quyền | Chưa có reservation; bổ sung cùng checkout ở mốc sau |
| 6. Hoàn thiện và nghiệm thu mốc đầu | Review/regression, template đổi tên, module flag, OpenAPI, vận hành/backup | Review/template đã kiểm; native recovery DB+media+keys/outbox local đạt 2/2, Catalog compatibility 2/2 | Đóng runtime/adapter; diễn tập deployment restore/RPO/RTO và session invalidation ở môi trường triển khai |
| 7. Ordering/checkout | Cart/Order chung vật lý + số, OrderItem snapshot, giao riêng, reservation/idempotency | **Đã chốt hành trình và giữ lúc tạo đơn; trả trước mặc định 30 phút/cấu hình; chưa triển khai/chưa tạo bảng** | Theo MVP: cách giao/kho số trước, sau đó Order/giữ hàng; trả muộn giữ lại đủ toàn bộ hoặc chờ nhân viên; thiết kế từng lát rồi mới migration |
| 8. Payment | Attempts/refunds, webhook bền vững và deduplication, đối soát | **Chưa triển khai; chưa chọn provider** | Chốt provider và state machine; kiểm thử sandbox/thật theo phạm vi cho phép |
| 9. Shipping | Shipment/ShipmentItem có số lượng, giao từng phần, trạng thái riêng | **Chưa triển khai; MVP đề xuất nhân viên cập nhật vận chuyển** | Chốt thao tác admin/đồng bộ Order; tích hợp carrier/callback khi có nhu cầu, chưa bắt buộc chọn nhiều provider cho MVP |

Các mốc 2–6 có phần triển khai song song. Tồn tại code ở mốc sau không làm các điều kiện nghiệm thu của mốc trước tự hoàn tất.

## Việc ưu tiên tiếp theo

1. **MVP cho shop nhỏ — đang ưu tiên:** đã có [kế hoạch gọn](features/shop-mvp.md) từ các quyết định được ghi nhận. Dừng mở rộng phỏng vấn; triển khai các luồng bán hàng/admin thông thường và xử lý chi tiết khi tới bước liên quan. Chưa đưa rule builder/page builder/PC BOM hoặc nhiều adapter vào MVP.
2. **Bước BE tiếp theo đề xuất:** thiết kế tối thiểu cách giao được hỗ trợ và kho số, dùng lại Catalog hiện có; sau đó Cart/Order/giữ hàng, nhận tiền/giao số, admin/giao vật lý/hủy/hoàn. Mỗi lát ghi cardinality/ownership/cột/constraints/concurrency/xóa, API/quyền và migrations hai DB theo [mẫu](templates/feature-plan.md). Giữ DDD có chọn lọc; không để handler tự bỏ qua quy tắc tồn/cấp phát/trạng thái.
3. **Nghiệm thu runtime trước vận hành thật:** chạy Linux CI và container non-root trên SQL Server/PostgreSQL theo [runbook](operations-runbook.md); kiểm operator CLI, volume media/Data Protection keys, health và thay container. Local proxy đã đạt, nhưng chưa chứng minh cấu hình proxy của deployment cụ thể.
4. **Vận hành/adapter trước vận hành thật:** [native recovery drill local](verification/2026-10-18-operations.md) đã đạt trên hai DB; tiếp tục staging restore/RPO/RTO/session policy và smoke các adapter được bật bằng cấu hình thật.

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

Đợt tiếp theo đã thực hiện: [nghiệm thu vận hành](features/operations-acceptance.md), [bằng chứng ngày 2026-10-18](verification/2026-10-18-operations.md). Container hiện vẫn blocked tại host Windows/Docker engine timeout; không chuyển mốc runtime thành hoàn tất.
