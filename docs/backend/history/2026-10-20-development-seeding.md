# 2026-10-20 — Seed dữ liệu development qua ICustomSeeder

Theo [kế hoạch BE](../features/development-seeding.md) và [hồ sơ phối hợp](../../shared/features/local-admin-integration.md).

## Đã triển khai

- Theo yêu cầu cuối, `ICustomSeeder` giữ một hàm `InitializeAsync(CancellationToken)` như mẫu NuGet. Runner chạy lần lượt theo thứ tự đăng ký DI và dừng khi lỗi/hủy. Đã bỏ Name/Order và kiểm tra tên không cần thiết.
- Dữ liệu nằm trong một file `Persistence/Initialization/SeedData/catalog.json`. `CatalogSeedData` đọc và kiểm tra JSON trước khi ghi; seeder chỉ điều phối thêm dữ liệu. File được copy tới `SeedData/catalog.json` trong output/publish.
- Host có lệnh riêng `--seed-dev`; chỉ cho `Development`, không ghép với bootstrap/preflight và không chạy HTTP. Khởi động HTTP thông thường không gọi seed. Không thêm migration hoặc tự chạy migration.
- `CatalogDevelopmentSeeder` chỉ đăng ký khi module Catalog bật. Bộ mẫu dùng `ICommerceService` để tạo loại/danh mục/sản phẩm, khởi tạo kho MAIN, điều chỉnh tồn và đổi trạng thái. Không ghi tắt aggregate bằng SQL hoặc tự cấp quyền.
- Dữ liệu mẫu gồm 2 loại, 6 danh mục, 14 sản phẩm vật lý, mỗi sản phẩm 1 SKU `DEMO-*` và một số dư kho MAIN; giá VND nguyên đồng. Có 8 Published, 5 Draft, 1 Archived để kiểm tra giao diện quản trị và public catalog.
- Định danh tra cứu: type code `DEMO-ESSENTIALS` / `DEMO-ELECTRONICS`, category slug, product SKU / slug. Record đã có được giữ nguyên, kể cả tên, slug, giá, trạng thái và tồn đã sửa. Tồn đầu kỳ chỉ ghi cho sản phẩm vừa tạo. Không reset database và không tạo dữ liệu đơn hàng giả.

## Cách chạy

1. Cấu hình SQL Server local; chạy migration và bootstrap hiện có bằng thao tác riêng.
2. Trong cấu hình local bị ignore, đặt `DevelopmentSeed:ActorEmail` tới tài khoản đang hoạt động đã có quyền Catalog tương ứng. Có thể đặt bằng biến môi trường `DevelopmentSeed__ActorEmail`. Không có email/password mặc định cho lệnh seed.
3. Đặt `ASPNETCORE_ENVIRONMENT=Development`, rồi chạy:

```powershell
dotnet run --project src/Host/Host.csproj --no-launch-profile -- --seed-dev
```

Lệnh không đăng nhập bằng password và không in credential; đây là thao tác của operator có quyền chạy process/configuration local. Seeder tra quyền hiện có: ProductType.Create, Category.Create, Product.Create, Product.Publish, Price.Update, Inventory.Initialize và Inventory.Adjust. Thiếu quyền thì dừng trước khi tạo kho/dữ liệu Catalog.

Mỗi use case sở hữu transaction của nó. Bộ dữ liệu không có transaction bao trùm nhiều use case. Nếu process bị dừng giữa lúc tạo một sản phẩm và bổ sung tồn/trạng thái, lần chạy sau giữ sản phẩm đã tồn tại; operator cần hoàn thiện record đó bằng chức năng quản trị. Seeder không đoán và ghi đè chỉnh sửa của người dùng để tự sửa một lần chạy dang dở.

## Thêm bộ dữ liệu khác

Tạo lớp implements `ICustomSeeder`, đọc JSON của bộ đó, inject use case cần thiết, đăng ký `services.AddScoped<ICustomSeeder, TenSeeder>();`. Runner chạy theo thứ tự đăng ký; mỗi bộ tự kiểm tra tồn tại và giữ dữ liệu đã chỉnh. Không thêm credential mặc định vào runner.

## Kiểm chứng

Đã chạy trên source của bước này, bằng SDK .NET 10.0.402 (global.json cho phép latestPatch):

| Kiểm tra | Kết quả |
|---|---|
| Build test project và các project phụ thuộc | Đạt; còn analyzer warnings trong repository |
| Runner/DI lượt trước khi chuyển JSON | 10/10 đạt trên source trước; không dùng làm bằng chứng cho source mới |
| Lượt cuối sau khi chuyển JSON và giản lược interface | 9/9 đạt: 7 unit/DI/JSON và 2 provider cases trên SQL Server `.` |
| Provider tests SQL Server LocalDB | 2/2 đạt |
| Provider tests SQL Server instance `.` bằng Windows authentication | 2/2 đạt; database riêng `boilerate_test_*` được fixture tạo và dọn |
| PostgreSQL | Chưa chạy trong lượt này; chưa có connection local |

Provider tests tạo seed qua use cases thực, kiểm 2 loại/6 danh mục/14 sản phẩm/14 SKU/14 số dư và movement; sửa tên, slug, giá, trạng thái, tồn rồi seed lại để chứng minh dữ liệu không bị ghi đè hoặc tạo trùng. Ca thiếu quyền chứng minh không thêm dữ liệu Catalog hoặc grants. Kiểm `HasPendingModelChanges()` không phát hiện thay schema.

Lệnh kiểm thử:

```powershell
dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj --no-restore --filter 'FullyQualifiedName~DevelopmentSeedingTests&Category!=ProviderIntegration' --verbosity quiet
dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~DevelopmentSeedingTests&DisplayName~mssql' --verbosity quiet
```

Lượt thứ hai dùng `BOILERATE_TEST_MSSQL` trỏ SQL Server `.`. Raw logs bị ignore tại `artifacts/development-seeding/`.

Lượt kiểm tra cuối chạy lại cả 9 ca trên source JSON hiện tại bằng bộ lọc loại PostgreSQL: **9 đạt, 0 lỗi, 0 bỏ qua**. Đã đối chiếu SHA-256 của JSON nguồn với bản copy trong Host/tests; cả ba giống nhau. Log `artifacts/seed-json-cli.log` ghi Host đọc JSON và giữ nguyên 14 sản phẩm có sẵn, không tạo mới. Xem [báo cáo kiểm chứng](../verification/2026-10-20-development-seeding.md) cho lệnh và phạm vi. Kết quả này không thay cho nghiệm thu giao diện/API thật.
