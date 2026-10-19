# Shop MVP — hoàn chỉnh luồng bán hàng cho một shop nhỏ

Ngày bắt đầu: 2026-10-18. Cập nhật: 2026-10-19, Asia/Saigon.
Trạng thái: bước1 kho số,2 Ordering,3 payment/giao số, [4A shipping/COD](physical-shipping.md), [4B gia hạn](manual-renewals.md) và [4C hậu mãi](refunds-returns.md) đã triển khai/kiểm chứng local; regression mới nhất468/468. Deployment/adapter thật và nghiệm thu toàn bộ MVP chưa hoàn tất.
Mốc roadmap: [Ordering, Payment và Fulfillment](../roadmap.md).

## 1. Mục tiêu và phạm vi

Theo chỉ dẫn mới nhất: shop e-commerce nhỏ, đủ nghiệp vụ thông thường; ưu tiên hoàn chỉnh luồng mua và quản trị. Kết thúc vòng hỏi sâu hiện tại, ghi các chi tiết chưa quyết định vào bước triển khai liên quan.

| Trong MVP đề xuất | Để sau |
|---|---|
| Một phần mềm cho một shop, thích ứng với mặt hàng muốn bán; cấu hình loại/trường/options/SKU bằng Catalog | Nền tảng quản lý nhiều shop, multi-tenant, marketplace |
| Chọn cách giao đã được hỗ trợ: vật lý, Unique, Shared, Extension do nhân viên làm | Rule/workflow builder tùy ý; trình kéo thả storefront; PC build/BOM/kiểm tương thích |
| Giỏ → checkout → đơn → nhận tiền → giao từng món → xem đơn và quản trị | Khuyến mãi phức tạp, loyalty, nhiều kho, đồng bộ ERP |
| Quản lý giao vật lý, hủy/hoàn và lịch sử thao tác đơn giản | Tự động đối soát/hoàn tiền/vận chuyển với nhiều nhà cung cấp |

Quần áo/PC/khóa học là ví dụ dữ liệu và cách bán; không xây module riêng cho từng tên mặt hàng. Webflow là mẫu tham khảo trải nghiệm quản trị sản phẩm. Đây là kế hoạch backend/admin API; chưa yêu cầu dựng frontend builder. Làm rõ mới nhất của người dùng thay thế cách hiểu trước đây rằng mục tiêu là phục vụ nhiều shop qua nhiều installation.

## 2. Quyết định và đề xuất

**Đã xác nhận** trong [phỏng vấn](../../shared/features/business-discovery.md), [phạm vi linh hoạt](../../shared/business/product-flexibility.md) và [quy tắc hàng số](../../shared/business/digital-fulfillment.md):

- Shop tự tạo loại/trường/options/SKU và chọn cách bán/giao sẵn có.
- Vật lý và số cùng giỏ/đơn, trả một lần, giao từng món riêng. Có hàng số phải trả trước toàn bộ; COD chỉ thuần vật lý.
- Tạo đơn thành công thì giữ hàng. Đơn trả trước mặc định 30 phút, shop cấu hình được. Trả sau hạn thì thử giữ lại toàn bộ: đủ tiếp tục, thiếu chờ nhân viên xử lý/hoàn tiền.
- Unique cấp một lần; Shared giao cùng link; Extension do nhân viên xử lý và chỉ lưu kết quả thực tế đã xác minh. Thanh toán và hoàn tất giao/gia hạn độc lập. PendingVerification cấm gia hạn lại khi chưa biết kết quả trước.

**Đề xuất đơn giản cho bản đầu, chưa phải chính sách được xác nhận riêng:** dùng Identity hiện có để khách xem đơn/hàng số; phí ship cấu hình đơn giản; nhân viên cập nhật vận chuyển/mã theo dõi; admin ghi nhận hủy/hoàn tiền với lý do và lịch sử. Chưa chọn gateway/carrier, tự động hoàn tiền hoặc chính sách đổi trả chi tiết. Các lựa chọn này không cản việc lập kế hoạch Catalog/kho số.

### Domain, database và API

Giữ modular monolith + DDD có chọn lọc: domain bảo vệ tồn/cấp phát/trạng thái; handler điều phối quyền, transaction và persistence. Dùng lại ACL Function–Action, audit và outbox; không gọi dịch vụ ngoài khi đang giữ transaction.

Kế hoạch tổng thể này không tự đổi schema; mỗi lát có hồ sơ/migration riêng. ProductType–Product 1–N, Product–Category N–N giữ theo [database hiện hành](../commerce-database.md). Order–OrderItem 1–N đã có schema ở [bước2](ordering-checkout.md); [4A](physical-shipping.md) thêm Order–Shipment 1–N và Shipment–OrderItem N–N có quantity. [4C](refunds-returns.md) thêm PaymentReceipt–Refund1–N và PhysicalReturn–ShipmentItemN–N có quantity. PaymentAttempt/gateway vẫn là hướng tương lai, không tạo attempt giả cho khoản đã hoàn thủ công.

