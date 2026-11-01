# Mã giảm giá cho đơn hàng

Ngày bắt đầu: 2026-10-30  
Cập nhật gần nhất: 2026-10-30  
Trạng thái: Đang triển khai  
Khu vực phụ trách: Shared, BE và FE

## Mục tiêu và phạm vi

Chủ shop quản lý mã giảm giá; khách áp dụng một mã vào giỏ và xem tổng trước khi xác nhận. Server quyết định điều kiện, số tiền và snapshot trên đơn. Phiên bản đầu hỗ trợ giảm phần trăm hoặc VND cố định, giá trị tiền hàng tối thiểu, thời gian hiệu lực và giới hạn tổng lượt tùy chọn. Giảm giá chỉ áp dụng tiền hàng, không áp dụng phí vận chuyển; không cho tổng tiền hàng sau giảm bằng 0. Mã dùng chung cho mọi sản phẩm trong giỏ, không cộng dồn.

Hoãn miễn phí vận chuyển, phân khúc khách, giới hạn theo khách, phạm vi sản phẩm, điểm thưởng và chiến dịch tự động. Guest checkout tiếp tục xác minh email trước đặt hàng; COD chỉ dùng cho giỏ thuần vật lý. Giao diện dùng chuẩn [DESIGN.md](../../../../Frontend/DESIGN.md).

## Quy tắc và hợp đồng phối hợp

- Nhập mã không phân biệt hoa/thường; mã đã gắn đơn giữ snapshot, không thay đổi hồi tố khi cấu hình mã được sửa.
- Quote trên cart để hiển thị; checkout tính lại dưới transaction, kiểm tra version và tổng mong đợi. Giữ giỏ và yêu cầu khách xem lại khi giá hoặc điều kiện thay đổi. Retry cùng operation/payload không tạo đơn hay tiêu thụ lượt lần hai.
- Lượt được ghi nguyên tử khi tạo đơn. Hủy hoặc hết hạn đơn chưa thanh toán giải phóng lượt một lần; đơn đã nhận tiền hoặc hoàn tiền giữ lịch sử. Thanh toán đến muộn sau khi đã nhả lượt phải bảo toàn receipt và bảo vệ quota, không âm thầm vượt hạn mức; chi tiết xử lý được BE ghi trong hợp đồng triển khai.
- Phân bổ phần giảm trên dòng hàng theo số nguyên VND và thứ tự xác định; số tiền nhận/hoàn vẫn lấy từ receipt/refund thực tế, không tính lại lịch sử.
- Admin dùng quyền `Discount.View`, `Discount.Create`, `Discount.Update`; không cấp grant ngầm khi startup. Dữ liệu mã đã dùng không bị hard-delete cùng lịch sử đơn.
- Admin dùng `GET/POST /api/admin/discounts`, `GET/PUT /api/admin/discounts/{id}`. Cart dùng `PUT /api/storefront/cart/discount` cho guest và `PUT /api/cart/discount` cho khách đăng nhập, body `{version, code}`; code null hoặc rỗng bỏ mã. DTO cart/đơn bổ sung mã và số tiền giảm, cart có trạng thái lỗi mã. Xem trường dữ liệu, điều kiện và lỗi tại [API commerce](../../backend/commerce-api.md#mã-giảm-giá).

## Trình tự và nghiệm thu

1. Chốt invariant, hợp đồng API, transaction và thiết kế lưu trữ trong hồ sơ BE; tạo migrations SQL Server/PostgreSQL tương ứng.
2. Triển khai quản lý, quote, checkout, quota, hủy/hết hạn và receipt đến muộn; kiểm domain, concurrency, idempotency, rollback và phân quyền.
3. Triển khai danh sách/form Admin và cart/checkout/chi tiết đơn; kiểm loading, empty, lỗi, validation, quyền, dirty/conflict và thao tác bàn phím.
4. Chạy FE check, BE regression, browser E2E và SQL Server live trên database thử nghiệm cô lập; review desktop 1440×900/tablet 1024×768, Light/Dark/System. Chỉ cập nhật baseline sau khi xem ảnh.
5. Cập nhật hợp đồng, tài liệu vận hành và bằng chứng đúng revision. Backup/verify trước migration lên database local; không thay grant hiện có tự động. PostgreSQL runtime chỉ ghi nhận đạt khi chạy thật; staging, SMTP thật và mobile không được suy từ local.

Hồ sơ BE sở hữu chi tiết DB/API; tài liệu FE sở hữu tương tác và trạng thái. Quy tắc có thể thay đổi theo quyết định người dùng trước khi áp dụng trên môi trường vận hành.
