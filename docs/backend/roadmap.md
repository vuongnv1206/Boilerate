# Lộ trình backend shop

Cập nhật: 2026-10-27 (Asia/Saigon), gồm mốc Tổng quan vận hành trên BE `4ff259b` cộng working tree tính năng. Lộ trình được cập nhật cùng code và [nhật ký phát triển](history/README.md). Trạng thái dưới đây mô tả bằng chứng hiện có; không coi cấu hình CI hoặc test adapter giả là nghiệm thu môi trường thật.

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
| 14. Tổng quan vận hành Admin | Ba GET chỉ đọc cho việc hiện tại, tiền theo ngày Việt Nam và cảnh báo kho; queue dùng chung với Orders/Renewals; quyền FinancialSummary.View riêng | **Đã triển khai và kiểm chứng BE trên SQL Server cô lập; rollout DB local đã áp index và cấp riêng quyền cho Admin**; [hợp đồng](features/admin-overview.md), [bằng chứng tích hợp](../../../Frontend/docs/verification/2026-10-26-admin-overview.md) | Browser/live cuối đợt theo báo cáo tích hợp; PostgreSQL runtime thiếu connection test, staging và mobile chưa nghiệm thu |

Các mốc 2–6 có phần triển khai song song. Tồn tại code ở mốc sau không làm các điều kiện nghiệm thu của mốc trước tự hoàn tất.

## Cập nhật đối chiếu 04/11/2026

Mã giảm giá đã có Admin/cart/checkout/quota/snapshot, theo hồ sơ discount ngày 01/11; không đưa vào backlog tính năng mới. Ba regression Webflow đã chuyển sang provider theories, chỉ SQL Server có bằng chứng thực thi trong lượt ghi nhận; PostgreSQL runtime vẫn chưa nghiệm thu. Các dòng ưu tiên cũ dưới đây cần đọc cùng hồ sơ tính năng mới nhất.

Đợt hiện tại: [hoàn thiện UX local](../shared/features/local-usability-upgrade.md), giữ API/schema và database cửa hàng. Kiểm chứng lại trên đúng source FE/BE cuối, không dùng số ca lịch sử thay bằng chứng mới.

## Việc ưu tiên tiếp theo

Core commerce từ Catalog/kho đến checkout, nhận tiền, giao số/vật lý, gia hạn và hậu mãi đã có code. Regression **468/468 ngày 19/10 là bằng chứng lịch sử**, không xác nhận HEAD sau thay đổi Webflow editor/category ngày 22/10. Source FE đã có Orders, Shipping/After-sales, Renewals và System; hành trình UI hiện tại còn cần nghiệm thu theo [kế hoạch FE](../../../Frontend/PRODUCT.md).

Mốc Tổng quan ngày 26/10 đã chạy Release non-provider regression và integration SQL Server cô lập, kiểm tra tiền, queue/count, tồn khả dụng, quyền độc lập và GET không ghi DB. Local `BoilerateDevelopment` đã backup/verify trước migration index; rollout chỉ đăng ký/cấp `FinancialSummary.View` cho Admin và giữ nguyên 55 dòng quyền cũ. Kết quả browser/live cuối đợt, source/build và giới hạn được giữ tại [báo cáo tích hợp](../../../Frontend/docs/verification/2026-10-26-admin-overview.md). PostgreSQL runtime và deployment chưa được xác nhận. Kết quả 231/231 trên `bab1df5` ngày 23/10 là bằng chứng lịch sử, không thay cho đợt này.

