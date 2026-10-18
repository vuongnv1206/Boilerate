# Kiểm chứng giao vật lý và COD — 2026-10-18

Môi trường: Windows, .NET SDK10.0.402, SQL Server LocalDB và PostgreSQL16 cluster test cô lập tại port55439. Fixture tạo database test riêng; không dùng dữ liệu shop thật. Raw logs/TRX nằm trong artifacts/shipping-*, bị loại khỏi Git.

## Từng lượt thực tế

1. `dotnet build src/Host/Host.csproj -c Release --no-restore`: **0 lỗi,1690 warnings**,44.72s. Đây chỉ là build trước scaffold, không phải nghiệm thu shipping.
2. EF scaffold offline `PhysicalShipping` cho hai provider, exit0; IDs MSSQL `20261018151738`, PostgreSQL `20261018151746`. Down guard bổ sung trước DDL; năm migrations cũ giữ nguyên.
3. `dotnet build tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-restore`: **0 lỗi,2192 warnings**,35.33s. Warnings analyzer/style vẫn còn.
4. `dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~Shipping|FullyQualifiedName~FreshBaselineMigrationTests|FullyQualifiedName~OrderingMigrationTests|FullyQualifiedName~PaymentMigrationTests' --logger 'trx;LogFileName=shipping-targeted.trx' --results-directory artifacts/shipping-targeted`: **62/62 pass,0 fail/skip**,1m39s. Gồm ShippingDomain23, ShippingIntegration22, ShippingMigration4, FreshBaseline4, OrderingMigration2, PaymentMigration4 và3 Ordering cases có từ Shipping trong tên. Đặt BOILERATE_TEST_POSTGRES tới cluster test và dùng LocalDB fallback fixture cho MSSQL.
5. Đang chạy regression `Runtime!=Container&Runtime!=Recovery&Smoke!=Template`, không cộng các test trùng với lượt62 thành tổng duy nhất. Kết quả bổ sung khi hoàn tất.

## Phạm vi dự kiến kiểm chứng

Domain Shipment/partial consume/COD; dispatch/delivered quantity/stock/ledger, replayhash/version, race, commitfailure rollback; manual COD full collection chỉ sau delivered, bank receipt giữa partial shipment không tái giữ; mixed Unique/ManualExtension; owner/ACL/HTTP/no-store/UTC; composite FK/check/unique/append-only; empty down/up và blocked destructive downgrade.

## Chưa nghiệm thu

Chưa có carrier API/webhook; cập nhật vận chuyển/COD là thao tác do nhân viên xác nhận thực tế. SMTP/gateway thật và Linux/Docker/CI deployment chưa được nghiệm thu bởi lượt này. Refund/returns và manual renewal workflow còn là lát tiếp nối. Bộ regression276 trước shipping là bằng chứng lịch sử riêng, không cộng với số test mới.
