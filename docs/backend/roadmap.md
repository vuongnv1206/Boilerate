# Lộ trình backend shop

Cập nhật: 2026-10-19. Lộ trình được cập nhật cùng code và [nhật ký phát triển](history/README.md). Trạng thái dưới đây mô tả bằng chứng hiện có; không coi cấu hình CI hoặc test adapter giả là nghiệm thu môi trường thật.

Lộ trình này chỉ theo dõi BE. Xem [roadmap FE](../frontend/roadmap.md) cho công việc giao diện và [shared](../shared/README.md) cho nghiệp vụ/quy trình. Việc tổ chức lại docs ngày 2026-10-19 không thay đổi kết quả kiểm chứng hoặc nghiệm thu BE.

## Phạm vi nền tảng

Catalog v1 ban đầu phục vụ một shop, hàng vật lý, VND nguyên đồng và kho MAIN không bán âm; [kho số](features/digital-inventory.md) và [Cart/Order/reservation](features/ordering-checkout.md) đã có code; [nhận tiền/giao số](features/payment-digital-delivery.md) có luồng xác nhận chuyển khoản và email outbox. Dùng modular monolith, .NET 10/MediatR/EF Core, một database có schema nghiệp vụ; hỗ trợ SQL Server và PostgreSQL. DDD bảo vệ Product/ProductType/InventoryItem/DigitalStockItem/Cart/Order; handler/service điều phối quyền và transaction.

Mục tiêu mới nhất được người dùng làm rõ: **một phần mềm cho một shop, thích ứng khi muốn bán mặt hàng khác; quản trị sản phẩm tham khảo Webflow**. Shop tự cấu hình loại/bộ trường/options/SKU và chọn cách bán/giao đã hỗ trợ. Quần áo, điện thoại, laptop, PC, khóa học và tài khoản là ví dụ kiểm tra độ linh hoạt; PC không phải yêu cầu build/tồn linh kiện bắt buộc. Không xây nền tảng quản lý nhiều shop; chỉ dẫn này thay thế cách hiểu mục tiêu nhiều installation trước đó. Xem [phạm vi linh hoạt](../shared/business/product-flexibility.md) và [business discovery](../shared/features/business-discovery.md); chưa coi mục tiêu mới là đã được Catalog v1 đáp ứng đầy đủ.

Luồng hàng số đã chốt ở mức kịch bản nghiệp vụ: mã/tài khoản Unique, link Shared và gói Extension. Nội dung có sẵn được giao sau thanh toán qua Đơn hàng của tôi/email; gia hạn do nhân viên xử lý, lưu kết quả thực tế đã xác minh và tách với trạng thái nhận tiền. Kết quả chưa rõ phải PendingVerification, không được gia hạn lại trước khi đối chiếu. Không yêu cầu LMS/API cấp quyền. Chi tiết và các quyết định còn mở nằm trong [quy tắc giao hàng số](../shared/business/digital-fulfillment.md); chưa triển khai hoặc cam kết toàn bộ các kịch bản vào MVP.

Product thuộc đúng một ProductType và có 0–N Category qua bảng nối N–N. Category phục vụ phân loại, ProductType quyết định bộ trường. Chi tiết tại [thiết kế database](commerce-database.md).

Đã chốt hành trình mua: vật lý và hàng số chung giỏ/đơn, thanh toán một lần, từng món giao riêng. Đơn có hàng số phải trả trước toàn bộ; COD chỉ thuần vật lý. Cart/checkout/snapshot/giữ hàng đã triển khai; trả trước mặc định30phút/cấu hình, COD giữ tới hủy hoặc bước xử lý tiếp theo. Nhân viên xác nhận chuyển khoản đã kiểm tra; Unique/Shared tự giao và email qua outbox. Trả sau hạn giữ lại đủ toàn bộ thì tiếp tục, thiếu hàng vẫn ghi Paid + NeedsReview để nhân viên bổ sung hàng/retry hoặc xử lý hoàn tiền ở mốc sau. Giao vật lý/COD và gia hạn có claim/xác minh đã có API; [hậu mãi4C](features/refunds-returns.md) thêm refund/return thủ công. Gateway/carrier/gia hạn tự động bên ngoài chưa triển khai.

