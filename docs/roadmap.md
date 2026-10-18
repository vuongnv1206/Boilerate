# Lộ trình backend shop

Cập nhật: 2026-10-18. Lộ trình được cập nhật cùng code và [nhật ký phát triển](history/README.md). Trạng thái dưới đây mô tả bằng chứng hiện có; không coi cấu hình CI hoặc test adapter giả là nghiệm thu môi trường thật.

## Phạm vi nền tảng

Catalog v1 ban đầu phục vụ một shop, hàng vật lý, VND nguyên đồng và kho MAIN không bán âm; [kho số](features/digital-inventory.md) và [Cart/Order/reservation](features/ordering-checkout.md) đã có code; [nhận tiền/giao số](features/payment-digital-delivery.md) có luồng xác nhận chuyển khoản và email outbox. Dùng modular monolith, .NET 10/MediatR/EF Core, một database có schema nghiệp vụ; hỗ trợ SQL Server và PostgreSQL. DDD bảo vệ Product/ProductType/InventoryItem/DigitalStockItem/Cart/Order; handler/service điều phối quyền và transaction.

Mục tiêu mới nhất được người dùng làm rõ: **một phần mềm cho một shop, thích ứng khi muốn bán mặt hàng khác; quản trị sản phẩm tham khảo Webflow**. Shop tự cấu hình loại/bộ trường/options/SKU và chọn cách bán/giao đã hỗ trợ. Quần áo, điện thoại, laptop, PC, khóa học và tài khoản là ví dụ kiểm tra độ linh hoạt; PC không phải yêu cầu build/tồn linh kiện bắt buộc. Không xây nền tảng quản lý nhiều shop; chỉ dẫn này thay thế cách hiểu mục tiêu nhiều installation trước đó. Xem [phạm vi linh hoạt](business/product-flexibility.md) và [business discovery](features/business-discovery.md); chưa coi mục tiêu mới là đã được Catalog v1 đáp ứng đầy đủ.

Luồng hàng số đã chốt ở mức kịch bản nghiệp vụ: mã/tài khoản Unique, link Shared và gói Extension. Nội dung có sẵn được giao sau thanh toán qua Đơn hàng của tôi/email; gia hạn do nhân viên xử lý, lưu kết quả thực tế đã xác minh và tách với trạng thái nhận tiền. Kết quả chưa rõ phải PendingVerification, không được gia hạn lại trước khi đối chiếu. Không yêu cầu LMS/API cấp quyền. Chi tiết và các quyết định còn mở nằm trong [quy tắc giao hàng số](business/digital-fulfillment.md); chưa triển khai hoặc cam kết toàn bộ các kịch bản vào MVP.

Product thuộc đúng một ProductType và có 0–N Category qua bảng nối N–N. Category phục vụ phân loại, ProductType quyết định bộ trường. Chi tiết tại [thiết kế database](commerce-database.md).

Đã chốt hành trình mua: vật lý và hàng số chung giỏ/đơn, thanh toán một lần, từng món giao riêng. Đơn có hàng số phải trả trước toàn bộ; COD chỉ thuần vật lý. Cart/checkout/snapshot/giữ hàng đã triển khai; trả trước mặc định30phút/cấu hình, COD giữ tới hủy hoặc bước xử lý tiếp theo. Nhân viên xác nhận chuyển khoản đã kiểm tra; Unique/Shared tự giao và email qua outbox. Trả sau hạn giữ lại đủ toàn bộ thì tiếp tục, thiếu hàng vẫn ghi Paid + NeedsReview để nhân viên bổ sung hàng/retry hoặc xử lý hoàn tiền ở mốc sau. Giao vật lý từng phần và xác nhận COD đã có API thủ công; gia hạn, hoàn tiền và gateway/carrier tự động chưa triển khai.

Tiến độ từng bước của mốc đầu nằm tại [hồ sơ Catalog v1](features/catalog-v1.md).

Theo chỉ dẫn mới nhất ngày 2026-10-18, ưu tiên **shop nhỏ, đủ luồng mua/quản trị thông thường**. [Kho số](features/digital-inventory.md) và [Ordering](features/ordering-checkout.md) theo từng bước của [MVP](features/shop-mvp.md); [phỏng vấn](features/business-discovery.md) giữ nguồn quyết định. Các điều kiện nghiệm thu runtime/adapter trước vận hành thật vẫn còn hiệu lực.

