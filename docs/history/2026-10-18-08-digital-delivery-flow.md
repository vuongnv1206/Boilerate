# Chốt luồng giao hàng số sau thanh toán

Ngày: 2026-10-18, Asia/Saigon.

1. Người dùng xác nhận: thanh toán thành công thì tự lấy mã/link/thông tin tài khoản từ kho hàng số, gán cho đơn, hiển thị trong Đơn hàng của tôi và gửi email thông báo.
2. Không cần LMS/trình học trực tuyến hoặc API cấp quyền bên thứ ba cho luồng giao hàng số. Chưa diễn giải điều này thành quyết định loại payment gateway; phương thức/nguồn xác nhận tiền chưa được hỏi xong.
3. Đã mở vòng 1C: bản ghi dùng một lần hay dùng chung; mua số lượng 2 là hai nội dung riêng hay gia hạn/quyền sử dụng. Các quy tắc giữ hàng, thiếu hàng sau thanh toán, nội dung email và retry còn mở.

Không sửa code, schema hoặc API. Tài liệu [business discovery](../features/business-discovery.md) và [roadmap](../roadmap.md) phân biệt quyết định đã chốt với câu hỏi/đề xuất. Tiếp theo: nhận chính sách tiêu thụ kho hàng số trước khi chốt reservation và luồng lỗi.
