# Quy tắc Backend (`Boilerate/`)

Đây là repository **Backend `.NET 10`** (Clean Architecture). Chỉ triển khai API, domain logic, phân quyền, persistence và EF Core migrations tại đây. **Không viết giao diện UI và không đặt tài liệu dự án trong repo này.**

## Đọc trước khi sửa code

1. Luật gốc của workspace: [../AGENTS.md](../AGENTS.md)
2. Luật kỹ thuật Backend: [../agents/backend.md](../agents/backend.md); quy trình và nơi ghi tài liệu: [../agents/workflow-and-docs.md](../agents/workflow-and-docs.md)
3. Tài liệu và hợp đồng: [../docs/README.md](../docs/README.md) ([API](../docs/contracts/commerce-api.md), [Database](../docs/contracts/commerce-database.md), [Local Runbook](../docs/contracts/local-runbook.md))
4. Skill triển khai tính năng Backend: [.agents/skills/backend-workflow/SKILL.md](.agents/skills/backend-workflow/SKILL.md)

## Nguyên tắc bắt buộc

- **Môi trường mặc định**: SQL Server local (`DBProvider = "mssql"` trong `src/Host/Configurations/database.json`, API tại `http://localhost:5000`). Giữ khả năng chuyển `DBProvider` (`mssql` | `postgresql`) và migrations cho cả hai provider; không yêu cầu PostgreSQL hay Docker trên máy phát triển.
- **Vertical-slice theo tính năng**: mỗi use case là một file `<UseCase>Request.cs` trong `src/Application/<Module>/` chứa Request + CustomValidator + Handler; controller ở `src/Host/Controllers/<Module>/` chỉ nhận HTTP và gọi `Mediator.Send(...)`. Không tạo service gom nghiệp vụ trong `src/Infrastructure/`. Chỉ sửa module nền tảng (`Identity`, `Auditing`, `Notifications`) khi có yêu cầu trực tiếp.
- **Kiến trúc và an toàn dữ liệu**: giữ ranh giới `Domain` → `Application` → `Infrastructure` → `Host`; kiểm quyền `Function.Action` ở cả controller và handler; giữ optimistic concurrency (`Version`), idempotency (`OperationId`) và khoá giao dịch (`ICommerceUnitOfWork`).
- **Code nền chưa dùng không phải code thừa**: đây là boilerplate. Không xoá tiện ích, interface hay service nền chỉ vì chưa có nơi tham chiếu.
- **Giữ sạch repo**: tài liệu cập nhật tại `../docs/`; không tạo lại `Boilerate/docs/`, không tạo script nháp dùng một lần, không để `artifacts/` lồng trong `src/` hay `tests/`. Commit không gắn `Co-Authored-By` của công cụ AI. `AGENTS.md` và `CLAUDE.md` phải giống nhau 100%.