## Các mốc và điều kiện hoàn tất

| Mốc | Đầu ra | Trạng thái hiện tại | Điều kiện còn lại |
|---|---|---|---|
| 0. Foundation | Identity, Function–Action ACL, sessions, audit, migration/bootstrap/preflight | Có triển khai và kiểm thử trong repo | Giữ regression khi bổ sung capability/use case |
| 1. Chốt thiết kế commerce | Cardinality, từ điển bảng/cột, FK/unique/check/index, transaction/xóa, API | Đã có [database](commerce-database.md) và [API](commerce-api.md) | Cập nhật cùng mọi thay đổi schema/hành vi tiếp theo |
| 2. Runtime triển khai | Linux CI, Docker non-root, volume media/keys, health, proxy/restart, migration riêng | Local key/proxy gate 5/5; image gate đã tăng kiểm operator CLI và recreate volume; **chưa nghiệm thu Linux/Docker/CI thật** | Docker daemon/Linux host hoạt động; chạy image trên cả hai DB; lưu run/log kết quả |
| 3. Transaction/media/outbox | Media lifecycle, DB outbox, lease/retry/dead-letter/replay và cleanup | Đã triển khai; có regression trên hai DB | Nghiệm thu container/volume/restart thật; đo tải sweeper khi backlog lớn |
| 4. Catalog/giá/publication | ProductType/typed fields, Category N–N, options/SKU, giá, Draft/Published/Archived | Đã triển khai; có kiểm thử domain/API/database | Duy trì kiểm chứng hai provider khi mở rộng; xem giới hạn trong API docs |
| 5. Inventory/public API | Balance MAIN, movement bất biến/idempotency, typed filters, Published-only/media | Đã triển khai; có kiểm thử concurrency và quyền; Reserved bổ sung cùng Ordering | Dispatch bổ sung ở mốc9, xuất bằng movement âm và consume reservation |
| 6. Hoàn thiện và nghiệm thu mốc đầu | Review/regression, template đổi tên, module flag, OpenAPI, vận hành/backup | Review/template đã kiểm; native recovery DB+media+keys/outbox local đạt 2/2, Catalog compatibility 2/2 | Đóng runtime/adapter; diễn tập deployment restore/RPO/RTO và session invalidation ở môi trường triển khai |
| 6A. Cách giao/kho số — MVP bước 1 | Physical/Unique/Shared/ManualExtension, stock metadata/import/shared/state/reveal, encryption/ACL/audit | **Đã triển khai; domain 10/10, provider 16/16, regression 184/184 đạt local tại bước1** | Nhận tiền/giao theo đơn bổ sung ở mốc8; gia hạn và runtime deployment còn mở; [bằng chứng ban đầu](verification/2026-10-18-digital-inventory.md) |
| 7. Ordering/checkout | Cart/Order chung vật lý + số, snapshot, reservation/idempotency, hủy/hết hạn/xem đơn | **Đã triển khai/kiểm chứng local; regression cuối222/222 đạt**, gồm24 Ordering integration và2 migration cases trên hai DB | Nhận tiền/trả muộn/giao số được bổ sung ở mốc8; [bằng chứng](verification/2026-10-18-ordering-checkout.md) |
| 8. Payment/giao số | Xác nhận chuyển khoản thực nhận, cấp Unique/Shared và email outbox; attempts/refunds/webhook ở lát sau | **Đã triển khai/kiểm local; regression276/276 đạt**, [hồ sơ](features/payment-digital-delivery.md), [bằng chứng](verification/2026-10-18-payment-delivery.md) | Gateway/SMTP thật, runtime và refund còn mở |
| 9. Shipping | Shipment/ShipmentItem có số lượng, giao từng phần, trạng thái riêng; thu COD độc lập | **Đã triển khai/kiểm local4A; regression326/326 đạt**, [hồ sơ](features/physical-shipping.md), [bằng chứng](verification/2026-10-18-physical-shipping.md) | Gia hạn/refund/returns và carrier/callback, deployment còn mở |
| 10. Gia hạn thủ công | Request/claim/start/PendingVerification/kết quả và bằng chứng, hoàn tất đơn độc lập Paid | **Đang triển khai4B**, [thiết kế](features/manual-renewals.md), chưa kiểm chứng | Domain/DB/API/tests; refund/returns và provider tự động ở lát riêng |

