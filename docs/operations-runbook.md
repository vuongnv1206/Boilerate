# Vận hành và phục hồi shop

Phạm vi: installation test cô lập. Kế hoạch và trạng thái tại [operations acceptance](features/operations-acceptance.md). Không coi runbook là bằng chứng một môi trường đã chạy đạt.

Kho số và snapshot hàng số đã giao dùng Data Protection: luôn cấu hình key ring bền vững và backup **database + keys** cùng recovery set. Giữ ApplicationName/purpose nhất quán sau restore; mất key hoặc xóa key cũ làm nội dung không giải mã được. Kho dùng `Boilerate.DigitalInventory.Content.v1` + StockItemId; snapshot giao dùng `Boilerate.Ordering.DigitalDelivery.Content.v1` + DeliveryId. Không hoán đổi ciphertext giữa hai mục đích/ID. Giới hạn quyền đọc backup/key volume; không đưa chúng vào Git hoặc artifacts công khai. [Kho số](features/digital-inventory.md) và [nhận tiền/giao số](features/payment-digital-delivery.md) có code; thao tác gia hạn và nghiệm thu phục hồi trên deployment thật vẫn cần chạy riêng.

Ordering cần `OrderingCheckout`, nhận tiền/giao số cần `PaymentDigitalDelivery`, và xuất giao/COD cần `PhysicalShipping`; installation mới apply đủ sáu migrations của provider trước khi bật luồng tương ứng. Migration Shipping là `20261018151738_PhysicalShipping` (SQL Server) hoặc `20261018151746_PhysicalShipping` (PostgreSQL). Worker hết hạn chạy cùng Catalog/Commerce worker, mặc định mỗi15giây xử lý tối đa50đơn; `Ordering__WorkerIntervalSeconds` và `Ordering__ExpirationBatchSize` cấu hình được. `Ordering__ReservationMinutes=30` áp dụng cho đơn trả trước mới, không đổi hạn đã lưu của đơn cũ; COD không có TTL trả trước. Phí ship phẳng `Ordering__FlatShippingAmount=0` cần đặt đúng chính sách shop trước vận hành. Worker tắt hoặc downtime làm giữ hàng quá hạn lâu hơn, không cho bán vượt tồn; khi khởi lại, các batch tiếp tục nhả hàng chưa trả từ trạng thái DB. Paid không bị hết hạn hoặc hủy qua luồng nhả hàng này. COD đã có lần xuất giao cũng không được hủy/nhả tồn qua API cũ.

Backup/restore phải giữ Orders, OrderItems, PaymentReceipts, DigitalDeliveries, Shipments/ShipmentItems, stock movements, physical reservations/ConsumedQuantity, balances/Reserved và digital reservation/delivered owners cùng một database snapshot. Không sửa Reserved/state thủ công để dọn hàng; dùng hủy/expiry chỉ cho đơn chưa trả và chưa xuất. Unique Delivered không được nhả/bật lại kể cả khi có xử lý hoàn tiền về sau. `PhysicalShipping` Down chặn trước DDL khi có shipment, consumed stock/dispatch flag, receipt/digital delivery/Paid hoặc Completed order, digital Reserved/Delivered hay physical holds; guard cố ý gồm cả dữ liệu không liên quan shipping để không gỡ một phần chuỗi rồi thất bại ở migration cũ. `PaymentDigitalDelivery` Down bị chặn khi có receipt/delivery/mã đã giao; `OrderingCheckout` Down bị chặn khi còn holds. Ngay cả khi hết holds, Ordering Down xóa lịch sử Order nên backup và dừng writer/worker trước thao tác. Ưu tiên forward migration hoặc restore nhất quán. Chưa có nghiệm thu deployment restore cho nghiệp vụ Order/Payment/Shipping ở đợt code này.

## Xác nhận tiền và xử lý chờ giao

Đây là xác nhận thủ công từ ngân hàng hoặc COD đã thực nhận, chưa có gateway/webhook/đối soát tự động. Cấp `Payment.Confirm` và `Order.Fulfill` có chủ đích bằng operator CLI; HTTP startup không tự grant. Các hợp đồng request/error ở [API commerce](commerce-api.md).

