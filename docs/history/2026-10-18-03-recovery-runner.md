# Native recovery drill và runner nghiệm thu

Ngày: 2026-10-18, Asia/Saigon.

1. Thêm RecoveryRuntimeTests/NativeRecoveryBackup: opt-in, native SQL Server COPY_ONLY/CHECKSUM/VERIFYONLY/RESTORE và PostgreSQL pg_dump/pg_restore. Chỉ thao tác database fixture ngẫu nhiên, restore vào đích mới, không WITH REPLACE. Partial backup ngoài temp cũng được cleanup; native credentials không đưa vào argument logs.
2. FoundationDatabase cho override cấu hình runtime để tách storage/key roots, từ chối override database ownership. Quiesce writer/worker, snapshot DB/media/keys; thay nguồn sau snapshot và kiểm recovery point trên provider mới.
3. Kiểm N–N category, Product Published/giá/SKU, tồn 7/movement gốc, key unprotect và ảnh; outbox reclaim lease, xóa orphan ở restored storage, giữ linked media và toàn bộ source installation.
4. Thêm verify-operations.ps1 với Local/Recovery/Container, JSON/TRX và exit code; không pass khi zero tests/skip/fail hoặc thiếu một provider. CI container dùng runner này. Viết [runbook](../operations-runbook.md) với điều kiện native client/backup path và session invalidation khi phục hồi snapshot cũ.

Kiểm chứng: Release build 0 errors. Recovery gate **2 passed, 0 failed, 0 skipped**, native trên LocalDB và PostgreSQL 16; evidence `artifacts/operations/20261017-181028-8554c7b4cc764d21a2c8f36fe20ca606/operations-recovery.trx`. Runner xác nhận có case passed cho cả hai provider. Source tương ứng là working tree sau 4ddbb67, như summary sourceDirty=true; không tuyên bố đây là lượt chạy checkout nguyên trạng của commit đầu đợt.

Giới hạn: drill không khởi HTTP/session flow và không xác nhận production RPO/RTO hoặc container/Linux. Container runner trên Windows ghi blocked/0 ca; engine Docker riêng vẫn timeout. [Verification](../verification/2026-10-18-operations.md) tổng hợp lệnh/kết quả và regression compatibility.

Tiếp theo: nghiệm thu gate image trên Linux CI/Docker hoạt động; áp dụng runbook trên môi trường triển khai và adapter thật trước checkout.

Commit subject: `test(operations): add native recovery drills and acceptance runner`.
