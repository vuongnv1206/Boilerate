# Kiểm chứng seed JSON — 2026-10-20

Theo [kế hoạch](../features/development-seeding.md) và [lịch sử](../history/2026-10-20-development-seeding.md). Kiểm tra local bằng .NET SDK 10.0.402; chưa nghiệm thu deployment.

## Source đã kiểm tra

- `ICustomSeeder` chỉ có `InitializeAsync(CancellationToken)`.
- `CustomSeederRunner` chạy lần lượt theo thứ tự DI, chuyển tiếp yêu cầu hủy và dừng khi có lỗi. Runner chỉ cho chạy trong Development; không tự migrate hoặc tạo tài khoản.
- `Infrastructure/Startup.cs` đăng ký runner. Bộ Catalog chỉ được đăng ký khi `Modules:Catalog:Enabled=true`.
- Tất cả dữ liệu mẫu nằm trong `Persistence/Initialization/SeedData/catalog.json`: 2 loại, 6 danh mục, 14 sản phẩm và 14 SKU. `CatalogSeedData` đọc và kiểm tra JSON; `CatalogDevelopmentSeeder` tạo dữ liệu qua các use case hiện có.
- `Infrastructure.csproj` copy file tới `SeedData/catalog.json` trong output/publish. Đã so sánh SHA-256 giữa file nguồn và output của Host/tests: giống nhau. Chưa chạy publish riêng trong lượt này.
- Host xử lý `--seed-dev` riêng trước khi chạy HTTP; không gọi seed khi khởi động HTTP thông thường.

## Test đã chạy

```powershell
$env:BOILERATE_TEST_MSSQL='Server=.;Database=master;Integrated Security=true;TrustServerCertificate=true'
dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj --no-restore --filter 'FullyQualifiedName~DevelopmentSeedingTests&DisplayName!~postgresql' --verbosity quiet
```

Kết quả: **9 đạt, 0 lỗi, 0 bỏ qua**, thời gian test 35 giây. Hai ca SQL Server tạo database riêng `boilerate_test_*` rồi dọn qua fixture; không sửa database development.

| Phần kiểm tra | Số ca | Kết quả |
|---|---:|---|
| Thứ tự runner, môi trường Development, hủy, lỗi, DI và đọc JSON đã copy | 7 | Đạt |
| Seed qua use case rồi chạy lại sau khi sửa tên/slug/giá/trạng thái/tồn | 1 | Đạt; không tạo trùng hoặc ghi đè; không có thay đổi model cần migration |
| Tài khoản thiếu quyền | 1 | Đạt; dừng trước khi ghi dữ liệu Catalog, không tự thêm quyền |

PostgreSQL chưa chạy vì lượt này không có cấu hình kết nối PostgreSQL. Không dùng kết quả SQL Server để suy ra PostgreSQL đã đạt.

## Host CLI với database development

Log local bị ignore tại `artifacts/seed-json-cli.log` ghi kết quả lệnh `--seed-dev`:

- Chạy `CatalogDevelopmentSeeder` từ file JSON.
- Tạo **0** sản phẩm mới; giữ **14** sản phẩm đã có.
- Runner hoàn tất và process dừng; không mở HTTP.

Đây là bằng chứng chạy lại trên database development. Việc tạo đủ dữ liệu mới và giữ các chỉnh sửa được kiểm riêng bằng ca SQL Server ở trên. Kiểm giao diện đọc/ghi API thực thuộc hồ sơ tích hợp FE, không nằm trong báo cáo này.
