# Rà soát commerce backend — 2026-10-17

Phạm vi: Domain và quan hệ database Catalog/Inventory, application/API, phân quyền, transaction, media/outbox và cấu hình runtime. Product–Category tiếp tục là N–N qua ProductCategories; lần rà soát này không đổi schema hoặc migration.

## Các lỗi đã sửa

1. **Vòng đời event của transaction commerce.** Unit of Work trước đây commit nhưng không gọi hoàn tất batch event của BaseDbContext. Event không được phát; nếu delegate đã SaveChanges rồi rollback, batch cũ làm lệnh tiếp theo thất bại. Nay transaction được commit/dispose trước khi phát event, rollback/dispose trước khi hủy batch. Lỗi handler sau commit không chạy vào nhánh rollback. Kiểm thử trước bản sửa tái hiện cả hai lỗi trên SQL Server và PostgreSQL (4/4 ca thất bại đúng lý do).
2. **Mất thay đổi chưa lưu.** ChangeTracker.Clear ở đầu lệnh có thể âm thầm bỏ các thay đổi/event đang chờ trong cùng scope. Nay Unit of Work từ chối lệnh trước khi detach nếu còn thay đổi/event chưa lưu, giữ nguyên tracked state để caller xử lý rõ ràng.
3. **Lọc số khác kết quả giữa hai provider.** SqlClient làm tròn tham số vượt scale của decimal(18,4), hoặc ném lỗi encode với decimal quá lớn. Bộ lọc nay kiểm tra miền/scale giống dữ liệu field; giá lọc tuân theo miền VND nguyên đồng. Giá trị không hợp lệ trả lỗi nghiệp vụ trước khi query. Regression bao gồm cả hai đầu range, decimal.MinValue/MaxValue, scale 5 và giá trị biên hợp lệ.
4. **Ẩn lý do từ chối nghiệp vụ ở Production.** API nay giữ thông báo DomainException trong ProblemDetails.detail khi status <500, giúp quản trị biết lý do publish/lưu bị từ chối. Thông tin lỗi máy chủ/database vẫn được che. Có kiểm thử cho cả thông báo nghiệp vụ và lỗi server.
5. **Đóng gói nhầm báo cáo kiểm thử.** Package trước rà soát chứa TestResults/TRX của workspace. Đã loại các kết quả kiểm thử khỏi cả NuGet pack và template source để dự án sinh mới không mang theo báo cáo, đường dẫn máy hoặc log của lần chạy cũ. Kiểm tra trực tiếp nội dung nupkg sau đóng gói.

## Kiểm chứng và giới hạn

Kết quả chạy sau sửa được version tại [báo cáo kiểm chứng](verification/2026-10-17-commerce.md); lịch sử và commit tại [nhật ký](history/README.md). Raw logs/TRX cục bộ được dẫn trong báo cáo, không commit vào template. Các ca integration tạo database test riêng, áp dụng migrations thật và dọn sau khi chạy trên SQL Server LocalDB/PostgreSQL.

Đã rà lại FK ghép, unique default/SKU/tổ hợp, cập nhật schema, quy tắc publish và thứ tự khóa. Chưa phát hiện thêm lỗi có thể tái hiện trong phạm vi đó. Media attach/cleanup dùng chung khóa asset; retry/crash và lease trùng tiếp tục dựa vào storage key bất biến và thao tác xóa idempotent.

Linux/Docker/CI và các adapter ngoài chưa được nghiệm thu thực tế trong môi trường này. Sweeper hiện quét payload của backlog chưa hoàn tất; khi backlog lớn cần đo tải và tối ưu truy vấn. Generic domain events vẫn phát trong process sau commit; durable outbox hiện phục vụ MediaCleanupRequested, không đồng nghĩa mọi event đều bền vững.
