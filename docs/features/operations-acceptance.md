# Nghiệm thu vận hành Catalog v1

Ngày bắt đầu/cập nhật: 2026-10-18 (Asia/Saigon). Trạng thái: đang triển khai. Mốc roadmap: runtime và hoàn thiện nghiệm thu mốc đầu.

## 1. Mục tiêu và phạm vi

Catalog/giá/kho/media-outbox đã có kiểm chứng local; chưa đủ bằng chứng để đánh dấu nghiệm thu triển khai. Bước tiếp theo là kiểm chứng các điều kiện vận hành trước khi thêm checkout/Orders.

Trong đợt này: kiểm tra lại Docker; tăng độ chặt của gate container hiện có (operator preflight/bootstrap trong image, health, thay container giữ volume); kiểm chứng trusted proxy bằng middleware thật; diễn tập native backup/restore qua SQL Server/PostgreSQL cùng media/key ring và outbox. Không chọn payment/shipping provider và không sửa schema thương mại.

Tiêu chí:

- [x] Header giả từ peer không được tin bị bỏ qua; proxy đã khai báo được phép forward đúng một hop (5/5 local tests gồm cả key ring).
- [ ] Image chạy operator/bootstrap/preflight tách HTTP; non-root, live/ready, media/key/session còn dùng được sau remove/recreate container trên cùng volume.
- [ ] Native DB backup được restore vào database test mới; dữ liệu, media, key ring và outbox phục hồi đúng recovery point trên cả hai provider.
- [ ] Worker sau restore không xóa ảnh còn được tham chiếu hoặc file ở installation nguồn.
- [ ] Runner/báo cáo ghi đúng pass/fail/skip/blocked; không coi Docker không sẵn sàng hoặc test bị skip là nghiệm thu đạt.

## 2. Thiết kế

Không đổi entity, cardinality, PK/FK/index hay migration. Giữ [database design](../commerce-database.md). API thương mại không đổi.

- Container gate tiếp tục dùng Linux CI và hai DB test riêng. Chỉ thao tác container/volume mang tên ngẫu nhiên của ca test. Không tự migrate/bootstrap ở HTTP startup.
- Recovery dùng database `boilerate_test_<guid>` và thư mục ngẫu nhiên đã kiểm tra đường dẫn; không restore đè database có sẵn và không dùng database nghiệp vụ.
- Quiesce writer/worker trong cửa sổ backup; copy media/keys cùng recovery point. SQL Server dùng native BACKUP/RESTORE; PostgreSQL dùng pg_dump/pg_restore. Native tool/đường dẫn backup là điều kiện chạy riêng, không được thay bằng mock.
- Sau restore dựng service provider mới, giữ cùng app name/key material cần thiết; kiểm tra dữ liệu/media/key/outbox rồi mới dọn tài nguyên của drill.
- Không commit raw logs, backup, secrets hay key files. Local drill không xác nhận production RPO/RTO, host Linux hoặc adapter ngoài. Restore snapshot cũ có thể phục hồi session/quyền đã thu hồi sau backup; runbook phải nêu chính sách vô hiệu hóa phù hợp trước mở lại traffic.

## 3. Kế hoạch từng bước

| Bước | Công việc | Đầu ra | Trạng thái |
|---|---|---|---|
| 1 | Đối chiếu roadmap và kiểm tra môi trường | Ghi rõ phần code/local verified và phần chưa nghiệm thu | Đã đối chiếu; Docker engine tiếp tục timeout |
| 2 | Tăng độ chặt gate container và proxy regression | Operator CLI, recreate volume và trusted/untrusted forwarding | Local proxy/key gate 5/5; container gate code có, chưa chạy image |
| 3 | Native recovery drill trên DB mới | Backup/restore DB+media+keys, kiểm dữ liệu/outbox qua hai DB | Đã viết code; chờ build/chạy |
| 4 | Runner và kiểm chứng thực tế | Lệnh tái chạy, kết quả thật và giới hạn | Runner đã viết; đang kiểm chứng |
| 5 | Docs/history/roadmap và commit nhỏ | Bằng chứng tracked; không đánh dấu container đạt nếu chưa chạy | Chưa bắt đầu |

## 4. Nhật ký

### 2026-10-18 — Bước 1

Đã đọc roadmap, Catalog plan và báo cáo 2026-10-17. Working tree ban đầu sạch, HEAD 4ddbb67. Docker CLI 24.0.2 có mặt; lần probe đầu daemon pipe chưa tồn tại. Đã khởi chạy Docker Desktop nền để kiểm tra lại; chưa coi môi trường Docker sẵn sàng.

### 2026-10-18 — Bước 2–3: triển khai các ca nghiệm thu

- Container gate đã chuyển bootstrap/preflight sang production image CLI, thêm live/ready và remove/recreate giữ volume; bootstrap env tách HTTP env và được dọn sớm.
- Thêm 3 ca TestServer cho trusted/untrusted peer và một hop forwarding; giữ kiểm key ring qua hai service providers.
- Thêm opt-in native recovery theory (Runtime=Recovery), helper BACKUP/RESTORE và pg_dump/pg_restore; fixture chỉ cho override cấu hình runtime, không thay database ownership.
- Thêm runner Local/Recovery/Container ghi summary/TRX và thất bại khi selected cases bị skip hoặc thiếu coverage provider. [Runbook](../operations-runbook.md) ghi điều kiện native tools/backup path, quiesce và session invalidation sau restore.
- Build đang chạy; chưa ghi ca nào là passed ở thời điểm cập nhật này. Docker engine timeout 8 giây ở các probe sau khởi động nên gate container chưa chạy.

### 2026-10-18 — Bước 2: kết quả local

Release build đạt 0 errors; còn warnings analyzer/style. `verify-operations.ps1 -Stage Local -SkipBuild` đạt 5/5, không skip. `-Stage Container` trên Windows ghi blocked/0 ca vì gate cần Linux host; các probe engine riêng cũng timeout. Đây là hai điều kiện chưa đáp ứng, không phải container acceptance passed. Native recovery đang chạy, sẽ ghi kết quả sau khi hoàn tất.

## 5. Kiểm chứng và công việc còn lại

Chưa có kết quả chạy mới cho các bước 2–4 tại thời điểm lập kế hoạch. Sẽ bổ sung lệnh/môi trường/pass/fail/skip và raw artifact path sau từng bước. Các kết quả 70/70 và 9/9 của ngày trước không thay thế nghiệm thu mới.

Lịch sử commit sẽ được ghi bằng hash thực tế sau khi commit; không ghi hash dự đoán.