Tiến độ từng bước của mốc đầu nằm tại [hồ sơ Catalog v1](features/catalog-v1.md).

Theo chỉ dẫn mới nhất ngày 2026-10-18, ưu tiên **shop nhỏ, đủ luồng mua/quản trị thông thường**. [Kho số](features/digital-inventory.md) và [Ordering](features/ordering-checkout.md) theo từng bước của [MVP](features/shop-mvp.md); [phỏng vấn](../shared/features/business-discovery.md) giữ nguồn quyết định. Các điều kiện nghiệm thu runtime/adapter trước vận hành thật vẫn còn hiệu lực.

## Các mốc và điều kiện hoàn tất

| Mốc | Đầu ra | Trạng thái hiện tại | Điều kiện còn lại |
|---|---|---|---|
| 0. Foundation | Identity, Function–Action ACL, sessions, audit, migration/bootstrap/preflight; [nhóm quyền](features/grouped-authorization.md) | Đã bổ sung nhóm và UI quản trị, kiểm chứng SQL Server cô lập | PostgreSQL runtime hoãn theo người dùng; chưa nghiệm thu deployment |
| 1. Chốt thiết kế commerce | Cardinality, từ điển bảng/cột, FK/unique/check/index, transaction/xóa, API | Đã có [database](commerce-database.md) và [API](commerce-api.md) | Cập nhật cùng mọi thay đổi schema/hành vi tiếp theo |
| 2. Runtime triển khai | Linux CI, Docker non-root, volume media/keys, health, proxy/restart, migration riêng | Local key/proxy gate 5/5; image gate đã tăng kiểm operator CLI và recreate volume; **chưa nghiệm thu Linux/Docker/CI thật** | Docker daemon/Linux host hoạt động; chạy image trên cả hai DB; lưu run/log kết quả |
| 3. Transaction/media/outbox | Media lifecycle, DB outbox, lease/retry/dead-letter/replay và cleanup | Đã triển khai; có regression trên hai DB | Nghiệm thu container/volume/restart thật; đo tải sweeper khi backlog lớn |
| 4. Catalog/giá/publication | ProductType/typed fields, Category N–N, options/SKU, giá, Draft/Published/Archived | Đã triển khai; có kiểm thử domain/API/database | Duy trì kiểm chứng hai provider khi mở rộng; xem giới hạn trong API docs |
| 5. Inventory/public API | Balance MAIN, movement bất biến/idempotency, typed filters, Published-only/media | Đã triển khai; có kiểm thử concurrency và quyền; Reserved bổ sung cùng Ordering | Dispatch bổ sung ở mốc9, xuất bằng movement âm và consume reservation |
| 6. Hoàn thiện và nghiệm thu mốc đầu | Review/regression, template đổi tên, module flag, OpenAPI, vận hành/backup | Review/template đã kiểm; native recovery DB+media+keys/outbox local đạt 2/2, Catalog compatibility 2/2 | Đóng runtime/adapter; diễn tập deployment restore/RPO/RTO và session invalidation ở môi trường triển khai |
| 6A. Cách giao/kho số — MVP bước 1 | Physical/Unique/Shared/ManualExtension, stock metadata/import/shared/state/reveal, encryption/ACL/audit | **Đã triển khai; domain 10/10, provider 16/16, regression 184/184 đạt local tại bước1** | Nhận tiền/giao theo đơn ở mốc8, gia hạn ở mốc10; runtime deployment còn mở; [bằng chứng ban đầu](verification/2026-10-18-digital-inventory.md) |
| 7. Ordering/checkout | Cart/Order chung vật lý + số, snapshot, reservation/idempotency, hủy/hết hạn/xem đơn | **Đã triển khai/kiểm chứng local; regression cuối222/222 đạt**, gồm24 Ordering integration và2 migration cases trên hai DB | Nhận tiền/trả muộn/giao số được bổ sung ở mốc8; [bằng chứng](verification/2026-10-18-ordering-checkout.md) |
| 8. Payment/giao số | Xác nhận chuyển khoản thực nhận, cấp Unique/Shared và email outbox; refund ở mốc11, attempts/webhook ở lát sau | **Đã triển khai/kiểm local; regression276/276 đạt**, [hồ sơ](features/payment-digital-delivery.md), [bằng chứng](verification/2026-10-18-payment-delivery.md) | Refund bổ sung ở mốc11; gateway/SMTP thật và runtime còn mở |
| 9. Shipping | Shipment/ShipmentItem có số lượng, giao từng phần, trạng thái riêng; thu COD độc lập | **Đã triển khai/kiểm local4A; regression326/326 đạt**, [hồ sơ](features/physical-shipping.md), [bằng chứng](verification/2026-10-18-physical-shipping.md) | Gia hạn ở mốc10, hậu mãi ở mốc11; carrier/callback và deployment còn mở |
| 10. Gia hạn thủ công | Request/claim/start/PendingVerification/kết quả và bằng chứng, hoàn tất đơn độc lập Paid | **Đã triển khai/kiểm chứng local4B; regression398/398 đạt**, targeted52/52 nền và34/34 API; [hồ sơ](features/manual-renewals.md), [bằng chứng](verification/2026-10-19-manual-renewals.md) | Refund/returns bổ sung ở mốc11; provider tự động và nghiệm thu deployment còn mở |
| 11. Hậu mãi4C | Refund ledger, nhận/restock theo shipment, Closed/Stopped và COD trả về | **Đã triển khai/kiểm chứng local; regression468/468 đạt**,32 domain/32 integration/6 migration mới; [hồ sơ](features/refunds-returns.md), [báo cáo](verification/2026-10-19-aftersales.md) | Nghiệm thu toàn luồng/runtime/adapter; gateway/RMA/đổi hàng ở lát riêng |
| 12. Admin Orders workspace | List filter server-side, detail snapshot read-only, cancel/payment confirmation/retry fulfillment, safe history và development seed | **Đã triển khai source; build pass, targeted seed tests 3/3**, [hồ sơ](features/admin-orders.md), [bằng chứng](verification/2026-10-21-admin-orders.md) | Full DB/provider replay, HTTP conflict/history tests, authenticated FE E2E và runtime acceptance |
| 13. Catalog workspace | Unified editor save, gallery/ảnh/default SKU, metadata/filter/sort/count, bulk publish/archive và editor danh mục kiểu Webflow | Product workspace đã kiểm chứng local; editor danh mục v2 đã có source/build, API membership versioned; [hồ sơ](features/catalog-workspace.md) | Chưa test API HTTP/browser E2E cho editor danh mục; nghiệm thu quyền, conflict và runtime; full provider suite còn chờ cấu hình ổn định; CSV là giai đoạn sau |

