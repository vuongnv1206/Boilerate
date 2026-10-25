# Guest storefront — giỏ, OTP, checkout và cài đặt thanh toán

Ngày bắt đầu: 2026-10-24
Cập nhật gần nhất: 2026-10-24
Trạng thái: Đã triển khai; nghiệm thu SQL Server đạt, PostgreSQL runtime còn mở
Khu vực phụ trách: BE
Mốc roadmap: [MVP shop một người bán](../roadmap.md#việc-ưu-tiên-tiếp-theo)
Hồ sơ liên quan: [điều phối guest checkout](../../shared/features/guest-storefront-checkout.md), [Ordering](ordering-checkout.md), [API commerce](../commerce-api.md)

## 1. Mục tiêu và phạm vi

Cho phép khách mua không có Identity account sử dụng Published Catalog, giỏ, checkout, My Orders và nội dung số đã trả tiền. Giữ các endpoint và chính sách owner hiện tại cho khách đã đăng nhập và Admin.

Hoãn payment gateway, QR, vận chuyển tự động, đăng ký tài khoản và deployment/SMTP acceptance thật. Phí ship tiếp tục `Ordering__FlatShippingAmount`.

## 2. Domain và database

| Quan hệ | Cardinality | Quy tắc |
|---|---|---|
| Guest cart session → Cart | 1–0..1 | Cookie được bảo vệ mang GUID ngẫu nhiên; GUID chỉ là key cart, không phải Identity. Cart/Order dùng FK hiện có. |
| Order → guest email access | 1–0..1 marker | Order guest có `IsGuestOrder`, `NormalizedEmail`; đơn user cũ vẫn theo `CustomerId`. Guest session chỉ đọc guest orders cùng normalized email. |
| Shop → storefront payment settings | 1–1 | Một hàng singleton, version concurrency; dữ liệu chuyển khoản là thông tin công khai. |
| Email → OTP challenge | 1–N history | Giữ hash, thời điểm tạo/hết hạn, số lần thử, dùng/invalidated; code email chỉ được giữ mã hóa trong outbox đến khi gửi. |

Thêm cột `IsGuestOrder` (mặc định false), `NormalizedEmail` (backfill từ Email, index theo email/thời gian) trên Ordering.Orders. Thêm `GuestEmailChallenges` và `StorefrontPaymentSettings`; không cascade xóa order/history. Challenge cũ được dọn sau thời hạn lưu ngắn; settings là singleton với ID cố định. Thêm migrations additive SQL Server và PostgreSQL, không sửa baseline.

Guest cart ID và email access cookie được encrypt/sign với purpose tách biệt bằng Data Protection. Cookie có `HttpOnly`, `SameSite=Lax`, 30 ngày và `Secure` trên HTTPS. OTP sáu chữ số, TTL 10 phút, năm lần thử; gửi lại tối thiểu sau 60 giây, tối đa năm lần/giờ/email và áp dụng authentication IP limiter. Code verifier dùng keyed hash/constant-time compare; mã trong outbox được bảo vệ và không ghi log.

## 3. API và quyền

Giữ `/api/cart` và `/api/orders/*` hiện hành cho Identity. Thêm các endpoints công khai `/api/storefront/cart` GET/PUT, `/api/storefront/access/request-code`, `/verify-code`, `/api/storefront/checkout`, `/api/storefront/orders` GET/`{id}` GET/`{id}/cancel` POST/`{id}/shipments` GET/`{id}/digital-deliveries` GET. Cookie cart tạo customer GUID ẩn danh; checkout yêu cầu email session cookie đã xác minh, email checkout phải khớp email đã xác minh. Checkout dùng cùng Order gate, version, snapshot, `OperationId`, price/stock lock, reservation và rollback.

Thêm `FulfillmentKind` cho CartLineDto và `HasDigitalItems` cho CartDto. Checkout trả trước bị từ chối nếu chuyển khoản đang tắt/chưa cấu hình; COD bị domain từ chối nếu có bất cứ dòng không Physical. Guest order read/cancel/shipment/delivery kiểm `IsGuestOrder && NormalizedEmail == verifiedEmail`; mismatch trả 404, delivery yêu cầu Paid, mọi response nhạy cảm `no-store`.

Thêm `/api/storefront/settings` GET chỉ trả bank name/account number/account holder/branch/instructions và trạng thái chuyển khoản. Admin GET/PUT `/api/admin/storefront-settings` yêu cầu permission mới `StorefrontSettings.View`/`StorefrontSettings.Update`; owner/editor cần được đăng ký chủ động qua capability CLI như các quyền khác. Admin cập nhật bằng Version. Settings có ngân hàng, số tài khoản, chủ tài khoản bắt buộc khi bật, chi nhánh/hướng dẫn optional, bật/tắt.

## 4. Triển khai và kiểm chứng

| Bước | Đầu ra | Trạng thái |
|---|---|---|
| Mô hình/domain và EF config | guest order/email, OTP challenge, settings singleton, mapping/query indexes | Đã triển khai |
| Service/controller/security | cart cookie, OTP/outbox/rate limit, checkout/read/cancel/delivery/settings | Đã triển khai và qua integration + HTTP live SQL Server |
| Migrations hai provider | migration SQL Server + PostgreSQL và snapshot đồng bộ | Đã sinh và kiểm tra snapshot; SQL Server fresh DB đã migrate, PostgreSQL runtime chờ test connection |
| Tests | OTP lifecycle, cookie ownership/isolation, pricing/stock/idempotency, COD restriction, delivery Paid gate, migrations | SQL Server integration đạt 5/5; 12/12 live browser/API checks đạt trên bốn cấu hình. PostgreSQL chưa có test connection |
| Contract/roadmap/evidence | API + database docs, roadmap, isolated live test results | Đã cập nhật; [báo cáo nghiệm thu](../../../../Frontend/docs/verification/2026-10-24-guest-storefront.md) |

Test integration không chạy vào production: dùng database `boilerate_test_*`. SQL Server là runtime acceptance target hiện tại; PostgreSQL migration và integration chỉ được xác nhận khi `BOILERATE_TEST_POSTGRES` test connection khả dụng.

### 2026-10-24 — Triển khai và nghiệm thu SQL Server

- Đã chốt guest accountless flow và các API tương thích với Ordering hiện hành.
- Kiểm chứng: đọc Order/Cart transaction, identity owner filter, public Catalog API, mail outbox, capability registration và provider migration layout.
- Đã thêm guest cart và email access cookie Data Protection 30 ngày, OTP/outbox, ordering cùng quy tắc khóa giá/tồn/idempotency, guest read/cancel/shipping/delivery, payment settings Admin/public và migrations additive cho hai provider.
- SQL Server integration đạt 5/5; non-provider BE đạt 233/233. Live acceptance đạt 12/12 trên DB `boilerate_test_*`, desktop/tablet × Light/Dark: OTP qua outbox mail sink, COD/My Orders, digital chỉ hiện sau staff xác nhận tiền, và nhân viên cập nhật settings qua Admin.
- Migration mới `20261024082919_GuestStorefront` (SQL Server) và `20261024082938_GuestStorefront` (PostgreSQL). SQL Server fresh DB đã chạy migration; runtime PostgreSQL chưa chạy do không có test connection.
- SMTP thật và deployment staging là gate riêng, không nằm trong nghiệm thu này.