Trước khi code mỗi lát, bổ sung thiết kế tối thiểu theo [mẫu feature plan](../../shared/templates/feature-plan.md): ownership/cardinality, bảng/cột/PK/FK/unique/check/index, concurrency, xóa, migrations SQL Server/PostgreSQL, API/quyền và transaction/idempotency. Không cần hoàn tất thiết kế mọi mốc trước khi bắt đầu lát đầu tiên.

## 3. Kế hoạch từng bước

| Bước | Công việc | Kết quả có thể kiểm chứng | Trạng thái |
|---|---|---|---|
| 1 | Chọn cách giao và kho hàng số | Dùng Catalog hiện có; cấu hình hành vi được hỗ trợ, nhập/quản lý mã/link/tài khoản; bảo vệ nội dung; thiết kế DB/API trước code | Đã triển khai/kiểm chứng local theo [hồ sơ](digital-inventory.md); deployment chưa nghiệm thu |
| 2 | Giỏ, checkout, đơn và giữ hàng | Tổng tiền/địa chỉ/phí ship được xác định trước khi trả; snapshot; đơn chung; đơn trả trước giữ 30 phút/cấu hình, hết hạn/nhả; khách xem đơn | Đã triển khai/kiểm chứng local, regression222/222; [hồ sơ](ordering-checkout.md) |
| 3 | Nhận tiền và giao hàng số | Xác nhận khoản chuyển khoản thực nhận, chống trùng; giao Unique/Shared qua My Orders; email thông báo; Extension chờ nhân viên | Đã triển khai/kiểm local, regression276/276; chưa nghiệm thu SMTP thật, [hồ sơ](payment-digital-delivery.md) |
| 4 | Quản trị đơn và giao vật lý | Xem/tìm đơn; cập nhật xử lý/vận chuyển; hủy/ghi nhận hoàn tiền; gia hạn có lịch sử/bằng chứng và PendingVerification |4A/4B/4C có code và kiểm chứng local; [báo cáo4C](../verification/2026-10-19-aftersales.md), regression468/468 |
| 5 | Kiểm chứng và chuẩn bị chạy thật | Luồng mua trọn vẹn, retry/concurrency/quyền trên hai DB; docs/OpenAPI; hoàn tất các gate runtime/adapter trước vận hành thật | Regression BE local468/468 đã đạt; tiếp tục nghiệm thu toàn luồng và runtime/adapter triển khai |

Mỗi lát có docs/backend/history và commit riêng, giữ các thay đổi model/API/migration liên quan trong một commit build được. Bước tiếp theo là nghiệm thu toàn luồng MVP và môi trường triển khai; không coi Paid, source hoặc local test là nghiệm thu deployment.

## 4. Tiêu chí chấp nhận dự kiến

Các checkbox dành cho nghiệm thu toàn luồng MVP; code/local evidence đã ghi ở bảng bước và từng feature plan, không tự đánh dấu deployment đạt từ regression.

- [ ] Chủ shop thêm loại/trường/options bằng cấu hình và bán được qua các cách giao sẵn có.
- [ ] Một đơn vật lý + số có đúng tổng tiền, liên hệ và địa chỉ khi cần; thanh toán một lần, theo dõi giao riêng; chặn COD nếu có hàng số.
- [ ] Hai khách không được cùng cam kết món cuối; hết hạn nhả đúng hàng; trả muộn chỉ tiếp tục khi giữ lại đủ toàn bộ.
- [ ] Retry checkout/xác nhận tiền/giao số không tạo đơn hoặc cấp hàng lần hai; lỗi email không làm đảo kết quả giao hàng đã thành công.
- [ ] Mua 2 Unique nhận hai mã/tài khoản; Shared hiển thị cùng link; yêu cầu gia hạn có người xử lý/lịch sử và cấm thực hiện lại khi PendingVerification.
- [ ] Hủy nhả phần đang giữ; không tự đưa mã/tài khoản đã giao hoặc hàng đã xuất trở lại hàng sẵn bán. Admin ghi nhận hoàn tiền không vượt số đã thu và có lịch sử.
- [ ] Khách chỉ xem đơn/nội dung của mình; nhân viên chỉ thao tác theo ACL; nội dung kho số không rò qua public API/log/email mặc định.
- [ ] Chạy regression phù hợp trên hai DB và nghiệm thu runtime/adapter thực tế trước khi đưa shop vào vận hành.

Các dòng trên là mục tiêu kiểm chứng, chưa phải kết quả test. Nội dung email và chính sách xử lý hàng đã giao cần được giữ rõ khi thiết kế bước 3–4; không phát triển hệ thống đổi trả phức tạp trong kế hoạch này.

## 5. Nhật ký và kiểm chứng

2026-10-18: lập kế hoạch gọn theo chỉ dẫn shop nhỏ; chuyển từ hỏi sâu ngoại lệ sang các bước triển khai. Bổ sung quyết định giữ hàng 30 phút và trả muộn. Chỉ thay đổi docs; chưa chạy build/test nghiệp vụ, chưa có migration hoặc endpoint mới. Lịch sử: [bản ghi bước này](../history/2026-10-18-11-small-shop-mvp.md).

