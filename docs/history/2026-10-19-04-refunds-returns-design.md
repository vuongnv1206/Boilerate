# 2026-10-19 — Thiết kế hoàn tiền và nhận hàng trả

- Đọc AGENTS/docs/roadmap/MVP và code receipt/shipment/reservation/renewal trước thay đổi theo yêu cầu tiếp tục.
- [Hồ sơ](../features/refunds-returns.md) ghi mục tiêu, cardinality/ownership/cột/PK/FK/check/index, concurrency/delete/migration và API/ACL trước code. Refund ghi tiền đã hoàn, return ghi hàng thực tế; restock có quyền riêng.
- Đề xuất tối giản: full refund Closed/Stopped, chỉ nhả phần chưa xuất/chưa giao; pending renewal không mở lại. Partial refund không tự dừng giao; return không tự sửa shipment/refund. COD chưa thu có thể đóng trong return cuối khi đã nhận đủ mọi món đã xuất.
- Review nhận diện Release cũ không dùng được sau partial dispatch và query cần tránh Unique Delivered. Thêm reservation Closed4/CloseRemainder, chỉ release Unique Reserved; không sửa lịch sử đã xuất.
- Bước này chỉ docs, chưa code/migration/test mới. Tiếp theo domain/persistence→use case/API→kiểm thử hai DB, commit theo phần build được.
