# Guest storefront checkout — điều phối BE–FE

Ngày bắt đầu: 2026-10-24
Cập nhật gần nhất: 2026-10-24
Trạng thái: Đã triển khai; SQL Server live acceptance đạt, PostgreSQL runtime còn mở
Khu vực phụ trách: Shared
Mốc roadmap: [BE MVP](../../backend/roadmap.md#việc-ưu-tiên-tiếp-theo), [FE storefront](../../../../Frontend/PRODUCT.md#mốc-sau)
Hồ sơ liên quan: [BE](../../backend/features/guest-storefront.md), [FE](../../../../Frontend/docs/features/storefront.md)

## Mục tiêu và phạm vi

Khách mua được sản phẩm trong shop một người bán mà không cần tạo tài khoản. Khách xác minh email trước khi tạo đơn; email đã xác minh cho phép xem đơn của chính địa chỉ đó và nhận nội dung số sau khi đã trả tiền.

Trong mốc này có catalog công khai, giỏ ẩn danh trên server, checkout COD cho hàng vật lý, trả trước cho hàng số/mixed, My Orders qua OTP, hủy đơn hợp lệ và thông tin chuyển khoản cấu hình trong Admin. Không có đăng ký tài khoản khách, QR, gateway, carrier tự động hay mobile optimization.

## Quyết định nghiệp vụ

- Cart cookie và cookie truy cập đơn là hai quyền riêng biệt, đều được bảo vệ bằng ASP.NET Data Protection, `HttpOnly`, `SameSite=Lax`, và `Secure` khi request HTTPS. Guest cart cookie tồn tại 30 ngày; email session sau OTP tồn tại 30 ngày và chỉ chứa địa chỉ email chuẩn hóa.
- OTP sáu chữ số hết hạn sau 10 phút, tối đa năm lần thử; giới hạn gửi lại tối thiểu một phút và tối đa năm yêu cầu mỗi giờ cho một email. Endpoint trả thông báo chung, không tiết lộ trạng thái đơn hay email.
- OTP được xác minh trước checkout. Đơn guest được truy vấn theo email chuẩn hóa đã xác minh; endpoint chi tiết, shipment, hủy và digital delivery vẫn kiểm quyền ở BE. Tài khoản đã có tiếp tục dùng contract đăng nhập hiện hành.
- Giá, tồn, phí ship, reservation, COD-vật-lý-only và idempotency được BE quyết định bằng cùng quy tắc Ordering hiện tại. Conflict về version/tổng/tồn giữ giỏ; FE yêu cầu khách xem lại tổng trước request mới.
- Một cài đặt thanh toán chuyển khoản cho shop. Thông tin công khai gồm ngân hàng, số tài khoản, chủ tài khoản, chi nhánh và hướng dẫn; chưa có QR. Chuyển khoản chỉ hoạt động khi cấu hình được bật và hợp lệ. Nhân viên xác nhận khoản tiền thực nhận.

## Kế hoạch phối hợp

| Bước | Đầu ra | Trạng thái |
|---|---|---|
| BE — session giỏ khách, OTP, checkout/read/cancel, settings và migrations hai provider | Source/migrations xong; SQL Server integration đạt 5/5 và non-provider BE đạt 233/233; PostgreSQL runtime chờ test connection | Đã kiểm chứng SQL Server |
| FE — nối storefront đang có với API guest và màn settings | Catalog → cart → verify email → checkout → My Orders; FE check 269/269, 16 mocked E2E đạt bốn cấu hình; không gửi token qua Web Storage | Đã kiểm chứng |
| Tích hợp | SQL Server isolated, desktop/tablet × Light/Dark, OTP outbox mail sink; 12/12 live browser/API, gồm Admin settings, COD/My Orders và digital sau khi xác nhận tiền | Đạt trên SQL Server |
| Tài liệu và roadmap | API/database BE, hành vi FE, roadmap và history chỉ khi mốc đã hoàn tất; [báo cáo nghiệm thu](../../../../Frontend/docs/verification/2026-10-24-guest-storefront.md) | Đã cập nhật |

## Tiêu chí chấp nhận

- [x] Khách không đăng nhập duyệt Published catalog, lưu giỏ qua cookie và xác minh email trước khi tạo đơn.
- [x] Đơn COD chỉ chứa hàng vật lý; BE kiểm giá/tồn, giữ hàng nguyên tử và replay cùng `OperationId`; có kiểm COD với loại fulfillment khác Physical.
- [x] Cookie email lọc theo email chuẩn hóa; sai email trả 404; có kiểm OTP sai/hết hạn/giới hạn và delivery chỉ sau Paid.
- [x] Admin settings có quyền View/Update; storefront chỉ nhận data chuyển khoản công khai, và checkout trả trước bị từ chối khi chưa bật/cấu hình.
- [x] Browser/live acceptance đạt desktop/tablet và Light/Dark trên SQL Server cô lập với OTP qua mail sink; migration cho SQL Server và PostgreSQL đã scaffold. PostgreSQL runtime, SMTP thật và deployment staging còn mở.

## Nhật ký triển khai

### 2026-10-24 — Triển khai và nghiệm thu SQL Server

- Đã chốt guest checkout, OTP trước checkout, settings Admin không QR, và giữ xác nhận tiền thủ công theo quyết định của người dùng.
- BE/FE source và migrations SQL Server/PostgreSQL đã được triển khai; SQL Server integration đạt 5/5 và live storefront đạt 12/12 trên database test `boilerate_test_*`.
- OTP đã đi qua API, outbox và in-memory mail sink; E2E live xác nhận COD, My Orders, nội dung số sau xác nhận thanh toán thủ công và cấu hình Admin.
- PostgreSQL runtime chưa chạy do không có test connection. SMTP thật và deployment staging là gate riêng. Mock E2E được ghi riêng, không thay cho live acceptance.
