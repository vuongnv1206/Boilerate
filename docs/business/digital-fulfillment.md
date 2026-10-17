# Giao hàng số và gia hạn — quyết định nghiệp vụ

Cập nhật: 2026-10-18, Asia/Saigon. Nguồn: câu trả lời của người dùng trong [phỏng vấn nghiệp vụ](../features/business-discovery.md). Đây là quy tắc đã xác nhận cho các kịch bản được nêu, **chưa phải luồng đã triển khai hoặc danh sách bắt buộc của MVP**.

## Nội dung khách nhận và số lượng

| Ví dụ sản phẩm | Cách cấp phát | Khi khách mua số lượng 2 |
|---|---|---|
| Mã kích hoạt khóa học | Unique: mỗi mã chỉ cấp một lần | Hai mã khác nhau |
| Link khóa học | Shared: cùng link giao cho nhiều đơn | Hiển thị cùng một link, không cần nhân đôi nội dung |
| Tài khoản có sẵn | Unique: mỗi tài khoản chỉ cấp một lần | Hai tài khoản khác nhau |
| Gói gia hạn tài khoản | Extension: yêu cầu gia hạn cho cùng tài khoản | Nếu dịch vụ cho cộng dồn, 2 × 30 ngày tương đương thêm 60 ngày; kết quả thực tế phải được xác minh |

Unique/Shared/Extension là nhãn mô tả nghiệp vụ, chưa phải enum hoặc database schema. Không suy ra Shared có giá bằng 0 khi tăng số lượng, bán không giới hạn hoặc có hai chỗ học; các chính sách đó còn mở.

Sau thanh toán thành công, mã/link/tài khoản có sẵn được lấy từ kho hàng số, gán cho đơn, hiển thị trong **Đơn hàng của tôi** và gửi email thông báo. Nội dung email chưa chốt; chưa mặc định gửi toàn bộ thông tin đăng nhập qua email.

Không xây LMS và không tích hợp API cấp quyền bên ngoài cho những kịch bản này. Đã chốt đơn có hàng số phải trả trước toàn bộ, kể cả đơn chung với hàng vật lý; COD chỉ dành cho đơn thuần vật lý. Cách xác nhận thanh toán và payment provider vẫn chưa được chọn. Xem [đơn mua chung](product-flexibility.md).

## Gia hạn do nhân viên thực hiện

- Thanh toán thành công và gia hạn thành công là hai kết quả độc lập.
- Dịch vụ bên ngoài quyết định thời hạn thực tế. Shop chỉ lưu thông tin đã xác minh; không tự cộng ngày rồi đánh dấu hoàn tất.
- Khi dịch vụ có quy tắc khác cộng dồn, phải theo kết quả thực tế của dịch vụ. Chưa chốt chính sách xử lý kết quả khác gói đã bán.
- Nhân viên cần lưu lịch sử thao tác, thời điểm hoàn tất, hạn mới và bằng chứng nếu có.
- Phải ngăn hai nhân viên cùng xử lý một yêu cầu và ngăn thực hiện gia hạn trùng khi thao tác lại.

### Kết quả chưa xác định

Tình huống: nhân viên A đã thao tác trên dịch vụ ngoài nhưng mất kết nối trước khi lưu kết quả ở shop. Nhân viên B nhìn thấy yêu cầu chưa hoàn tất.

**Đã chốt:** đưa yêu cầu vào `PendingVerification` (Chờ xác minh). Tuyệt đối không được thực hiện gia hạn lại khi chưa xác định được kết quả thao tác trước. Việc hết phiên xử lý hoặc bàn giao không được hiểu là bằng chứng lần trước chưa thực hiện.

Đây là tên trạng thái nghiệp vụ được người dùng xác nhận; chưa có state machine/API/persistence tương ứng trong code.

## Quyết định còn mở

| Chủ đề | Cần làm rõ trước khi triển khai |
|---|---|
| Kho Unique | Khi giữ mã/tài khoản, thời hạn giữ, giải phóng và xử lý đã thu tiền nhưng thiếu hàng |
| Shared | Cách tính tiền theo số lượng, quyền nhận được, giới hạn bán và thay link sau khi đã giao |
| Gia hạn | Nhận diện tài khoản đích; tài khoản hết hạn; hai đơn gia hạn khác nhau cho cùng tài khoản; kết quả thiếu/một phần/thất bại và hoàn tiền |
| Quyền xử lý | Ai được xác minh, xác nhận kết quả, bàn giao hoặc cho phép thực hiện tiếp sau đối chiếu; chưa mặc định chỉ quản lý |
| Truy cập | Có bắt buộc đăng nhập không; nội dung email; quyền xem lại và bảo vệ nội dung hàng số |
| Thanh toán | Phương thức, nguồn xác nhận tiền, thời điểm cấp phát và ngoại lệ thanh toán |

## Bước tiếp theo và kiểm chứng

Đã chốt [mức cấu hình quản trị và giỏ/đơn chung](product-flexibility.md). Tiếp theo chốt giữ hàng/cam kết hàng, các ngoại lệ và khả năng cần ở MVP; sau đó mới viết use case, state transitions, cardinality/ownership/constraints, API/quyền, transaction/idempotency và các ca nghiệm thu. ProductType hiện hành quyết định bộ trường nhập liệu; không đồng nhất nó với cách giao hàng.

Hiện chỉ đối chiếu tài liệu với câu trả lời. Chưa có code, migration, kiểm thử hoặc nghiệm thu runtime cho các luồng giao hàng số/gia hạn này. Các kiểm thử Catalog trước đây không xác nhận chúng.
