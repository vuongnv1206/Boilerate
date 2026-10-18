# 2026-10-18 — Bước 18: email thông báo giao hàng số qua outbox

- Sau core `533b681`, thêm producer `DigitalDeliveryReady` v1 trong cùng UoW với receipt/đơn/delivery. Một thông báo cho mỗi lần giao tự động của đơn; retry confirmation hoặc đơn đã giao không phát sinh lần nữa. Đơn NeedsReview/chưa giao không tạo thông báo.
- Consumer gửi qua IMailService ngoài transaction; chỉ tải email/số đơn và sự tồn tại của delivery, không tải ProtectedContent. Payload chỉ OrderId, email hướng dẫn xem Đơn hàng của tôi, không gửi mã/mật khẩu/link bí mật.
- Dùng claim/lease/retry/dead-letter/replay của worker hiện hữu. Message-Id ổn định theo outbox, nhưng SMTP vẫn at-least-once: crash sau gửi/trước ack có thể lặp thông báo, không cấp lại hàng.
- File: Domain/Messaging/OutboxMessage, Commerce/OrderingPayments và OutboxProcessor, helper tests, PaymentEmailTests và test rollback thực tế của producer.
- Kiểm chứng: build Release cuối0 lỗi; regression276/276 đạt, gồm8 email provider cases và2 commit-failure producer cases. Bằng chứng/lệnh/phạm vi trong [báo cáo](../verification/2026-10-18-payment-delivery.md). Không sử dụng SMTP/network thật trong tests; không tuyên bố adapter đã nghiệm thu.
- Tài liệu: đồng bộ API/database/runbook, feature plan/shop MVP/roadmap và navigation. Tiếp theo: commit email riêng; fix UTC tách commit tiếp nối, sau đó chọn gateway/nghiệm thu SMTP khi có cấu hình thực; quản trị giao vật lý/gia hạn/hoàn tiền ở mốc sau.