2026-10-18: code bước 1 hoàn tất, Domain 10/10, provider 16/16 và regression 184/184 (có ca trùng giữa các lượt) đạt; xem [báo cáo](../verification/2026-10-18-digital-inventory.md). Mục tiêu được làm rõ là một phần mềm cho một shop thích ứng sản phẩm, quản trị tham khảo Webflow; không xây nền tảng nhiều shop. Bước 2–5 chưa hoàn tất.

2026-10-18: hoàn tất bước2 BE, gồm snapshot/giữ hàng/customer/admin đọc-hủy/expiry; regression cuối222/222 đạt sau review/fix. [Báo cáo](../verification/2026-10-18-ordering-checkout.md) phân biệt các lượt test và phần chưa chạy. Tiếp theo bước3 nhận tiền/giao số; không đánh dấu toàn bộ MVP hoặc deployment hoàn tất.

2026-10-18: hoàn tất bước3 local: receipt chuyển khoản nhân viên xác nhận, Unique/Shared snapshot, trả muộn/retry và email outbox. Regression276/276 đạt trên hai DB; xem [bằng chứng](../verification/2026-10-18-payment-delivery.md). Extension chỉ chờ nhân viên, chưa có gia hạn/Shipping/refund; SMTP thật và deployment còn mở.

2026-10-18: hoàn tất [4A shipping/COD](physical-shipping.md) local: partial shipments, consume phần đã giữ, ledger, owner/admin API, COD tách đã giao/đã thu. Regression đầu324/325 phát hiện precision timestamp PostgreSQL, đã sửa và chạy lại326/326 đạt; [báo cáo](../verification/2026-10-18-physical-shipping.md). Phần gia hạn/returns/refund của bước4 chưa triển khai; carrier/gateway/SMTP/runtime thật chưa nghiệm thu.

Catalog và vận hành đã kiểm chứng trước đó có [báo cáo riêng](../verification/2026-10-17-commerce.md); chúng không xác nhận MVP Order/Payment/fulfillment. Linux/Docker/CI thật và adapter vẫn là các gate mở trong roadmap.

2026-10-19: hoàn tất local [4C hậu mãi](refunds-returns.md): refund từng phần/toàn bộ, Closed/Stopped, nhận/restock theo shipment và COD trả về. Targeted đầu86/90 với4fail test setup/assertion, sửa rồi migration6/6 và regression468/468 đạt; [báo cáo](../verification/2026-10-19-aftersales.md) giữ từng lượt. Tiếp theo bước5, không mở gateway/RMA phức tạp trong lát này.

2026-10-19: hoàn tất local [4B gia hạn thủ công](manual-renewals.md): request sau Paid, target sau mua, claim/Start/PendingVerification, Verify riêng, expiry thực tế, lịch sử và order reconcile. Targeted52/52 nền,34/34 API và regression398/398 đạt trên hai DB; [bằng chứng](../verification/2026-10-19-manual-renewals.md). Refund/returns và runtime/adapter thật còn mở; toàn bộ bước4/MVP chưa hoàn tất.

## 6. Lịch sử commit

Các quyết định nền đã được lưu tại `2cd1120` (cấp phát/gia hạn) và `8836fc9` (flex/đơn chung/trả trước). Hash của kế hoạch này tra bằng `git log --follow -- docs/backend/features/shop-mvp.md`; không ghi hash dự đoán.

## 7. Chi tiết xử lý khi tới bước liên quan

| Chi tiết còn mở | Xử lý tại bước |
|---|---|
| Cách giao đã chọn ở Product, mọi SKU kế thừa; một mode/Product trong lát kho số; bảo vệ nội dung bằng mã hóa/quyền riêng. Thay mode sau tạo hoặc nhiều mode/Product để sau nếu có nhu cầu | [1: đã có thiết kế/code](digital-inventory.md) |
| Bước2 dùng khách đăng nhập, ship phẳng cấu hìnhserver, COD khôngTTLtrảtrước, snapshotgiá và hủyđơnchờ; thay đổi chính sách sau này ghi theo usecase | [2](ordering-checkout.md) |
| Đã có xác nhận chuyển khoản do nhân viên, late payment, quyền Payment.Confirm/Order.Fulfill và email chỉ thông báo; provider/webhook tự động và SMTP thật chưa nghiệm thu | [3](payment-digital-delivery.md) |
| Gia hạn dùng Verify riêng, claim chỉ bàn giao trước Start theo [4B](manual-renewals.md); hủy/hoàn từng phần và chính sách hàng đã giao còn ở lát sau | 4 |

Không cần mở tiếp vòng phỏng vấn tổng thể cho mọi chi tiết trong bảng. Khi triển khai, ghi đề xuất tối giản cùng trường hợp sử dụng; chỉ hỏi lại những quyết định thực sự ảnh hưởng trải nghiệm/chính sách của shop và chưa có căn cứ để chọn.