1. Đọc chi tiết đơn, đối chiếu số đơn, tổng/currency snapshot và khoản tiền thực nhận tại ngân hàng. Chỉ xác nhận đúng toàn bộ tổng VND; khoản thiếu/thừa cần xử lý riêng, chưa được hỗ trợ trong lát này.
2. Gửi payment-confirmations với Version hiện tại, OperationId mới, reference ngân hàng duy nhất và ReceivedAtUtc thực tế. Nếu mất response, gửi lại chính payload/OperationId; không tạo mã mới hoặc gọi điều chỉnh kho để bù.
3. Kiểm PaymentStatus và FulfillmentStatus riêng. Unique/Shared đủ hàng sẽ có snapshot trong Đơn hàng của tôi; Physical giữ reservation, ManualExtension chờ xử lý và không tự gia hạn. Giao số commit cùng notification outbox; email chỉ hướng dẫn vào đơn, không gửi nội dung bí mật.
4. Nếu Paid + NeedsReview, kiểm tồn từng SKU và audit metadata. `DigitalContentUnavailable` nghĩa là cần kiểm tra key ring/ciphertext; khôi phục đúng key, không ghi nội dung bí mật vào log. Hệ thống không giữ/cấp một phần mới khi thiếu hàng hoặc lỗi crypto; holds cũ vẫn còn và không bị expiry sau khi đã trả.
5. Sau khi sửa nguyên nhân, đọc Version mới rồi gọi retry-fulfillment bằng quyền Order.Fulfill. Không ghi receipt thứ hai hoặc tự nhả Unique Delivered. Đơn Cancelled nhận tiền chỉ ghi Paid + NeedsReview, không tự giao hoặc cho retry; hoàn tiền/hủy sau nhận tiền thuộc mốc tiếp theo.

Các bước trên dùng Source mặc định `ManualBankTransfer`. Với tiền COD, dùng `ManualCOD` chỉ sau khi toàn bộ số lượng vật lý đã Delivered, đúng đủ tổng VND; không xác nhận tiền chỉ vì nhân viên đã đánh dấu giao. Nếu khách chuyển khoản trong khi COD mới giao một phần, dùng ManualBankTransfer: hệ thống đối chiếu shipment hiện có, không giữ lại toàn bộ số hàng đã xuất. Kiểm PaymentStatus độc lập với FulfillmentStatus; một receipt/đơn, reference duy nhất trong cùng Source. Thiếu/thừa tiền, hoàn tiền hoặc thu COD từng phần chưa có nghiệp vụ hỗ trợ.

Luồng xác nhận/giao số commit không phụ thuộc SMTP. Worker gửi thông báo DigitalDeliveryReady sau commit qua cấu hình SMTPEmailSettings hiện hữu (host/port/TLS/credential/from); không ghi secrets vào Git. Giữ Commerce worker bật để gửi/retry; khi email lỗi, khách vẫn đọc được nội dung đã giao trong đơn. Theo dõi backlog/oldestAge/deadLetters, sửa cấu hình rồi replay bằng Outbox.Replay; không xác nhận tiền/giao lại chỉ để gửi email. Crash sau gửi trước ack có thể lặp email, Message-Id ổn định không bảo đảm dedup. Phân biệt kiểm chứng mock local với nghiệm thu ngân hàng/SMTP/deployment thật; runbook này không xác nhận adapter đã đạt.

## Xuất giao vật lý và xác nhận giao thành công

Shipping có service/API thủ công, trạng thái kiểm chứng ở [hồ sơ tính năng](features/physical-shipping.md). Chưa có carrier/callback tự động. Sau migrate, chạy `--register-capabilities`, rồi cấp có chủ đích các cặp Shipping.View/Dispatch/Deliver cho nhân viên; `--grant-admin-capabilities` chỉ dùng khi operator muốn cấp đầy đủ cho admin. Không suy quyền Shipping từ Order.View hoặc quyền nhận tiền.

