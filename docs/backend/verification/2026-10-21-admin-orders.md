# Verification — Admin Orders — 2026-10-21

## Phạm vi

Kiểm chứng contract BE, DI, seeder và build cho Admin Orders. Đây là bằng chứng local source/build/test; chưa coi là nghiệm thu database, deployment hoặc production.

## Kết quả

| Lệnh | Phạm vi | Kết quả |
|---|---|---|
| `dotnet build Boilerate.sln --no-restore` | .NET 10 solution, Windows | Pass; 0 error. Lượt compile đầy đủ phát ra analyzer warnings nền của solution. |
| `dotnet test ... --filter "DisplayName~mssql&FullyQualifiedName~AdminOrderHistoryIncludesIndirectRenewalRequestAndAttemptAudit"` | History nối Order với RenewalRequest/RenewalAttempt | Pass; 1/1 trên MSSQL. |
| `dotnet test ... --filter "DisplayName~mssql&FullyQualifiedName~PartialPaidShipmentsConsumeOnlyTheirHeldQuantitiesAndCompleteAfterEveryDelivery"` | DTO phản ánh `PhysicalDispatchStarted` sau dispatch | Pass; 1/1 trên MSSQL. |
| `dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj --no-restore --filter "FullyQualifiedName~DevelopmentSeedingTests.OrderingDataIsReadFromTheCopiedJsonFile\|FullyQualifiedName~DevelopmentSeedingTests.RunnerAwaitsSeedersInRegistrationOrder\|FullyQualifiedName~DevelopmentSeedingTests.RegistrationIsScopedAndDoesNotRequireTheCatalogModule"` | Ordering seed registration/data | Pass; 3/3. |
| `git diff --check` | Backend working tree | Pass tại thời điểm lập báo cáo. |

## Bằng chứng source

- `AdminOrderQuery` lọc search theo number/email/phone, status/payment/fulfillment/date; sort `CreatedOn DESC, Id DESC`.
- `OrderSummary` có recipient/email/phone và status context.
- History endpoint dùng `IOrderHistoryService`, DI scoped trỏ về `OrderingService`, chỉ trả safe DTO và không trả OldValues/NewValues raw.
- History còn nối audit của `RenewalRequests`, `RenewalAttempts` và `RenewalOperations` qua request id liên quan tới order, không chỉ các bảng có `OrderId` trực tiếp.
- Không thêm `PUT/PATCH Order`; command cũ tiếp tục đi qua version/permission/lifecycle service.
- `OrderingDevelopmentSeeder` đọc `SeedData/ordering.json`, phụ thuộc Catalog, operation key ổn định, bỏ qua scenario đã seed và không cấp permission.

## Chưa kiểm chứng

- Chưa chạy full database integration suite với SQL Server/PostgreSQL trong lượt này.
- PostgreSQL integration chưa chạy vì môi trường thiếu `BOILERATE_TEST_POSTGRES`; MSSQL đã chạy hai regression test nêu trên.
- Chưa chạy `--seed-dev` trên database trống và database đã có operator edits để xác nhận replay/không overwrite ở runtime.
- Chưa chạy HTTP contract test cho tất cả filters, history authorization, 409 version conflict và safe projection.
- Chưa đối chiếu OpenAPI artifact nếu pipeline sinh tài liệu từ source.
