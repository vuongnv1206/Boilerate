# 2026-10-18 — Bước 17: nhận tiền và tự động giao Unique/Shared

- Thay đổi: PaymentReceipt append-only, PaymentStatus/FulfillmentStatus độc lập; staff xác nhận đủ tiền có ACL/version/idempotency; snapshot DigitalDelivery mã hóa; owner-only đọc nội dung có audit/no-store. Trả muộn preflight mọi dòng, thiếu hàng giữ Paid+NeedsReview; retry riêng có quyền. Physical vẫn giữ hàng, Extension chưa được đánh dấu đã gia hạn.
- Review: bổ sung FulfillmentKind vào cả composite FK của delivery để Unique không thể gán thành Shared vượt unique index. Chuẩn bị toàn bộ ciphertext trước thay đổi hold; lỗi crypto giữ receipt/Paid và chờ sửa keys/nội dung, không cấp một phần.
- File: Domain Payments/Ordering/DigitalInventory, Application Ordering, Infrastructure OrderingPayments/persistence/audit, Host PaymentsController, ACL/bootstrap; migration `20261018144205` SQL Server và `20261018144215` PostgreSQL.
- Kiểm chứng đã chạy: build Release tests project, 0 lỗi (còn analyzer/style warnings); scaffold hai migrations đạt. Lượt đầu49/51 pass,2 failure vì assertion hủy Paid sai loại lỗi; đã sửa và chạy lại bộ theo class48/48 pass (gồm hai ca đó). Chi tiết phạm vi/lệnh và khác biệt filter ở [báo cáo](../verification/2026-10-18-payment-delivery.md).
- Chưa triển khai tại bước này: email outbox; gateway/webhook, refund, shipping và thao tác gia hạn. SMTP/deployment chưa nghiệm thu.
- Tiếp theo: hoàn tất kiểm chứng hai DB, commit lát nhận tiền/giao số; sau đó email outbox trong commit riêng.
