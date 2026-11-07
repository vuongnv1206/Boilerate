# Boilerate Backend (`.NET 10`)

Backend Clean Architecture trên `.NET 10`: Identity, phân quyền `Function–Action` theo nhóm từ database, giữ phiên trình duyệt qua HttpOnly cookie, kiểm toán (`AuditTrails`), hệ thống thương mại một cửa hàng tùy chọn (`Catalog`, `Storefront`, `Ordering`, `Payments`, `Shipping`, `Renewals`, `AfterSales`) và các adapter mail/storage/cache/jobs. Giao diện người dùng nằm ở repo độc lập kế bên [Frontend](../Frontend/README.md).

## Tài liệu & Quy tắc phát triển

- Luật làm việc: [AGENTS.md](AGENTS.md), [../AGENTS.md](../AGENTS.md) và [../agents/backend.md](../agents/backend.md).
- Nguồn tài liệu chính thức duy nhất của toàn dự án: [../docs/README.md](../docs/README.md).
  - Kiến trúc & Quy tắc nghiệp vụ: [../docs/overview/architecture.md](../docs/overview/architecture.md), [../docs/overview/business-rules.md](../docs/overview/business-rules.md), [../docs/overview/roadmap.md](../docs/overview/roadmap.md).
  - Hợp đồng API, Schema DB & Sổ tay Local: [../docs/contracts/commerce-api.md](../docs/contracts/commerce-api.md), [../docs/contracts/commerce-database.md](../docs/contracts/commerce-database.md), [../docs/contracts/local-runbook.md](../docs/contracts/local-runbook.md).
  - Nhật ký & Kết quả kiểm chứng: [../docs/history/changelog.md](../docs/history/changelog.md), [../docs/history/verification-summary.md](../docs/history/verification-summary.md).

## Cấu trúc các tầng & Nguyên tắc Runtime

- `Domain` giữ thực thể và bất biến nghiệp vụ; `Application` giữ use case, DTO, validation và interfaces; `Infrastructure` triển khai EF Core persistence, Identity, Commerce services và các adapter; `Host` khởi chạy HTTP/SignalR và lệnh CLI quản trị.
- **Mặc định chạy SQL Server Local** (`DatabaseSettings:DBProvider = "mssql"` trong `src/Host/Configurations/database.json`). Code vẫn duy trì song song `src/Migrators.MSSQL` và `src/Migrators.PostgreSQL` trên cùng mô hình dữ liệu để có thể chuyển đổi `DBProvider` khi cần, nhưng quá trình phát triển local không dùng Docker và không yêu cầu cài đặt PostgreSQL.
- `ICurrentUser` đọc người thao tác hiện tại; claims trong JWT chỉ định danh phiên (`sid`), còn quyền thực tế (`Function.Action` và `isCentralAdmin`) được kiểm tra trực tiếp từ database ở từng request. Async validators dùng chung `DbContext` chạy tuần tự.

## Khởi tạo trên SQL Server Local

Cài đặt .NET SDK theo `global.json` và chuẩn bị SQL Server / LocalDB tại máy phát triển. Không lưu mật khẩu hay khóa bí mật vào Git:

```powershell
dotnet tool restore
dotnet restore Boilerate.sln --locked-mode
dotnet build Boilerate.sln --no-restore

$env:DatabaseSettings__DBProvider = "mssql" # mặc định local (hoặc postgresql khi cấu hình riêng)
$env:DatabaseSettings__ConnectionString = "Server=localhost;Database=BoilerateDevelopment;Trusted_Connection=True;TrustServerCertificate=True;"
$env:SecuritySettings__JwtSettings__Key = "<khoa-ngau-nhien-rieng-it-nhat-32-byte-utf8>"

# Áp dụng chuỗi migrations SQL Server:
dotnet ef database update --project src/Migrators.MSSQL --startup-project src/Host -- --provider mssql

# Tạo tài khoản quản trị trung tâm đầu tiên trên DB mới:
$env:BOILERATE_BOOTSTRAP_EMAIL = "admin@gmail.com"
$env:BOILERATE_BOOTSTRAP_PASSWORD = "<mat-khau-manh-toi-thieu-9-ky-tu>"
dotnet run --project src/Host --no-launch-profile -- --bootstrap-admin
Remove-Item Env:BOILERATE_BOOTSTRAP_PASSWORD

# Bật module thương mại và chạy API (mặc định http://localhost:5000):
$env:Modules__Catalog__Enabled = "true"
dotnet run --project src/Host
```

Ở môi trường `Development`, nên lưu cấu hình qua `dotnet user-secrets set --project src/Host ...` (xem chi tiết tại [local-runbook.md](../docs/contracts/local-runbook.md)). Host **không tự migrate hay seed** khi khởi chạy HTTP; Swagger Development mở tại `/swagger`, kiểm tra sức khỏe tại `/health/live` và `/health/ready`.

## Lệnh Operator CLI

