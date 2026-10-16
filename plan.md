# Boilerate: nền tảng backend và kế hoạch nâng cấp

Cập nhật 2026-10-16 theo quyết định mới: tiếp tục nâng cấp code hiện có, dùng baseline cho database mới, chưa mở rộng Catalog trong lượt này. README là hướng dẫn vận hành; kết quả kiểm chứng từng lượt ghi riêng trong artifacts.

## Phạm vi hiện tại

- .NET 10, quản lý package tập trung/lock/SDK pin; giữ versions đã được kiểm chứng, không nâng major hàng loạt.
- SQL Server hoặc PostgreSQL chọn lúc startup, model chung và một baseline InitialFoundation riêng cho mỗi provider. **Chỉ database mới**; lịch sử migration thử nghiệm trước đây đã gỡ khỏi source. Không xóa hay migrate database đang có; muốn chuyển dữ liệu cũ cần yêu cầu và công cụ riêng.
- Role là nhóm quyền; user nhiều role, nhận hợp grants Function–Action. Code ổn định, labels sửa được; bảng valid pairs và grants có FK/unique/concurrency. Chưa có user-specific deny, role inheritance, department/data scope hoặc frontend quản trị.
- Bootstrap admin/registration/grant là lệnh operator explicit. Startup không migrate, không sửa ACL. Preflight đọc schema hiện tại bằng LINQ, kiểm tra Code/NormalizedCode và grant pairs, không backfill theo Name hay tự sửa quyền.
- Sessions giữ refresh hash, atomic rotation/reuse family revoke, JWT issuer/audience/sid và account/session DB checks. User không còn cột token cũ. Notifications có read/hide receipt theo người nhận, không còn trạng thái đọc chung.
- CurrentUser reader/initializer giữ đúng scoped boundary; actor ID không tạo authentication. Giữ repository/specification/Identity/cache/storage/mail/jobs/MediatR ở nơi sử dụng thực tế.
- Catalog giữ code/model/dữ liệu trong schema, mặc định tắt. Cờ module chỉ thay routes/Swagger sau restart, không đổi model.
- PDFsharp/MigraDoc6.2.4 và Noto OFL, Razor/ClosedXML; các demo endpoint/hợp đồng chưa triển khai đã gỡ. Roslyn vẫn cần cho Razor runtime.

## Lượt nâng cấp khả năng bảo trì

| Khu vực | Mục tiêu |
|---|---|
| Persistence | Mọi async save được hỗ trợ chạy cùng pipeline audit/soft delete/events; overload sync hoặc deferred acceptance từ chối rõ ràng thay vì bypass |
| Repository | Range operations phát event nhất quán, input chỉ duyệt một lần, spec delete dùng đúng tập entity |
| Tìm kiếm/export | IsActive áp dụng cả rows/count; phân trang có thứ tự ổn định; audit export không bỏ qua filter hoặc chấp nhận UserId sai |
| Identity | Confirmation request lỗi trả400, thông báo chung không chứa email/phone; giữ các kiểm tra account/session và ACL hiện tại |
| Domain chung | AuditableEntity có Guid ngay trước EF tracking; bỏ generic audit layer không cần và trạng thái legacy đã được thay thế |
| Excel | Không reflection vào indexer, hỗ trợ enum/số lẻ, bảo toàn số vượt precision Excel, cancellation trong công việc thực tế |
| Source cleanup | Xóa helper/DTO export không có caller/DI/config/runtime contract; giữ abstraction đang phục vụ hệ thống |
| Migration | Hai baseline gọn, schema không có legacy token/read columns, valid grant pairs/unique constraints, no pending model changes |

Không tắt analyzer hoặc sửa generated migrations chỉ để giảm warning. Cảnh báo có ảnh hưởng thực tế được sửa kèm kiểm thử; style/docs và API/performance còn lại được ghi rõ theo call site/rule. Mốc trước lượt này: 1.260 warnings, 0 lỗi; template RenamedBackend81/81 tests. Lượt mới có báo cáo/số đo riêng tại artifacts/maintainability-review/report.md, không dùng kết quả cũ thay bằng chứng.

## Kiểm chứng và release

Locked restore + full Release build với analyzer; tests trên SQL Server/PostgreSQL tạm thời; template tên mới chạy migrate/bootstrap/run với Catalog on/off. Mỗi provider chỉ có một migration baseline, không tài khoản/ACL/grants seed tự động ngoài sentinel lock row.

Production triển khai migration bằng bước riêng sau backup/review script. Baseline mới chỉ áp dụng vào database mới/rỗng; không dùng để nâng installation cũ. Không suy từ mock SDK hoặc Windows local sang Linux/SMTP/cloud/Redis/OAuth/Hangfire thật. Domain events hiện in-process, chưa durable outbox; file mồ côi cần cleanup có kiểm soát.

## Buổi trao đổi tiếp theo: shop linh hoạt

Định hướng đã ghi nhận: một shop bán nhiều loại hàng, catalog linh hoạt, SKU/biến thể, giá và tồn kho; đơn hàng/thanh toán làm sau. Chưa thêm bảng, API hoặc framework thuộc tính động trong lượt này. Cần chốt quy tắc hàng vật lý/số/dịch vụ, biến thể, tiền tệ, tồn kho và tính duy nhất SKU trước khi triển khai.
