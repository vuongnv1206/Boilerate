# Kiểm chứng hoàn tiền và hàng trả — 2026-10-19

Local Windows, SDK .NET10.0.402, SQL Server LocalDB và PostgreSQL16 cluster test cô lập port55439; fixtures tạo database riêng. Raw logs/TRX tại artifacts/aftersales-* bị ignore; không dùng database shop hoặc credentials thật.

## Từng lượt thực tế

1. `dotnet build src/Host/Host.csproj -c Release --no-restore`: **0 lỗi/1899warnings**,80.97s. Chỉ xác nhận build trước scaffold, không phải nghiệm thu nghiệp vụ.
2. EF offline scaffold RefundsReturns hai provider exit0; MSSQL20261019112431/PG20261019112442. Thêm3bảng,2AK và constraints/index; không AlterColumn Currency, không sửa7migrations cũ. Down guard đã bổ sung trước DDL.
3. Build test project Release `--no-restore` sau review money-hash/precision: **0 lỗi/3122warnings**,39.52s. Warnings analyzer/style còn tồn tại.
4. Targeted lượt đầu: **86 pass/4 fail/0 skip trên90**,3m2s. Lệnh `dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~AfterSales|FullyQualifiedName~FreshBaselineMigrationTests|FullyQualifiedName~OrderingMigrationTests|FullyQualifiedName~PaymentMigrationTests|FullyQualifiedName~ShippingMigrationTests|FullyQualifiedName~RenewalMigrationTests' --logger 'trx;LogFileName=aftersales-targeted.trx' --results-directory artifacts/aftersales-targeted`, BOILERATE_TEST_POSTGRES trỏ cluster test, MSSQL fallback LocalDB. TRX gồm32 domain hậu mãi,32 integration hậu mãi,6 migration hậu mãi và20 migration regression.
5. Bốn failures thuộc hai case migration trên mỗi DB: seed COD gọi Close khi chưa RegisterPhysicalShipment; assertion audit tìm PascalCase dù serializer hiện hữu ghi camelCase. Đã sửa setup ghi nhận dispatch trước Close và parse JSON kiểm quantity/movement/actor/composite keys cụ thể. Không đổi domain/serializer hoặc bỏ assertion; chuyển sang build/rerun bước6, kết quả cuối ở bước7.398/398 renewal trước lát này là bằng chứng lịch sử riêng.
6. Sau sửa test: build **0 lỗi/1437warnings**,16.44s. Rerun `--filter 'FullyQualifiedName~AfterSalesMigrationTests' --logger 'trx;LogFileName=aftersales-migration-fixed.trx' --results-directory artifacts/aftersales-migration-fixed`: **6/6 pass,0 fail/skip**,22s. Giữ nguyên cả4 ca từng lỗi, tăng assertion kiểm giá trị JSON cụ thể. Các tests trùng giữa lượt đầu/rerun không cộng thành tổng duy nhất.
7. Regression cuối: `dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'Runtime!=Container&Runtime!=Recovery&Smoke!=Template' --logger 'trx;LogFileName=aftersales-regression.trx' --results-directory artifacts/aftersales-regression`: **468/468 pass,0 fail/skip**,9phút. Source code tại `9b2fe73`; thay đổi tiếp theo chỉ docs. Gồm70 ca hậu mãi mới (32 domain,32 integration,6 migration) cùng398 ca hiện hữu; TRX có129 ca/provider. Không bỏ4 ca từng lỗi hoặc nới domain để đạt kết quả.
8. Đã đối chiếu counters cả ba TRX: regression executed=passed=468, failed/notExecuted=0; lượt đầu86/90 vẫn giữ trong báo cáo. PostgreSQL test port55439 đã dừng sạch, pg_ctl fast exit0. Kiểm tra cuối **69 tệp Markdown/394 liên kết nội bộ,0 hỏng**, `git diff --check` đạt; raw logs/TRX/keys không commit.

## Đã kiểm chứng local

Refund cap/actor/reference/idempotency, partial tiếp tục giao, full dừng/nhả đúng phần còn giữ, Unique Delivered giữ nội dung; PendingVerification không mở, late Deliver/Verify không hồi sinh Closed; return cap/restock/ownership, COD cuối/late money; ACL/owner/HTTP/no-store, rollback/races; migrations emptyDownUp/historyblock/append-only trên cả hai DB.

## Chưa nghiệm thu

API ghi nhận đã hoàn/đã nhận hàng, không chuyển tiền hoặc gọi carrier. Không tự đặt chính sách đủ điều kiện refund/return. Gateway/RMA frontend/đổi hàng, Linux/Docker/CI và adapters/restore deployment thật còn mở. Regression local đã loại Container/Recovery/Template theo filter, không coi các gate đó pass hoặc skip trong468.
