# 2026-10-18 — Bước 19: nhất quán timestamp UTC trong response đơn hàng

Kế hoạch sửa nhỏ thuộc [nhận tiền/giao số](../features/payment-digital-delivery.md), ghi trước khi sửa code.

- Mục tiêu: thời điểm PaidAtUtc, ExpiresAt và CreatedOn của OrderDto/OrderSummary được serialize với hậu tố `Z` như hợp đồng UTC trên cả SQL Server và PostgreSQL. SQL Server `datetime2` trả về DateTime.Kind=Unspecified dù giá trị được lưu theo UTC; không được diễn giải thành giờ local.
- Phạm vi: chuẩn hóa Kind tại mapping response trong OrderingService; không đổi giá trị thời gian, database/migration, lịch sử receipt/delivery hoặc quy tắc nhận tiền.
- Các bước: (1) bổ sung mapping UTC cho chi tiết và danh sách sau khi query trang đã materialize; (2) thêm một theory hai provider seed Order/PaymentReceipt bằng domain; (3) kiểm Kind và JSON PaidAtUtc/ExpiresAt có `Z`; (4) root chạy build/regression, ghi kết quả thực tế và commit riêng.
- Tiêu chí chấp nhận: query owner detail/list giữ nguyên instant và có Kind=Utc; CreatedOn của summary cũng Utc; JSON không có timestamp thiếu timezone; null timestamp giữ null; query hoạt động trên hai provider mà không phụ thuộc SQL dịch helper.
- File dự kiến: `src/Infrastructure/Commerce/OrderingService.cs`, `tests/Boilerate.Tests/PaymentTimestampTests.cs`, entry này.
- Kiểm chứng tại lúc lập kế hoạch: chưa sửa/chưa chạy build hoặc test cho fix. Không có thay đổi nghiệm thu deployment/adapter.
- Quyết định còn mở: không có; dữ liệu thương mại hiện hành đã quy ước lưu UTC. Root sẽ cập nhật hồ sơ tính năng, index history và kết quả sau khi kiểm chứng.

Đã triển khai: mapping chi tiết chuẩn hóa PaidAtUtc/ExpiresAt; danh sách chuẩn hóa thêm CreatedOn sau paging/materialization để không đưa helper vào SQL. Đã thêm theory hai provider kiểm instant/kind và JSON `Z`, seed receipt qua domain. Không có migration hoặc sửa lịch sử đã lưu.

Kiểm chứng đã thực hiện: build Release0 lỗi (2630 warnings analyzer/style vẫn còn); regression276/276 đạt, gồm PaymentTimestampTests2/2 trên SQL Server/PostgreSQL. [Báo cáo](../verification/2026-10-18-payment-delivery.md) giữ lệnh/filter và phạm vi source. Không thay đổi trạng thái nghiệm thu adapter/runtime thật.

Commit trước fix: `5315fa4` thiết kế, `533b681` core nhận tiền/giao số và `b7afe1a` email outbox. Fix này được commit riêng với test và history; hash tra bằng `git log -- docs/history/2026-10-18-19-order-timestamp-utc.md`.