| Ưu tiên | Phần việc BE | Đầu ra để đóng đợt | Phối hợp/phụ thuộc |
|---|---|---|---|
| Đã đạt — nền kiểm tra | Build/non-provider BE và FE checks đã đạt; duy trì khi sửa code | Chốt mốc source cho working tree FE trước vòng tích hợp, mỗi revision có bằng chứng riêng | Browser fixture chưa xác nhận API/DB live; không thay dữ liệu đang vận hành |
| P0 — Catalog mới | Bắt đầu bằng coverage trực tiếp `SetCategoryProducts`: add/remove/no-op, giữ category khác, stale Product.Version rollback toàn batch, quyền và cạnh tranh với editor/delete; tiếp theo hồi quy Webflow migration/retirement/media, tracking=false qua checkout/ship/return | HTTP và UI thật lưu/reload đúng; conflict không ghi dở membership, UI giữ draft và phản ánh đúng metadata đã lưu riêng; không phá đơn/kho/lịch sử | Xác nhận Host artifact/revision, migration và SQL Server test đích trước write; [Catalog BE](features/catalog-workspace.md), [điều phối](../shared/features/catalog-workspace.md) |
| P1 — Admin Orders | HTTP filters/paging ổn định/history safe/ACL/409; seed replay DB trống và DB có operator edits; kiểm receipt, shipping/COD, refund/return và renewal commands | Cùng data set thực đi được qua list/detail/command/history; replay không tạo side effect lần hai; seed không ghi đè operator | [Orders](features/admin-orders.md), [Shipping](features/physical-shipping.md), [Renewal](features/manual-renewals.md), [After-sales](features/refunds-returns.md); FE chạy authenticated browser |
| P1 — toàn luồng API | Physical COD; mixed prepaid Unique/Shared; trả muộn thiếu hàng/retry; PendingVerification; refund/return/restock; quyền owner/staff và concurrency món cuối | Mỗi kịch bản có input/kết quả, rollback/idempotency và nguồn bằng chứng trên đúng source | [Tiêu chí MVP](features/shop-mvp.md#4-tiêu-chí-chấp-nhận-dự-kiến); giữ xác nhận nhận/hoàn tiền thủ công |
| P2 — guest storefront | Guest cart/OTP/checkout/My Orders, cài đặt chuyển khoản Admin và hợp đồng API/database | **Đã triển khai; SQL Server integration 5/5, non-provider BE 233/233, live browser/API 12/12 trên bốn cấu hình** | PostgreSQL runtime chưa chạy do chưa có test connection; SMTP thật và deployment staging thuộc gate riêng. Xem [feature BE](features/guest-storefront.md), [phối hợp](../shared/features/guest-storefront-checkout.md), [bằng chứng](../../../Frontend/docs/verification/2026-10-24-guest-storefront.md) |
| P3 — staging/release | Xác minh CI cho revision release; non-root container, migration riêng/operator CLI, media/keys volume, health/proxy/restart; restore và session policy; smoke SMTP/adapter được bật | Bản deploy cụ thể vượt các gate runbook và có bằng chứng; không suy từ build/configuration | Chuẩn bị môi trường song song P1; nghiệm thu toàn luồng sau giao diện mua; [runbook](operations-runbook.md) |

SQL Server là môi trường local ưu tiên hiện tại. Ngày 24/10, live acceptance guest storefront chạy trên DB SQL Server cô lập và migration mới được áp dụng. `BOILERATE_TEST_POSTGRES` chưa có nên PostgreSQL runtime migration/integration vẫn chưa được kiểm tra. Trước khi tuyên bố release hỗ trợ cả hai provider, chạy migration fresh/upgrade và các hồi quy mới trên PostgreSQL; ba regression Webflow đã chuyển sang provider theories; cần thực thi runtime PostgreSQL trước khi xác nhận parity. Không dùng các ca cũ chạy PG để nghiệm thu editor mới.

Ngày 21/10, [browser session](features/browser-session.md) có HTTP contract/SQL Server local và [báo cáo CI](verification/2026-10-21-browser-session.md) ghi Linux đã qua integration hai DB, container còn đang chạy tại lúc ghi. Kết quả cuối workflow và runtime revision mới **chưa xác minh trong lần rà soát này**; tình trạng Docker bị chặn ở các báo cáo cũ không phải kết luận hiện tại.

Gateway/webhook, carrier tự động, RMA/đổi hàng phức tạp, CSV, rule/page builder và nhiều shop tiếp tục ngoài đợt hoàn tất MVP này. Mỗi lát thay đổi hành vi/database giữ feature plan, transaction/quyền và migrations hai DB theo [mẫu](../shared/templates/feature-plan.md).

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

Đợt nghiệm thu vận hành trước đây có [hồ sơ](features/operations-acceptance.md) và [bằng chứng ngày 2026-10-18](verification/2026-10-18-operations.md). Lượt đó bị chặn bởi Windows/Docker engine timeout; trạng thái môi trường hiện tại cần kiểm tra lại, không chuyển mốc runtime thành hoàn tất từ bằng chứng cũ.
