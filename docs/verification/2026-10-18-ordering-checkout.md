# Kiểm chứng Ordering/checkout — 2026-10-18

Asia/Saigon. Source đầu đợt `31a0109`; [hồ sơ thiết kế](../features/ordering-checkout.md). Kết quả dưới đây thuộc working tree sau source đó, không phải checkout cũ nguyên trạng.

## Môi trường và phạm vi

Windows/.NET 10, SQL Server LocalDB và PostgreSQL 16 cluster test cô lập localhost:55439. Fixture tạo/xóa database `boilerate_test_<guid>`. Raw logs/TRX/keys không commit.

## Kết quả

| Lượt | Kết quả thực tế | Artifact local |
|---|---|---|
| Build tích hợp | 0 errors, 2335 analyzer/style warnings | Console; log được ghi đè bởi các lượt rebuild sau |
| Rebuild với migrations/test race bổ sung | 0 errors,1816 warnings ở phần build lại | Console; log được ghi đè bởi lượt tiếp theo |
| Rebuild thêm kiểm email/migration tests | 0 errors,1171 warnings ở phần build lại | artifacts/ordering-build.log |
| Rebuild thêm CanCheckout/giỏ vượt miền tiền | 0 errors,2071 warnings ở phần build lại | Console; log được thay bởi build cuối sau chỉnh lookup lock |
| Build cuối sau chỉnh lookup lock | 0 errors,1836 warnings ở phần build lại | artifacts/ordering-build-final.log |
| Domain | 12 passed,0 failed,0 skipped | artifacts/ordering-checkout/ordering-domain.trx |
| Provider trên LocalDB/PG16 | 22 passed,0 failed,0 skipped,1m17s | artifacts/ordering-checkout/ordering-provider.trx |
| Migration Down guard/Down-Up | 2 passed,0 failed,0 skipped | artifacts/ordering-checkout/ordering-migration.trx |
| Regression trước bản sửa quote vượt miền tiền | 220 passed,0 failed,0 skipped,4m29s; không gồm CanCheckout | artifacts/ordering-checkout/ordering-regression.trx |
| Regression cuối sau sửa quote/lookup lock | **222 passed,0 failed,0 skipped,3m30s** | artifacts/ordering-checkout/ordering-regression-final.trx; artifacts/ordering-regression-final-test.log |

Một build giữa chừng thiếu using Domain.Messaging cho CommerceLock, đã sửa và build lại. Số warnings ở các lượt incremental không phải tổng cảnh báo đã giảm; chúng vẫn là analyzer/style warnings cần xử lý riêng.

Review sau lượt220 phát hiện tổng giỏ quá lớn làm MapCart báo lỗi sau khi SaveCart đã commit. Bản cuối bỏ chặn đọc quote, thêm CanCheckout và hai provider cases giảm/tăng/xóa giỏ quá giới hạn. Cũng tách lookup balance→SKU→Product thành từng query trước khi khóa type/product/balance, tránh join query giữ read lock ngược thứ tự. Những thay đổi này là lý do chạy lại regression trên bản cuối; không dùng kết quả220 để xác nhận bản sửa chưa được chạy.

Regression222 cuối đã chạy trên bản sửa và chứa12 Ordering domain,24 Ordering integration (12 mỗi provider) và2 Ordering migration (1 mỗi provider), cùng suite hiện có. Không cộng các lượt riêng thành số ca độc lập. Container/Recovery/Template smoke được loại rõ bằng filter; không có fail hoặc skip trong222ca thực sự được chọn.

Đã đối chiếu XML counters TRX:222 executed/passed,0 failed/notExecuted; toàn bộ lượt cuối có58 MSSQL +58 PostgreSQL và106 ca không dùng provider. Kiểm46 file Markdown/248 relative links và `git diff --check` đạt. Các raw artifacts chỉ lưu local, không nằm trong commit.

Lệnh từ root (biến BOILERATE_TEST_POSTGRES trỏ cluster test cô lập):

```powershell
dotnet build tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-restore
dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~OrderingDomainTests'
dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~OrderingIntegrationTests'
dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~OrderingMigrationTests'
dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'Runtime!=Container&Runtime!=Recovery&Smoke!=Template'
```

Scaffold tuần tự bằng `dotnet ef migrations add OrderingCheckout --no-build --configuration Release --project src/Migrators.<provider> --startup-project src/Host -- --provider <mssql|postgresql> --design-only`, cả hai lệnh exit0. Không thay InitialFoundation/CommerceCatalog/DigitalInventory. Down guard viết riêng để từ chối mất ownership khi còn hold trước mọi DDL.

Provider tests chạy migrations trên database thật, kiểm SQL check/composite FK, checkout retry/race/rollback, snapshot, TTL clock giả, hai expiry workers, race với adjustment, shipping/current price và HTTP auth/ownership/ACL/no-store. Những ca worker gọi service trên scope độc lập; chúng không xác nhận production process restart hay deployment worker thực tế. Migration tests chứng minh Down còn hold trả lỗi đúng và không mất dữ liệu; sau cancel, Down/Up giữ Catalog/balance/ciphertext và decrypt được, trong khi lịch sử Ordering bị xóa đúng cảnh báo.

## Giới hạn

Chỉ Cart/Order/reservation, xem/hủy đơn và expiry. Chưa nhận tiền, xử lý trả muộn, giao bí mật/email, gia hạn/PendingVerification hoặc Shipment. Không chạy dịch vụ/payment/carrier thật; Linux/Docker/deployment cần gate riêng.
