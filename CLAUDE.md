# Quy tắc Backend (`Boilerate/`)

Đây là repository **Backend `.NET 10`** (Clean Architecture). Chỉ triển khai API, domain logic, phân quyền, persistence và EF Core migrations tại đây — **tuyệt đối không viết giao diện UI hay tài liệu dự án trong repo này**.

## Đọc trước khi sửa code

1. Luật gốc của workspace: [../AGENTS.md](../AGENTS.md)
2. Luật kỹ thuật Backend chi tiết: [../agents/backend.md](../agents/backend.md) và quy trình tài liệu: [../agents/workflow-and-docs.md](../agents/workflow-and-docs.md)
3. Nguồn tài liệu & hợp đồng duy nhất: [../docs/README.md](../docs/README.md) ([API](../docs/contracts/commerce-api.md), [Database](../docs/contracts/commerce-database.md), [Local Runbook](../docs/contracts/local-runbook.md))
4. Skill triển khai tính năng Backend: [.agents/skills/backend-workflow/SKILL.md](.agents/skills/backend-workflow/SKILL.md)

## Nguyên tắc bắt buộc

- **Môi trường mặc định**: Chạy **SQL Server Local** (`DBProvider = "mssql"` trong `src/Host/Configurations/database.json`, cổng `http://localhost:5000`). Giữ nguyên hỗ trợ chuyển đổi `DBProvider` (`mssql` | `postgresql`) và migrations hai provider trong code, nhưng không yêu cầu chạy PostgreSQL hay Docker ở máy phát triển.
- **Chuẩn Vertical-Slice theo tính năng**: Mỗi use case nghiệp vụ đặt trong đúng thư mục module tại `src/Application/<Module>/` với 1 file `<UseCase>Request.cs` chứa trọn bộ **Request + CustomValidator + Handler**; Controller tương ứng tại `src/Host/Controllers/<Module>/` chỉ nhận HTTP và gọi `Mediator.Send(...)`; không tạo God-service nhồi nghiệp vụ ở `src/Infrastructure/`. Giữ nguyên các module nền tảng (`Identity`, `Personal`, `Auditing`, `Notifications`) trừ khi có yêu cầu trực tiếp.
- **Bảo toàn kiến trúc & an toàn dữ liệu**: Tôn trọng ranh giới `Domain` → `Application` → `Infrastructure` → `Host`, kiểm tra quyền `Function.Action` ở cả controller và handler, giữ optimistic concurrency (`Version`), idempotency (`OperationId`) và cổng khóa giao dịch (`ICommerceUnitOfWork`).
- **Giữ sạch repo**: Mọi tài liệu cập nhật tại `../docs/`; không tạo lại `Boilerate/docs/`, không tạo script nháp dùng một lần và không để lại `artifacts/` lồng trong `src/` hay `tests/`. `AGENTS.md` và `CLAUDE.md` phải giống nhau 100%.