1. Đọc đơn và các shipment hiện có. Prepaid/mixed phải Paid, không NeedsReview; COD thuần vật lý có thể xuất trước khi nhận tiền. Kiểm đúng số lượng vật lý còn giữ, đơn vị vận chuyển và mã theo dõi thực tế.
2. POST `/api/admin/orders/{id}/shipments` với OrderVersion hiện tại, OperationId mới và items(OrderItemId,Quantity). Đây là xuất giao thật: OnHand và Reserved cùng giảm, ledger âm và lịch sử shipment commit trong một transaction. Ví dụ tồn5/giữ3, xuất2 thì tồn3/giữ1/Available2; không adjustment thêm để trừ lần hai.
3. Nếu mất response, retry chính OperationId/payload; không tạo shipment mới để thay cho một lần xuất chưa biết kết quả. Đọc lại Shipment.Version và OrderVersion trả về. Phần chưa xuất tiếp tục được giữ; lập shipment khác khi có lần xuất thực tế tiếp theo.
4. Khi đã xác minh giao thành công, gọi `.../shipments/{shipmentId}/deliver` với OrderVersion, Version của Shipment và OperationId delivery mới. Server ghi timestamp UTC. Replay cùng operation không đổi thời điểm giao hoặc cấp lại hàng; API GET owner/admin đều no-store.
5. Đối chiếu trạng thái riêng: shipment Delivered không tự Paid. COD giao đủ có FulfillmentCompleted nhưng vẫn Unpaid/AwaitingProcessing; sau nhận đủ tiền thực tế mới ghi ManualCOD receipt. Đơn mixed chỉ Completed nếu đủ phần vật lý và snapshot Unique/Shared; ManualExtension vẫn chờ workflow xác minh riêng.

Sau dispatch, đơn không thể hủy/nhả tồn qua API cancel cũ, kể cả COD chưa thu. Không sửa ledger/reservation, bật lại tồn hoặc xóa shipment để giả lập hoàn hàng. Lát này chưa có hủy chuyến/returns/refund; ghi nhận sự cố ngoài luồng và xử lý nghiệp vụ ở lát tiếp nối. Giao vật lý không gọi API carrier/SMTP trong transaction và không thay đổi snapshot số đã cấp.

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
4. Kiểm product/SKU/category, giá, kho/history, Orders/receipts/digital deliveries/shipments, movement âm và reservation consumption/stock owners, ảnh và khả năng đọc cả nội dung kho số lẫn snapshot giao được Data Protection bảo vệ. Kiểm outbox pending/processing hết lease/dead-letter; chỉ mở worker khi storage tham chiếu đúng installation phục hồi.
5. Xử lý khoảng trống từ recovery point đến thời điểm sự cố. Tiền đã vào ngân hàng/COD đã thu, hàng vật lý đã xuất và mã khách đã nhận không bị đảo ngược khi restore database cũ: đối chiếu giao dịch/cấp phát/shipment sau snapshot trước khi mở lại bán/giao, tránh xuất hai lần, bán lại mã hoặc ghi nhận lại khoản tiền. Restore DB cũ còn có thể khôi phục session/quyền đã thu hồi sau backup: trước mở traffic phải quyết định và thực hiện vô hiệu hóa session/token phù hợp, đối soát thay đổi quyền và dữ liệu mất sau snapshot.
6. Mở đọc/ghi/worker có kiểm soát, theo dõi health, backlog/age/dead-letter và lỗi storage. Ghi RPO/RTO đo thực tế, các bước thủ công và kết quả; local test không xác nhận SLA production.

## 3. Adapter ngoài

SMTP, cloud storage, Redis, OAuth và jobs được nghiệm thu riêng theo cấu hình bật thật. Không ghi credential vào docs/log; giữ kết quả, endpoint đã che và phạm vi kiểm tra. Không bật provider thanh toán/vận chuyển trước khi có hồ sơ tính năng và thiết kế tương ứng.
