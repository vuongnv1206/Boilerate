# Nghiệm thu và phục hồi Catalog

Phạm vi: installation test cô lập. Kế hoạch và trạng thái tại [operations acceptance](features/operations-acceptance.md). Không coi runbook là bằng chứng một môi trường đã chạy đạt.

Kho số dùng Data Protection để mã hóa nội dung: luôn cấu hình key ring bền vững và backup **database + keys** cùng recovery set. Giữ ApplicationName/purpose nhất quán sau restore; mất key hoặc xóa key cũ làm nội dung không giải mã được. Giới hạn quyền đọc backup/key volume; không đưa chúng vào Git hoặc artifacts công khai. [Lát kho số](features/digital-inventory.md) chưa bổ sung cấp phát/gia hạn theo đơn; nghiệm thu dữ liệu kho số phục hồi trên deployment thật vẫn cần chạy riêng.

Ordering cần apply `OrderingCheckout` trước khi bật luồng giỏ/đơn. Worker hết hạn chạy cùng Catalog/Commerce worker, mặc định mỗi15giây xử lý tối đa50đơn; `Ordering__WorkerIntervalSeconds` và `Ordering__ExpirationBatchSize` cấu hình được. `Ordering__ReservationMinutes=30` áp dụng cho đơn trả trước mới, không đổi hạn đã lưu của đơn cũ; COD không có TTL trả trước. Phí ship phẳng `Ordering__FlatShippingAmount=0` cần đặt đúng chính sách shop trước vận hành. Worker tắt hoặc downtime làm giữ hàng quá hạn lâu hơn, không cho bán vượt tồn; khi khởi lại, các batch tiếp tục nhả hàng từ trạng thái DB. Chưa có nhận tiền/giao hàng trong bước này.

Backup/restore phải giữ Orders, OrderItems, physical reservations, balances/Reserved và digital reservation owners cùng một database snapshot. Không sửa Reserved/state thủ công để dọn hàng; dùng hủy/expiry. `OrderingCheckout` Down bị chặn khi còn holds; ngay cả khi hết holds, Down xóa lịch sử Order nên backup và dừng writer/worker trước thao tác. Chưa có nghiệm thu deployment restore cho nghiệp vụ Order ở đợt code này.

## 1. Các gate độc lập

Chạy PowerShell 7.2+ tại root repo. `scripts/verify-operations.ps1` tạo thư mục ngẫu nhiên `artifacts/operations/<time>-<id>`, ghi `summary.json`, log restore/build/test và TRX. Summary chỉ được ghi `passed` khi có ca thực sự chạy đạt, không có fail/skip; Recovery/Container phải có ca passed cho cả SQL Server/PostgreSQL. `blocked` và exit code khác 0 nghĩa là điều kiện môi trường chưa đáp ứng.

Runner ghi commit, trạng thái working tree, OS, thời điểm và kết quả; không ghi connection strings/keys. Raw artifacts chỉ lưu local hoặc trong CI artifact có quyền truy cập phù hợp, không commit. Dùng `-SkipBuild` chỉ khi đã build đúng source/configuration hiện tại.

### Local: key ring và trusted proxy

```powershell
./scripts/verify-operations.ps1 -Stage Local
```

Gate kiểm key ring qua hai service providers và forwarding middleware thật: trusted peer được forward, untrusted peer không được giả client/protocol, giới hạn một hop. Không cần database/Docker; kết quả không thay thế container acceptance.

### Recovery: native DB + media + keys + outbox

```powershell
$env:BOILERATE_TEST_POSTGRES = '<isolated test server connection>'
# Windows có thể dùng LocalDB mặc định; máy khác phải cung cấp SQL Server test:
$env:BOILERATE_TEST_MSSQL = '<isolated SQL Server connection>'
# Tùy chọn nếu pg_dump/pg_restore chưa ở PATH:
$env:BOILERATE_POSTGRES_BIN = '<PostgreSQL client bin directory>'
./scripts/verify-operations.ps1 -Stage Recovery
```

