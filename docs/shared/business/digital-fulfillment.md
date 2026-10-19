# Giao hàng số và gia hạn — quyết định nghiệp vụ

Cập nhật: 2026-10-19, Asia/Saigon. Nguồn: câu trả lời của người dùng trong [phỏng vấn nghiệp vụ](../features/business-discovery.md). Đây là nguồn quyết định nghiệp vụ; trạng thái triển khai và kiểm chứng được theo dõi trong từng hồ sơ tính năng, không suy ra từ việc ghi quy tắc.

Trạng thái code mới nhất: đã có [kho số](../../backend/features/digital-inventory.md), [giỏ/đơn/giữ hàng](../../backend/features/ordering-checkout.md), [nhận tiền/giao Unique/Shared/email outbox](../../backend/features/payment-digital-delivery.md) và [giao vật lý/COD](../../backend/features/physical-shipping.md), có bằng chứng local riêng. [Gia hạn thủ công](../../backend/features/manual-renewals.md) đã triển khai và kiểm chứng local theo [báo cáo](../../backend/verification/2026-10-19-manual-renewals.md); runtime/adapter thật chưa nghiệm thu. Các đoạn quy tắc dưới đây giữ quyết định nghiệp vụ; không tự coi mọi kịch bản là đã được nghiệm thu.

## Nội dung khách nhận và số lượng

| Ví dụ sản phẩm | Cách cấp phát | Khi khách mua số lượng 2 |
|---|---|---|
| Mã kích hoạt khóa học | Unique: mỗi mã chỉ cấp một lần | Hai mã khác nhau |
| Link khóa học | Shared: cùng link giao cho nhiều đơn | Hiển thị cùng một link, không cần nhân đôi nội dung |
| Tài khoản có sẵn | Unique: mỗi tài khoản chỉ cấp một lần | Hai tài khoản khác nhau |
| Gói gia hạn tài khoản | Extension: yêu cầu gia hạn cho cùng tài khoản | Nếu dịch vụ cho cộng dồn, 2 × 30 ngày tương đương thêm 60 ngày; kết quả thực tế phải được xác minh |

Code dùng ProductFulfillmentKind Physical/DigitalUnique/DigitalShared/ManualExtension, một cách giao bất biến cho mỗi Product, SKU kế thừa. Shared tính tiền UnitPrice × Quantity nhưng giao một snapshot/link; sửa link trong kho không đổi nội dung đã giao. Không suy ra hai chỗ học hoặc quyền khác ngoài mô tả gói shop bán.

Sau xác nhận tiền, mã/link/tài khoản được lấy từ kho số, gán snapshot cho đơn và đọc qua API **Đơn hàng của tôi** chỉ bởi khách đã đăng nhập sở hữu đơn, có Read audit. Email qua outbox chỉ thông báo đã giao, không chứa credentials; SMTP thật chưa nghiệm thu.

Không xây LMS hoặc API cấp quyền bên ngoài. Đơn có hàng số trả trước toàn bộ, kể cả đơn chung vật lý; COD chỉ thuần vật lý. Mốc hiện tại nhận tiền bằng xác nhận chuyển khoản thực nhận có quyền, hoặc COD sau giao đủ; chưa chọn gateway tự động. Xem [đơn mua chung](product-flexibility.md).

## Gia hạn do nhân viên thực hiện

- Thanh toán thành công và gia hạn thành công là hai kết quả độc lập.
- Dịch vụ bên ngoài quyết định thời hạn thực tế. Shop chỉ lưu thông tin đã xác minh; không tự cộng ngày rồi đánh dấu hoàn tất.
- Khi dịch vụ có quy tắc khác cộng dồn, phải theo kết quả thực tế của dịch vụ. Chưa chốt chính sách xử lý kết quả khác gói đã bán.
- Nhân viên cần lưu lịch sử thao tác, thời điểm hoàn tất, hạn mới và bằng chứng nếu có.
- Phải ngăn hai nhân viên cùng xử lý một yêu cầu và ngăn thực hiện gia hạn trùng khi thao tác lại.

### Kết quả chưa xác định

Tình huống: nhân viên A đã thao tác trên dịch vụ ngoài nhưng mất kết nối trước khi lưu kết quả ở shop. Nhân viên B nhìn thấy yêu cầu chưa hoàn tất.

**Đã chốt:** đưa yêu cầu vào `PendingVerification` (Chờ xác minh). Tuyệt đối không được thực hiện gia hạn lại khi chưa xác định được kết quả thao tác trước. Việc hết phiên xử lý hoặc bàn giao không được hiểu là bằng chứng lần trước chưa thực hiện.

Thiết kế [gia hạn thủ công](../../backend/features/manual-renewals.md) hiện lưu PendingVerification ngay khi nhân viên bấm Start, trước hành động ngoài. Lease chỉ dành cho bước nhận việc chưa Start. Verify riêng mới được kết luận Applied hoặc NotApplied; chưa coi mất kết nối hoặc hết lease là bằng chứng thất bại.

## Quyết định còn mở

| Chủ đề | Cần làm rõ trước khi triển khai |
|---|---|
| Kho Unique | Đã triển khai giữ30phút/cấu hình, nhả hết hạn và trả muộn giữ lại toàn bộ; mã đã giao không tự trả về Available |
| Shared | Đã có giá theo Quantity và snapshot một link; quyền ngoài dịch vụ theo mô tả gói, chưa tích hợp xác minh |
| Gia hạn | Thu định danh sau Paid; serialize cùng canonical target; lưu expiry thực tế còn hiệu lực; kết quả thiếu/một phần hoặc hoàn tiền cần review ở mốc refund |
| Quyền xử lý | Process/Verify/Reveal riêng; Verify có thể đối chiếu của người khác, không hardcode quản lý; thao tác mới chỉ sau NotApplied có chứng cứ |
| Truy cập | Dùng khách đăng nhập/owner-only, API reveal audit, email thông báo không credentials; frontend My Orders chưa triển khai trong BE slice |
| Thanh toán | Chuyển khoản/COD xác nhận có quyền đã có; gateway/webhook/refund và adapters thật còn mở |

## Bước tiếp theo và kiểm chứng

Đã chốt [mức cấu hình quản trị, giỏ/đơn chung và cam kết hàng](product-flexibility.md). Theo chỉ dẫn shop nhỏ, chuyển sang [MVP gọn](../../backend/features/shop-mvp.md), thiết kế/code từng lát thay vì tiếp tục hỏi sâu mọi ngoại lệ. Trước khi đổi database/API vẫn ghi rõ use case, cardinality/ownership/constraints, quyền, transaction/idempotency và ca nghiệm thu. ProductType hiện hành quyết định bộ trường nhập liệu; không đồng nhất nó với cách giao hàng.

Theo dõi kết quả thực tế trong từng feature plan và `docs/backend/verification/`; kiểm chứng giao diện sau này thuộc `docs/frontend/verification/`. Bằng chứng local cho các bước trước không tự nghiệm thu code gia hạn mới, gateway/SMTP, FE hoặc Linux/Docker deployment.
