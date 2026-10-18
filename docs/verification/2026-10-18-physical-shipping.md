# Kiểm chứng giao vật lý và COD — 2026-10-18

Môi trường: Windows, .NET SDK10.0.402, SQL Server LocalDB và PostgreSQL16 cluster test cô lập tại port55439. Fixture tạo database test riêng; không dùng dữ liệu shop thật. Raw logs/TRX nằm trong artifacts/shipping-*, bị loại khỏi Git.

## Từng lượt thực tế

1. `dotnet build src/Host/Host.csproj -c Release --no-restore`: **0 lỗi,1690 warnings**,44.72s. Đây chỉ là build trước scaffold, không phải nghiệm thu shipping.
2. EF scaffold offline `PhysicalShipping` cho hai provider, exit0; IDs MSSQL `20261018151738`, PostgreSQL `20261018151746`. Down guard bổ sung trước DDL; năm migrations cũ giữ nguyên.
3. `dotnet build tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-restore`: **0 lỗi,2192 warnings**,35.33s. Warnings analyzer/style vẫn còn.
4. `dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~Shipping|FullyQualifiedName~FreshBaselineMigrationTests|FullyQualifiedName~OrderingMigrationTests|FullyQualifiedName~PaymentMigrationTests' --logger 'trx;LogFileName=shipping-targeted.trx' --results-directory artifacts/shipping-targeted`: **62/62 pass,0 fail/skip**,1m39s. Gồm ShippingDomain23, ShippingIntegration22, ShippingMigration4, FreshBaseline4, OrderingMigration2, PaymentMigration4 và3 Ordering cases có từ Shipping trong tên. Đặt BOILERATE_TEST_POSTGRES tới cluster test và dùng LocalDB fallback fixture cho MSSQL.
5. Regression `Runtime!=Container&Runtime!=Recovery&Smoke!=Template` lượt đầu: **324 pass,1 fail,0 skip /325**,5m29s, TRX artifacts/shipping-regression/shipping-regression.trx. Failure PostgreSQL `ShipmentOperationsReplay...`: response ban đầu giữ tick100ns, DB chỉ microsecond, replay khác phần tick dư. Domain đã sửa chuẩn hóa microsecond và thêm clock/ticks cố định, không nới assertion. Build/lượt kiểm chứng sau sửa đang thực hiện. Không cộng các test trùng với lượt62 thành tổng duy nhất.
6. Build sau sửa precision: `dotnet build tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-restore`, **0 lỗi,2806 warnings**,24.91s. Chạy filter `FullyQualifiedName~ShipmentOperationsReplayWithoutVersions|FullyQualifiedName~DispatchAndDeliveryPreserveMicroseconds`: **3/3 pass,0 fail/skip**,14s; TRX artifacts/shipping-precision/shipping-precision.trx. Clock của replay có ticks dư cố định; giữ assertion bằng nhau chính xác cho timestamp trước/sau đọc DB. Regression đầy đủ sau sửa đang chạy, không coi3ca này thay thế cả lượt regression.
7. Regression cuối sau sửa: `dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'Runtime!=Container&Runtime!=Recovery&Smoke!=Template' --logger 'trx;LogFileName=shipping-final-regression.trx' --results-directory artifacts/shipping-final-regression`: **326/326 pass,0 fail/skip**,6m10s. Gồm24 ShippingDomain,22 ShippingIntegration và4 ShippingMigration; còn lại regression Catalog/Identity/Ordering/payment/digital/media/outbox hiện hữu. Đây là source có fix precision và clock tái hiện, không bỏ ca thất bại hoặc nới assertion. Các ca trùng giữa các lượt không được cộng thành tổng duy nhất.
8. Kiểm tra docs cuối: **58 files,319 liên kết nội bộ,0 hỏng**; `git diff --check` đạt. PostgreSQL cluster test port55439 đã dừng sạch sau regression; logs/TRX vẫn ở artifacts bị ignore.

## Phạm vi đã kiểm chứng local

Domain Shipment/partial consume/COD; dispatch/delivered quantity/stock/ledger, replayhash/version, race, commitfailure rollback; manual COD full collection chỉ sau delivered, bank receipt giữa partial shipment không tái giữ; mixed Unique/ManualExtension; owner/ACL/HTTP/no-store/UTC; composite FK/check/unique/append-only; empty down/up và blocked destructive downgrade.

## Chưa nghiệm thu

Chưa có carrier API/webhook; cập nhật vận chuyển/COD là thao tác do nhân viên xác nhận thực tế. SMTP/gateway thật và Linux/Docker/CI deployment chưa được nghiệm thu bởi lượt này. Container/Recovery/Template bị loại theo filter, không được coi là các test adapter/runtime đã pass hoặc skip trong326. Refund/returns và manual renewal workflow còn là lát tiếp nối. Bộ regression276 trước shipping là bằng chứng lịch sử riêng, không cộng với số test mới.