Các mốc 2–6 có phần triển khai song song. Tồn tại code ở mốc sau không làm các điều kiện nghiệm thu của mốc trước tự hoàn tất.

## Việc ưu tiên tiếp theo

Ngày 21/10, [browser session](features/browser-session.md) bổ sung cookie HttpOnly để FE giữ đăng nhập khi reload, dùng cơ chế phiên/thu hồi hiện có. [Báo cáo](verification/2026-10-21-browser-session.md) ghi HTTP contract và SQL Server local; không thay các gate deployment bên dưới.

1. **MVP cho shop nhỏ — đang ưu tiên:** đã có [kế hoạch gọn](features/shop-mvp.md) từ các quyết định được ghi nhận. Dừng mở rộng phỏng vấn; triển khai các luồng bán hàng/admin thông thường và xử lý chi tiết khi tới bước liên quan. Chưa đưa rule builder/page builder/PC BOM hoặc nhiều adapter vào MVP.
2. **Bước tiếp theo:** nghiệm thu toàn luồng MVP và triển khai, sau [4C hậu mãi](features/refunds-returns.md) local468/468. Đối chiếu hành trình Catalog→checkout→nhận tiền→giao/gia hạn→refund/return cùng quyền, docs/OpenAPI; xử lý chỗ thiếu trước khi mở gateway/RMA/đổi hàng. Nhận tiền và hoàn tiền hiện là nhân viên xác nhận thực tế; chưa có provider/webhook tự động. Mỗi lát giữ thiết kế/DDD/transaction/quyền và migrations hai DB theo [mẫu](../shared/templates/feature-plan.md).
3. **Admin Orders:** hoàn tất gate runtime cho mốc 12: chạy `--seed-dev` replay trên SQL Server/PostgreSQL, test filter/history/version conflict qua HTTP, rồi chạy FE E2E với actor development. Giữ invariant snapshot; không mở API edit Order để “bù” UI.
4. **Nghiệm thu runtime trước vận hành thật:** chạy Linux CI và container non-root trên SQL Server/PostgreSQL theo [runbook](operations-runbook.md); kiểm operator CLI, volume media/Data Protection keys, health và thay container. Local proxy đã đạt, nhưng chưa chứng minh cấu hình proxy của deployment cụ thể.
5. **Vận hành/adapter trước vận hành thật:** [native recovery drill local](verification/2026-10-18-operations.md) đã đạt trên hai DB; tiếp tục staging restore/RPO/RTO/session policy và smoke các adapter được bật bằng cấu hình thật.