Các mốc 2–6 có phần triển khai song song. Tồn tại code ở mốc sau không làm các điều kiện nghiệm thu của mốc trước tự hoàn tất.

## Việc ưu tiên tiếp theo

1. **MVP cho shop nhỏ — đang ưu tiên:** đã có [kế hoạch gọn](features/shop-mvp.md) từ các quyết định được ghi nhận. Dừng mở rộng phỏng vấn; triển khai các luồng bán hàng/admin thông thường và xử lý chi tiết khi tới bước liên quan. Chưa đưa rule builder/page builder/PC BOM hoặc nhiều adapter vào MVP.
2. **Bước BE tiếp theo:** gia hạn có claim/xác minh/lịch sử và hủy-hoàn/returns sau thanh toán, tiếp nối [giao vật lý/COD](features/physical-shipping.md). Hiện nhận tiền bằng xác nhận chuyển khoản hoặc COD có quyền; chưa có provider/webhook tự động. Mỗi lát ghi cardinality/ownership/cột/constraints/concurrency/xóa, API/quyền và migrations hai DB theo [mẫu](templates/feature-plan.md). Giữ DDD có chọn lọc; không để handler tự bỏ qua quy tắc tồn/cấp phát/trạng thái.
3. **Nghiệm thu runtime trước vận hành thật:** chạy Linux CI và container non-root trên SQL Server/PostgreSQL theo [runbook](operations-runbook.md); kiểm operator CLI, volume media/Data Protection keys, health và thay container. Local proxy đã đạt, nhưng chưa chứng minh cấu hình proxy của deployment cụ thể.
4. **Vận hành/adapter trước vận hành thật:** [native recovery drill local](verification/2026-10-18-operations.md) đã đạt trên hai DB; tiếp tục staging restore/RPO/RTO/session policy và smoke các adapter được bật bằng cấu hình thật.

Docker daemon chưa hoạt động là giới hạn của lần nghiệm thu hiện tại; cần kiểm tra lại điều kiện đó khi tiếp tục, không coi đây là trạng thái cố định của dự án.

## Hướng database cho các mốc sau

| Quan hệ | Cardinality/hợp đồng dự kiến |
|---|---|
| Order → OrderItem | **Đã triển khai** 1–N, snapshot tên/SKU/options/kind/giá/số lượng |
| Order → PaymentAttempt | 1–N; cho phép thanh toán lại |
| PaymentAttempt → Refund | 1–N; tổng hoàn không vượt số đã thu |
| Order → Shipment | 1–N; chừa khả năng giao từng phần |
| Shipment ↔ OrderItem | N–N qua ShipmentItem có số lượng |

Order/OrderItem, reservation, PaymentReceipt, DigitalDelivery và Shipment/ShipmentItem đã có schema; PaymentAttempt/Refund còn là hướng tương lai, chưa tạo bảng. Payment sở hữu receipts và hướng attempts/refunds; Order, Payment và Shipment có trạng thái riêng. Reservation dùng `Available = OnHand - Reserved`, xuất giảm cả OnHand/Reserved và tăng ConsumedQuantity theo [shipping](features/physical-shipping.md); Unique giữ/giao theo OrderItem. Hủy/hết hạn chỉ nhả đơn chưa nhận tiền/chưa xuất; xem [hồ sơ Ordering](features/ordering-checkout.md).

## Cách theo dõi từ đây

Mỗi tính năng tạo hồ sơ trong `docs/features/` theo [quy trình](development-workflow.md), cập nhật bước đang làm và bằng chứng ngay khi có. [Nhật ký](history/README.md) lưu quyết định, kết quả và commit thực tế. Tài liệu hợp đồng hiện hành giữ mô tả cuối cùng; nhật ký giữ lý do và trình tự thay đổi.

Các lần kiểm chứng commerce trước đây được lưu tại [báo cáo kiểm chứng](verification/2026-10-17-commerce.md) và [review ngày 2026-10-17](commerce-review.md); raw evidence cục bộ tại `artifacts/commerce-review/report.md` khi còn trong workspace. Lần kiểm chứng mới phải ghi kết quả riêng, không thay thế âm thầm kết quả cũ.

Đợt tiếp theo đã thực hiện: [nghiệm thu vận hành](features/operations-acceptance.md), [bằng chứng ngày 2026-10-18](verification/2026-10-18-operations.md). Container hiện vẫn blocked tại host Windows/Docker engine timeout; không chuyển mốc runtime thành hoàn tất.