SQL Server ngoài LocalDB cần `BOILERATE_RECOVERY_SQL_BACKUP_DIRECTORY` là thư mục test riêng mà cả SQL Server process và runner đọc/ghi được; không giả định host path tồn tại bên trong container. PostgreSQL native client phải tương thích server. Chỉ bật trực tiếp theory bằng `BOILERATE_TEST_RECOVERY=1` khi hiểu các điều kiện này; runner đặt rồi khôi phục env flag cho lượt của nó.

Drill tạo database nguồn/đích `boilerate_test_<guid>` và thư mục test riêng, seed Catalog/kho/media/outbox, dừng writer/worker trong cửa sổ snapshot, dùng backup/restore native vào đích mới và copy media/key ring. Sau snapshot thay đổi nguồn để phân biệt recovery point. Dựng provider mới cho dữ liệu phục hồi, kiểm giá/category/stock/movement, bytes ảnh, Data Protection và chạy lại cleanup. Không restore đè database nghiệp vụ; không dùng `WITH REPLACE` để chiếm database có sẵn. Các tài nguyên test được dọn theo tên/path đã kiểm tra.

### Container: Linux và image production

```powershell
docker build -t boilerate-ci .
# Cần server test SQL/PG truy cập được từ Linux host và container:
$env:BOILERATE_TEST_MSSQL = '<SQL test connection>'
$env:BOILERATE_TEST_POSTGRES = '<PG test connection>'
./scripts/verify-operations.ps1 -Stage Container -ContainerImage boilerate-ci
```

Gate hiện dùng Linux host networking; chạy Linux CI hoặc Linux host có Docker engine. Không dùng LocalDB/Windows host làm bằng chứng cho gate này. Fixture tạo database/migrate riêng; image production chạy operator bootstrap và preflight trước HTTP. HTTP startup không tự đổi schema hoặc cấp ACL. Kiểm non-root, live/ready, login/upload; remove/recreate container với cùng /data, đối chiếu media/key ring/session. Cleanup chỉ xóa container/volume của ca test.

Gate container không tự xác nhận DNS/TLS/load balancer hoặc cấu hình proxy/volume của một production installation. Cần smoke đúng cấu hình được deploy trước khi đánh dấu môi trường đó nghiệm thu.

## 2. Thứ tự phục hồi một installation

1. Chọn recovery point, dừng traffic ghi và worker. Giữ bản sao hiện tại phục vụ điều tra; xác nhận đích và quyền operator.
2. Restore database, media và Data Protection key ring từ cùng cửa sổ backup vào môi trường cô lập. Bảo vệ key/backup như dữ liệu nhạy cảm; kiểm hash và quyền truy cập.
3. Giữ đúng DB provider, application name/purpose và cấu hình bảo vệ cần thiết. Chạy migration/preflight riêng; không khởi HTTP với quyền tự bootstrap.
4. Kiểm product/SKU/category, giá, kho/history, ảnh và khả năng đọc dữ liệu được Data Protection bảo vệ. Kiểm outbox pending/processing hết lease/dead-letter; chỉ mở worker khi storage tham chiếu đúng installation phục hồi.
5. Xử lý khoảng trống từ recovery point đến thời điểm sự cố. Restore DB cũ có thể khôi phục session/quyền đã thu hồi sau backup: trước mở traffic phải quyết định và thực hiện vô hiệu hóa session/token phù hợp, đối soát thay đổi quyền và dữ liệu mất sau snapshot.
6. Mở đọc/ghi/worker có kiểm soát, theo dõi health, backlog/age/dead-letter và lỗi storage. Ghi RPO/RTO đo thực tế, các bước thủ công và kết quả; local test không xác nhận SLA production.

## 3. Adapter ngoài

SMTP, cloud storage, Redis, OAuth và jobs được nghiệm thu riêng theo cấu hình bật thật. Không ghi credential vào docs/log; giữ kết quả, endpoint đã che và phạm vi kiểm tra. Không bật provider thanh toán/vận chuyển trước khi có hồ sơ tính năng và thiết kế tương ứng.