```powershell
dotnet run --project src/Host --no-launch-profile -- --preflight
dotnet run --project src/Host --no-launch-profile -- --register-capabilities
dotnet run --project src/Host --no-launch-profile -- --grant-admin-capabilities
dotnet run --project src/Host --no-launch-profile -- --register-capabilities --grant-admin-capabilities --function FinancialSummary
dotnet run --project src/Host -- --seed-dev
```

- `--preflight` kiểm tra toàn vẹn mã định nghĩa và bảng quyền mà không sửa dữ liệu.
- `--register-capabilities` chỉ thêm định nghĩa `Function`/`Action` mới vào DB; `--grant-admin-capabilities` (có thể giới hạn bằng `--function <Code>`) là quyết định cấp quyền chủ đích của quản trị viên.
- `--seed-dev` (chỉ chạy ở `Development`) nạp dữ liệu mẫu từ `SeedData/catalog.json` và `SeedData/ordering.json` một cách idempotent.
- Khi thay đổi EF model, luôn tạo migration cho cả `Migrators.MSSQL` và `Migrators.PostgreSQL` để giữ đồng bộ hai provider.

## Phân quyền, Tài khoản & Module cấu hình

Chi tiết các endpoint xác thực/phiên trình duyệt và quản trị hệ thống xem tại [../docs/auth-system/README.md](../docs/auth-system/README.md); chi tiết toàn bộ endpoint thương mại xem tại [../docs/contracts/commerce-api.md](../docs/contracts/commerce-api.md).

| Cấu hình | Mặc định / Hành vi |
|---|---|
| `DatabaseSettings:DBProvider` | `mssql` (mặc định local) hoặc `postgresql` |
| `Modules:Catalog:Enabled` | `false`; khi tắt sẽ ẩn routes/Swagger và dừng worker Commerce nhưng giữ nguyên schema/dữ liệu |
| `Modules:Notifications:Enabled` | `false`; khi bật sẽ mở API, `/hubs/notifications` và dispatcher |
| `HangfireStorageSettings:Enabled` | `false`; yêu cầu cấu hình storage riêng khi bật |
| `HangfireStorageSettings:EnableDashboard` | `false`; khi bật bắt buộc xác thực + quyền `Hangfire.View` |
| `BlobStorageSettings:Enabled` | `false`; lưu file local tại `Files/` (hoặc Azure/AWS khi cấu hình) |
| `CacheSettings:UseDistributedCache` | `false`; dùng memory cache (hoặc Redis khi cấu hình) |

## Quy ước giữ cảnh báo Analyzer có chủ đích

Ưu tiên xử lý triệt để lỗi bảo mật, giao dịch và rò rỉ tài nguyên; không tắt analyzer hàng loạt hoặc phá vỡ hợp đồng API/migration chỉ để ép số cảnh báo về 0:

| Nhóm giữ có chủ đích | Lý do kỹ thuật |
|---|---|
| `ICurrentUser` / initializer, marker DI/event | Cô lập reader/khởi tạo và đăng ký DI; attribution không tạo authentication |
| DTO collections/string URL, exception nghiệp vụ | Phục vụ model binding, relative paths và hợp đồng lỗi `ProblemDetails` hiện có |
| Normalized keys, EF string expressions | Giữ mã chuẩn hóa ổn định và khả năng dịch sang SQL; không thay máy móc bằng `StringComparison` |
| Persisted enum và generated migrations | Giữ giá trị số lưu dưới DB và giữ nguyên code migration do EF Core sinh |
| Retry boundary, cleanup guard & JSON cache logging | Bảo vệ vòng lặp retry, chặn xóa nhầm thư mục ngoài vùng test và không log nội dung cache nhạy cảm |

## Thư viện xuất file & Bản quyền

- Xuất PDF dùng **PDFsharp + PDFsharp-MigraDoc 6.2.4** (MIT) kèm font nhúng `Noto Sans` hỗ trợ tiếng Việt (SIL OFL 1.1); không tự nâng lên bản `6.5+` (đổi sang giấy phép AGPL/thương mại) khi chưa rà soát. Xuất Excel dùng `ClosedXML`, template email dùng `RazorEngineCore`. Xem chi tiết tại [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Kiểm thử & Đóng gói Template Local

Các ca tích hợp tự động tạo và xóa database ngẫu nhiên `boilerate_test_*` trên SQL Server / LocalDB (tuyệt đối không trỏ biến test vào database cửa hàng thật):

```powershell
# Kiểm thử nhanh Domain & Ranh giới kiến trúc (không cần DB):
dotnet test Boilerate.sln -c Release --filter "Category!=ProviderIntegration"

# Kiểm thử tích hợp với SQL Server Local:
$env:BOILERATE_TEST_MSSQL = "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet test Boilerate.sln -c Release --filter "FullyQualifiedName!~postgresql"

# Kiểm tra an toàn gói phụ thuộc & đóng gói dotnet new template:
dotnet list Boilerate.sln package --vulnerable --include-transitive --no-restore
pwsh scripts/verify-operations.ps1 -Stage Local
pwsh scripts/verify-template.ps1
```
