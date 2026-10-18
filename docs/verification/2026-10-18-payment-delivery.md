# Kiểm chứng nhận tiền và giao hàng số — 2026-10-18

Môi trường: Windows, .NET SDK 10.0.402; SQL Server LocalDB và PostgreSQL 16 cluster test cô lập port55439. Connection test được đặt qua `BOILERATE_TEST_MSSQL`/`BOILERATE_TEST_POSTGRES` hoặc fallback LocalDB của fixture. Không dùng database vận hành. Log/TRX ở `artifacts/payment-*`, không commit.

## Từng lượt thực tế

1. Build trước scaffold: `dotnet build tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-restore`: đạt, 0 lỗi, 2494 warnings. Scaffold hai migration mới `PaymentDigitalDelivery` đạt.
2. Build sau scaffold và xử lý lỗi crypto giữ Paid+NeedsReview: cùng lệnh, đạt, 0 lỗi, 1995 warnings. Warnings analyzer/style hiện hữu vẫn còn; không tuyên bố đã xử lý toàn bộ.
3. Chạy `dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~Payment|FullyQualifiedName~FreshBaselineMigrationTests|FullyQualifiedName~OrderingMigrationTests' --logger 'trx;LogFileName=payment-core.trx' --results-directory artifacts/payment-core`: **49 pass, 2 fail, 0 skip / 51**, 1m15s. Hai failure cùng test mixed order ở hai provider: expected DomainException khi hủy Paid nhưng service chủ ý trả ConflictException/409. Đã sửa assertion phù hợp hợp đồng; chưa coi bản sửa đạt cho đến khi chạy lại.
4. Build lại Release đạt, 0 lỗi, 2612 warnings (bao gồm code consumer email đang chuẩn bị cho commit sau). Chạy lại filter rõ class `FullyQualifiedName~PaymentDomainTests|FullyQualifiedName~PaymentIntegrationTests|FullyQualifiedName~PaymentMigrationTests|FullyQualifiedName~FreshBaselineMigrationTests|FullyQualifiedName~OrderingMigrationTests`: **48/48 pass, 0 fail/skip**, 1m03s, TRX `artifacts/payment-core-recheck/payment-core-recheck.trx`. Gồm16 domain,22 payment integration,4 payment migration,4 baseline và2 ordering migration. Lượt đầu có thêm3 Ordering tests có chữ Payment trong tên; filter class lượt này không chọn chúng, không phải giảm test để né failure. Hai ca mixed đã được chạy lại và đạt.

## Phạm vi kiểm tra

Receipt full amount/UTC/normalization; Unique/Shared snapshot; expiry/reacquisition/shortage/retry; cancelled payment review; cạnh tranh confirmation và mã cuối; crypto defer; append-only và FK/unique/check; owner/ACL/no-store HTTP; fresh model và downgrade bảo vệ lịch sử/holds. Các test cụ thể nằm trong PaymentDomainTests, PaymentIntegrationTests, PaymentMigrationTests và baseline/ordering migration tests.

## Chưa nghiệm thu

Chưa có gateway/webhook, ngân hàng tự động, SMTP thật hoặc Linux/Docker/CI deployment acceptance trong lượt này. Email consumer đang được triển khai sau lát core; regression cuối sẽ được bổ sung khi chạy. Shipping/refund/manual renewal workflow chưa thuộc phạm vi.
