# Nghiệm thu và phục hồi Catalog

Phạm vi: installation test cô lập. Kế hoạch và trạng thái tại [operations acceptance](features/operations-acceptance.md). Không coi runbook là bằng chứng một môi trường đã chạy đạt.

Kho số và snapshot hàng số đã giao dùng Data Protection: luôn cấu hình key ring bền vững và backup **database + keys** cùng recovery set. Giữ ApplicationName/purpose nhất quán sau restore; mất key hoặc xóa key cũ làm nội dung không giải mã được. Kho dùng `Boilerate.DigitalInventory.Content.v1` + StockItemId; snapshot giao dùng `Boilerate.Ordering.DigitalDelivery.Content.v1` + DeliveryId. Không hoán đổi ciphertext giữa hai mục đích/ID. Giới hạn quyền đọc backup/key volume; không đưa chúng vào Git hoặc artifacts công khai. [Kho số](features/digital-inventory.md) và [nhận tiền/giao số](features/payment-digital-delivery.md) có code; thao tác gia hạn và nghiệm thu phục hồi trên deployment thật vẫn cần chạy riêng.

Ordering cần apply `OrderingCheckout`, và nhận tiền/giao số cần thêm `PaymentDigitalDelivery`, trước khi bật luồng tương ứng. Worker hết hạn chạy cùng Catalog/Commerce worker, mặc định mỗi15giây xử lý tối đa50đơn; `Ordering__WorkerIntervalSeconds` và `Ordering__ExpirationBatchSize` cấu hình được. `Ordering__ReservationMinutes=30` áp dụng cho đơn trả trước mới, không đổi hạn đã lưu của đơn cũ; COD không có TTL trả trước. Phí ship phẳng `Ordering__FlatShippingAmount=0` cần đặt đúng chính sách shop trước vận hành. Worker tắt hoặc downtime làm giữ hàng quá hạn lâu hơn, không cho bán vượt tồn; khi khởi lại, các batch tiếp tục nhả hàng chưa trả từ trạng thái DB. Paid không bị hết hạn hoặc hủy qua luồng nhả hàng này.

Backup/restore phải giữ Orders, OrderItems, PaymentReceipts, DigitalDeliveries, physical reservations, balances/Reserved và digital reservation/delivered owners cùng một database snapshot. Không sửa Reserved/state thủ công để dọn hàng; dùng hủy/expiry cho đơn chưa trả. Unique Delivered không được nhả/bật lại kể cả khi có xử lý hoàn tiền về sau. `PaymentDigitalDelivery` Down bị chặn khi có receipt/delivery/mã đã giao; `OrderingCheckout` Down bị chặn khi còn holds. Ngay cả khi hết holds, Ordering Down xóa lịch sử Order nên backup và dừng writer/worker trước thao tác. Chưa có nghiệm thu deployment restore cho nghiệp vụ Order/Payment ở đợt code này.

## Xác nhận tiền chuyển khoản và xử lý chờ giao

Đây là xác nhận thủ công từ ngân hàng, chưa có gateway/webhook/đối soát tự động. Cấp `Payment.Confirm` và `Order.Fulfill` có chủ đích bằng operator CLI; HTTP startup không tự grant. Các hợp đồng request/error ở [API commerce](commerce-api.md).

1. Đọc chi tiết đơn, đối chiếu số đơn, tổng/currency snapshot và khoản tiền thực nhận tại ngân hàng. Chỉ xác nhận đúng toàn bộ tổng VND; khoản thiếu/thừa cần xử lý riêng, chưa được hỗ trợ trong lát này.
2. Gửi payment-confirmations với Version hiện tại, OperationId mới, reference ngân hàng duy nhất và ReceivedAtUtc thực tế. Nếu mất response, gửi lại chính payload/OperationId; không tạo mã mới hoặc gọi điều chỉnh kho để bù.
3. Kiểm PaymentStatus và FulfillmentStatus riêng. Unique/Shared đủ hàng sẽ có snapshot trong Đơn hàng của tôi; Physical giữ reservation, ManualExtension chờ xử lý và không tự gia hạn. Core hiện chưa gửi email; notification outbox là bước kế tiếp.
4. Nếu Paid + NeedsReview, kiểm tồn từng SKU và audit metadata. `DigitalContentUnavailable` nghĩa là cần kiểm tra key ring/ciphertext; khôi phục đúng key, không ghi nội dung bí mật vào log. Hệ thống không giữ/cấp một phần mới khi thiếu hàng hoặc lỗi crypto; holds cũ vẫn còn và không bị expiry sau khi đã trả.
5. Sau khi sửa nguyên nhân, đọc Version mới rồi gọi retry-fulfillment bằng quyền Order.Fulfill. Không ghi receipt thứ hai hoặc tự nhả Unique Delivered. Đơn Cancelled nhận tiền chỉ ghi Paid + NeedsReview, không tự giao hoặc cho retry; hoàn tiền/hủy sau nhận tiền thuộc mốc tiếp theo.

Luồng xác nhận/giao số core không phụ thuộc SMTP hoặc worker email. Phân biệt code đã có, kiểm chứng local được ghi trong hồ sơ và nghiệm thu ngân hàng/SMTP/deployment thật; runbook này không xác nhận adapter đã đạt.

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
4. Kiểm product/SKU/category, giá, kho/history, Orders/receipts/deliveries/stock owners, ảnh và khả năng đọc cả nội dung kho số lẫn snapshot giao được Data Protection bảo vệ. Kiểm outbox pending/processing hết lease/dead-letter; chỉ mở worker khi storage tham chiếu đúng installation phục hồi.
5. Xử lý khoảng trống từ recovery point đến thời điểm sự cố. Tiền đã vào ngân hàng và mã khách đã nhận không bị đảo ngược khi restore database cũ: đối chiếu các giao dịch/cấp phát sau snapshot trước khi mở lại bán/giao, tránh bán lại mã hoặc ghi nhận lại khoản tiền. Restore DB cũ còn có thể khôi phục session/quyền đã thu hồi sau backup: trước mở traffic phải quyết định và thực hiện vô hiệu hóa session/token phù hợp, đối soát thay đổi quyền và dữ liệu mất sau snapshot.
6. Mở đọc/ghi/worker có kiểm soát, theo dõi health, backlog/age/dead-letter và lỗi storage. Ghi RPO/RTO đo thực tế, các bước thủ công và kết quả; local test không xác nhận SLA production.

## 3. Adapter ngoài

SMTP, cloud storage, Redis, OAuth và jobs được nghiệm thu riêng theo cấu hình bật thật. Không ghi credential vào docs/log; giữ kết quả, endpoint đã che và phạm vi kiểm tra. Không bật provider thanh toán/vận chuyển trước khi có hồ sơ tính năng và thiết kế tương ứng.
