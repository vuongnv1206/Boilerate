# 2026-10-18 — Bước 16: thiết kế nhận tiền và giao hàng số

- Yêu cầu: tiếp tục nhận tiền/giao số, cập nhật docs từng bước và chia commit.
- Thiết kế: [feature plan](../features/payment-digital-delivery.md) ghi ownership/cardinality, receipt full-amount, snapshot giao số mã hóa, chống lặp, khóa/transaction, late payment, ACL, email outbox và migrations hai DB.
- Nguồn nhận tiền mốc đầu: nhân viên xác nhận khoản chuyển khoản đã kiểm tra; chưa có gateway được chọn/nghiệm thu.
- File: feature plan, chỉ mục history, navigation roadmap/shop MVP.
- Kiểm chứng: đọc code/docs hiện hành; chưa build/test hoặc tạo migration mới tại bước thiết kế.
- Tiếp theo: triển khai domain/persistence và use case; sau đó email và regression. Không đánh dấu deployment hoặc toàn bộ MVP hoàn tất.
