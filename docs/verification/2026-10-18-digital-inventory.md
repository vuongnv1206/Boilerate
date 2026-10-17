# Kiểm chứng cách giao và kho số — 2026-10-18

Timezone Asia/Saigon. Source đầu đợt: `8f98dbe`; các thay đổi đang ở working tree, không coi là đã kiểm thử trên checkout ban đầu nguyên trạng. Kế hoạch: [digital inventory](../features/digital-inventory.md).

## Phạm vi và môi trường

Windows, .NET SDK 10.0.402; SQL Server LocalDB và PostgreSQL 16 cluster test riêng localhost:55439. Test provider dùng database ngẫu nhiên `boilerate_test_<guid>` của fixture. Không dùng database shop thật.

## Kết quả

| Lượt | Kết quả thực tế | Artifact local |
|---|---|---|
| Build tích hợp sau cập nhật constructor helpers | 0 errors; 2140 analyzer/style warnings | Kết quả console đã đối chiếu; raw build log được ghi đè bởi lượt rebuild bên dưới |
| Rebuild có hai migrations và tests nâng cấp/concurrency | 0 errors; 957 warnings cho phần build lại, không so như tổng warnings đã giảm | artifacts/digital-inventory-build.log, nội dung hiện tại |
| Domain | 10 passed, 0 failed, 0 skipped | artifacts/digital-inventory/digital-domain.trx |
| Digital provider/service/HTTP/fresh/upgrade/concurrency | 16 passed, 0 failed, 0 skipped | artifacts/digital-inventory/digital-provider.trx |
| Regression ngoài Container/Recovery/Template smoke | **184 passed, 0 failed, 0 skipped**, 3m37s | artifacts/digital-inventory/digital-regression.trx; artifacts/digital-regression-test.log |

Các lượt build giữa chừng từng gặp file output đang bị giữ, thiếu interface implementation chưa viết xong và thiếu tham số Data Protection trong RecoveryRuntime helper. Đã cập nhật helper và build lại thành công; không coi các lỗi tích hợp tạm là kết quả test. Chạy build/test tuần tự sau tích hợp.

Lệnh đã chạy từ root (biến `BOILERATE_TEST_POSTGRES` trỏ cluster cô lập; Windows dùng LocalDB mặc định):

```powershell
dotnet build tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-restore
dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~DigitalInventoryDomainTests'
dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~DigitalInventoryIntegrationTests'
dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'Runtime!=Container&Runtime!=Recovery&Smoke!=Template'
```

Các lệnh test dùng TRX logger và results directory `artifacts/digital-inventory`. Không cộng các lượt thành số test độc lập vì regression chứa lại những ca đã chạy riêng. Provider cases tạo/migrate database thật, kiểm composite FK/check/unique, import cùng version, ciphertext/audit redaction, quyền HTTP và no-store. Upgrade case migrate tới CommerceCatalog, seed Product/SKU/balance rồi apply DigitalInventory, giữ nguyên giá/tồn và default Physical.

Scaffold migrations qua `dotnet ef migrations add DigitalInventory --no-build --configuration Release --project src/Migrators.<provider> --startup-project src/Host -- --provider <mssql|postgresql> --design-only`; hai lệnh đạt. Tên migration dùng thời gian UTC 20261017, tương ứng ngày triển khai 2026-10-18 Asia/Saigon. InitialFoundation/CommerceCatalog không bị sửa.

Đã đối chiếu counters TRX: provider riêng có 8 MSSQL + 8 PostgreSQL; regression có 45 MSSQL + 45 PostgreSQL và các ca không dùng provider. Kiểm 42 file Markdown/208 relative links đạt, `git diff --check` đạt. Raw TRX/log và nội dung/keys không được commit.

## Giới hạn

Chỉ mốc cấu hình cách giao/kho số. Chưa có Cart/Order, reservation, giao theo đơn, xác nhận thanh toán, email hoặc yêu cầu gia hạn. Test local không xác nhận Linux/Docker/CI/deployment/adapter thật.