Docker daemon chưa hoạt động là giới hạn của lần nghiệm thu hiện tại; cần kiểm tra lại điều kiện đó khi tiếp tục, không coi đây là trạng thái cố định của dự án.

## Hướng database cho các mốc sau

| Quan hệ | Cardinality/hợp đồng dự kiến |
|---|---|
| Order → OrderItem | **Đã triển khai** 1–N, snapshot tên/SKU/options/kind/giá/số lượng |
| Order → PaymentAttempt | 1–N; cho phép thanh toán lại |
| PaymentReceipt → Refund | **Đã triển khai4C** 1–N; tổng hoàn không vượt receipt |
| PaymentAttempt → giao dịch hoàn gateway | Hướng tương lai khi tích hợp provider; không tạo attempt giả cho ledger thủ công |
| Order → Shipment | 1–N; chừa khả năng giao từng phần |
| Shipment ↔ OrderItem | N–N qua ShipmentItem có số lượng |

Order/OrderItem, reservation, PaymentReceipt/Refund, DigitalDelivery, Shipment/ShipmentItem, PhysicalReturn/Items và Renewal đã có schema; PaymentAttempt/gateway còn là hướng tương lai. Payment sở hữu receipts/refunds thực tế; Order, Payment và Shipment có trạng thái riêng. Return/refund không xóa lịch sử giao hoặc tự thu hồi quyền số. Reservation dùng `Available = OnHand - Reserved`, xuất giảm cả OnHand/Reserved và tăng ConsumedQuantity theo [shipping](features/physical-shipping.md); Unique giữ/giao theo OrderItem. Hủy/hết hạn chỉ nhả đơn chưa nhận tiền/chưa xuất; xem [hồ sơ Ordering](features/ordering-checkout.md).

## Cách theo dõi từ đây

Mỗi tính năng BE tạo hồ sơ trong `docs/backend/features/` theo [quy trình](../shared/development-workflow.md), cập nhật bước đang làm và bằng chứng ngay khi có. [Nhật ký](history/README.md) lưu quyết định, kết quả và commit thực tế. Tài liệu hợp đồng hiện hành giữ mô tả cuối cùng; nhật ký giữ lý do và trình tự thay đổi.

Các lần kiểm chứng commerce trước đây được lưu tại [báo cáo kiểm chứng](verification/2026-10-17-commerce.md) và [review ngày 2026-10-17](commerce-review.md); raw evidence cục bộ tại `artifacts/commerce-review/report.md` khi còn trong workspace. Lần kiểm chứng mới phải ghi kết quả riêng, không thay thế âm thầm kết quả cũ.

Đợt tiếp theo đã thực hiện: [nghiệm thu vận hành](features/operations-acceptance.md), [bằng chứng ngày 2026-10-18](verification/2026-10-18-operations.md). Container hiện vẫn blocked tại host Windows/Docker engine timeout; không chuyển mốc runtime thành hoàn tất.
