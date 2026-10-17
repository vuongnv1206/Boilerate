# 2026-10-18 — Chốt kịch bản cấp phát và gia hạn

## Mục tiêu và quyết định

Ghi các câu trả lời vòng 1C–1E vào tài liệu trong lúc phỏng vấn; chưa triển khai nghiệp vụ mới. Đây là quyết định cho các kịch bản người dùng nêu, chưa cam kết tất cả vào MVP.

- Mã/tài khoản có sẵn là Unique; mua 2 nhận hai bản ghi khác nhau. Link là Shared, không nhân đôi nội dung khi mua 2. Gói gia hạn là Extension cho cùng tài khoản.
- Thanh toán và gia hạn thành công độc lập. Nhân viên xử lý ở dịch vụ ngoài; chỉ lưu hạn đã xác minh, lịch sử, thời điểm hoàn tất và bằng chứng nếu có.
- Chống hai nhân viên xử lý đồng thời một yêu cầu và thực hiện trùng khi thử lại. Kết quả thao tác chưa rõ phải PendingVerification; cấm gia hạn lại cho đến khi xác định được kết quả trước.
- Chưa chốt ai có quyền xác minh/bàn giao, giá/quyền của Shared, thời điểm giữ kho Unique hoặc chính sách hoàn tiền. Không tự chọn công thức tính hạn.

## File liên quan

- [Quy tắc hàng số](../business/digital-fulfillment.md): quy tắc và các mục còn mở.
- [Feature plan](../features/business-discovery.md), [roadmap](../roadmap.md) và [index](../README.md): đồng bộ tiến độ khám phá.
- [History index](README.md): liên kết bản ghi này.

## Kiểm chứng

Đã đối chiếu nội dung với câu trả lời trực tiếp của người dùng. `git diff --check` đạt; kiểm tra 34 file Markdown/127 relative links đạt. Đây là thay đổi docs; không chạy build/test ứng dụng, chưa có kiểm chứng thực thi hoặc nghiệm thu deployment cho luồng hàng số.

## Còn lại và bước tiếp

Làm rõ mong muốn linh hoạt sản phẩm, các nhóm hàng cần ở MVP và hành trình mua trước khi chốt use case/database/API. Sau đó hỏi giữ hàng, xác nhận tiền, ngoại lệ và quyền xử lý. Các gate vận hành hiện có vẫn còn mở trong roadmap.
