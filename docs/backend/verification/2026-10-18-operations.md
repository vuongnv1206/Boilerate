# Kiểm chứng vận hành — 2026-10-18

Timezone Asia/Saigon. Source đầu đợt: 4ddbb67; các thay đổi nghiệm thu được thực hiện trong working tree sau commit này, không trình bày test như chạy trên checkout 4ddbb67 nguyên trạng. Kế hoạch: [operations acceptance](../features/operations-acceptance.md), lệnh: [runbook](../operations-runbook.md).

## Môi trường và điều kiện

Windows, .NET 10, SQL Server LocalDB và PostgreSQL 16 ở cluster test riêng localhost:55439. Chỉ dùng database boilerate_test_<guid> và thư mục temp ngẫu nhiên của drill. Raw backup/media/key/log/TRX không được commit.

Docker CLI 24.0.2 có mặt. Probe đầu không có daemon pipe; sau khởi Docker Desktop nền, các probe docker info vẫn timeout sau 8 giây. Chưa có bằng chứng container/Linux/CI chạy đạt trong đợt này.

## Kết quả theo bước

| Lượt | Kết quả thực tế | Bằng chứng local |
|---|---|---|
| Release build working tree đầu đợt | 0 errors, 2.041 analyzer/style warnings | artifacts/operations-build.log |
| Build incremental sau review cleanup/TLS của native helper | 0 errors, 671 warnings (chỉ phần được build lại; không coi là giảm tổng warnings) | artifacts/operations-recovery-build.log |
| Local proxy/key gate | **5 passed, 0 failed, 0 skipped** | artifacts/operations/20261017-180846-1c2be7e0f6254c26a04825c0acabc1e9/operations-local.trx và summary.json |
| Native Recovery qua hai provider | **2 passed, 0 failed, 0 skipped** | artifacts/operations/20261017-181028-8554c7b4cc764d21a2c8f36fe20ca606/operations-recovery.trx và summary.json |
| Catalog regression sau thay đổi fixture | **2 passed, 0 failed, 0 skipped** | artifacts/operations-compatibility/catalog-compatibility.trx |
| Container runner trên host Windows | **blocked, 0 ca chạy**; gate yêu cầu Linux host | artifacts/operations/20261017-180936-513075d3043e4402836e3c4075571eb3/summary.json |

Tên thư mục runner dùng UTC (2026-10-17T18:xxZ), tương ứng ngày 2026-10-18 Asia/Saigon. Không cộng các lượt thành full regression hoặc thay thế các kết quả ngày 2026-10-17. Không chạy lại toàn bộ suite trong đợt này vì thay đổi tập trung ở fixtures/gates/runbook; hai regression Catalog dùng cấu hình fixture mặc định để kiểm tính tương thích.

Lệnh đã chạy sau build (PostgreSQL trỏ cluster test riêng; LocalDB mặc định Windows):

```powershell
dotnet build tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-restore
./scripts/verify-operations.ps1 -Stage Local -SkipBuild
./scripts/verify-operations.ps1 -Stage Recovery -SkipBuild
dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~CatalogRelationsTypedFiltersPublicationAndVersionsWork'
./scripts/verify-operations.ps1 -Stage Container -ContainerImage boilerate-ci -SkipBuild
```

Recovery sử dụng BACKUP COPY_ONLY/CHECKSUM + VERIFYONLY + RESTORE/MOVE của SQL Server, và pg_dump/pg_restore của PostgreSQL 16. Sau snapshot đổi nguồn (tồn 7→10, tên/category), bản phục hồi vẫn giữ tồn 7 và dữ liệu lúc backup. Manifest media/keys, unprotect payload, movement gốc, reclaim expired lease và source storage isolation đều được kiểm. Thư mục/temp/database của drill được cleanup bởi fixture.

Container vẫn chưa nghiệm thu; CLI/bootstrap/preflight/recreate trong production image là code gate đã chuẩn bị, chưa phải bằng chứng image chạy thực tế. CI đã chuyển qua runner để không pass khi không có ca chạy hoặc thiếu một provider; chưa có run CI mới được dispatch từ lượt local này.

Kiểm tài liệu cuối đợt: 27 file Markdown, 100 liên kết tương đối hợp lệ và `git diff --check` đạt. Summary đối chiếu TRX/docs tại artifacts/operations/documentation-check.json. Commit code đã có: `5692394`, `08ddfdd`; commit tổng hợp tài liệu tra trong [history](../history/README.md).

## Phạm vi không được suy diễn

Local recovery kiểm dữ liệu/native DB backup/media/key/outbox và recovery point; không xác nhận production RPO/RTO, session policy đã được thực thi trong production, native backup của remote server có volume khác, hoặc adapter ngoài. Hiện không có Ordering/Payment/Shipping mới trong đợt này và không đổi schema/API thương mại.
