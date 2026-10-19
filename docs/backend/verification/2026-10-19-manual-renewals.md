# Kiểm chứng gia hạn thủ công — 2026-10-19

Môi trường local: Windows, .NET SDK10.0.402, SQL Server LocalDB và PostgreSQL16 cluster test cô lập port55439. Fixture tạo database riêng, không dùng dữ liệu shop. Raw logs/TRX tại artifacts/renewal-* bị loại khỏi Git. Không ghi connection string thật hoặc keys vào báo cáo.

## Các lượt thực tế

1. Ngày18, trước scaffold: Host Release build `--no-restore` **0 lỗi/1802warnings**,76.41s. Scaffold offline `ManualRenewals` cho hai provider exit0; thêm ba bảng và Down guard, không sửa sáu migrations cũ.
2. Ngày18: test project Release build `--no-restore` **0 lỗi/1568warnings**,39.93s. Sau đó review tăng dung lượng ciphertext Unicode và thêm integration tests; build này chưa chứng minh source cuối.
3. Ngày19: test project Release build `--no-restore` sau sửa Unicode **0 lỗi/2692warnings**,1m56.71s; warnings analyzer/style vẫn còn.
4. Foundation targeted: **52/52 pass,0 fail/skip**,1m23s, gồm RenewalDomain32, RenewalMigration6, FreshBaseline4, OrderingMigration2, ShippingMigration4, PaymentMigration4. Lệnh: `dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~RenewalDomainTests|FullyQualifiedName~RenewalMigrationTests|FullyQualifiedName~FreshBaselineMigrationTests|FullyQualifiedName~OrderingMigrationTests|FullyQualifiedName~ShippingMigrationTests|FullyQualifiedName~PaymentMigrationTests' --logger 'trx;LogFileName=renewal-foundation.trx' --results-directory artifacts/renewal-foundation`. Đặt BOILERATE_TEST_POSTGRES tới cluster test cô lập; MSSQL dùng LocalDB fallback. Chạy trên working tree có API đang chuẩn bị, chỉ xác nhận phạm vi domain/migration ở lượt này; không xác nhận API/deployment.
5. Sau bổ sung6 integration cases receipt rollback/NeedsReview/Release: build **0 lỗi/1283warnings**,18.47s. Lượt API `dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~RenewalIntegrationTests' --logger 'trx;LogFileName=renewal-api.trx' --results-directory artifacts/renewal-api`: **34/34 pass,0 fail/skip**,1m21s; gồm17 trường hợp trên mỗi provider, kể cả HTTP JWT/ACL/no-store và commit failure thật tại interceptor. Không có gọi dịch vụ gia hạn/SMTP bên ngoài.
6. Regression chung: `dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'Runtime!=Container&Runtime!=Recovery&Smoke!=Template' --logger 'trx;LogFileName=renewal-regression.trx' --results-directory artifacts/renewal-regression`: **398/398 pass,0 fail/skip**,6m29s. Source code tại `33f0310`; thay đổi tiếp theo chỉ docs. Có72 ca renewal mới (32 domain,6 migration,34 integration) và326 regression hiện hữu; TRX có110 ca mỗi provider. Không cộng52/34 lượt targeted vào398. Không loại ca thất bại hoặc nới assertion; cả ba lượt test ngày19 đều đạt lần chạy đã ghi.
7. Đối chiếu counters TRX foundation/API/regression: executed=total=passed, failed/notExecuted=0. PostgreSQL cluster test port55439 đã dừng sạch bằng pg_ctl fast, exit0. Raw logs/TRX tiếp tục bị ignore. Kiểm tra cuối **63 tệp Markdown,361 liên kết nội bộ,0 hỏng**; `git diff --check` đạt. Không có failure bị bỏ khỏi báo cáo; vấn đề dung lượng Unicode được phát hiện/sửa trong review trước lượt test ngày19, không phải một test failure được suy đoán.

## Phạm vi cần kiểm

Domain lease/Start/PendingVerification/target lock/NotApplied→attempt mới/expiry thật/UTC; FK/check/UQ/history và migration empty Down/Up; Paid provisioning/replay; tranh chấp nhân viên/cùng target; mất response/restart; rollback; owner/ACL/reveal/audit/no-store; Unicode evidence; đơn manual-only và mixed physical/digital/manual.

## Chưa nghiệm thu

Gia hạn ở dịch vụ ngoài vẫn do nhân viên thực hiện và xác minh; không có provider API/LMS. Quy trình không bảo đảm exactly-once nếu bỏ qua Start/verification hoặc dùng alias khác cho cùng tài khoản. Linux/Docker/CI thật, SMTP/gateway/carrier và restore deployment còn mở. Container/Recovery/Template sẽ được loại khỏi regression local; không coi chúng là đã pass.
