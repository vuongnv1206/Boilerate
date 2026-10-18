# Vận hành và API commerce v1

Thiết kế quan hệ, khóa và kiểu cột: [commerce-database.md](commerce-database.md). Catalog mặc định tắt. Bật `Modules__Catalog__Enabled=true` rồi restart; schema luôn tồn tại dù module tắt.

## Khởi tạo

1. Cấu hình provider/connection/JWT secret của installation mới theo README.
2. Apply toàn bộ migrations InitialFoundation, CommerceCatalog, DigitalInventory, OrderingCheckout và PaymentDigitalDelivery bằng EF CLI của provider đã chọn; Host không tự migrate. Installation đã ở baseline repo chỉ apply migration còn thiếu.
3. Bootstrap admin, hoặc chạy `--register-capabilities` rồi `--grant-admin-capabilities` có chủ đích với Catalog bật. Restart không tự cấp quyền mới.
4. Login, gọi POST `/api/inventory/initialize` một lần; gọi lại trả cùng kho MAIN. Quyền `Inventory.Initialize`.
5. Tạo ProductType/schema, Category, upload media; sau đó tạo Product/SKU và chọn cách giao. Physical có balance ban đầu bằng 0, nhập kho bằng adjustment riêng; hàng số dùng API kho số bên dưới, không cần MAIN để tạo SKU số.

Giá chỉ nhận VND nguyên đồng. Mọi mã SKU được giữ duy nhất toàn shop, kể cả SKU inactive và Product archived. ProductTypeId không đổi. Product names được phép trùng; slug duy nhất trong từng namespace Product/Category.

## Hợp đồng API

| Endpoint | Dữ liệu/chức năng | Quyền |
|---|---|---|
| GET/POST `/api/product-types`, GET/PUT `/{id}` | List, create, schema detail, edit schema | ProductType.View/Create/Update |
| GET/POST `/api/categories`, PUT/DELETE `/{id}` | Cây danh mục; DELETE có query version | Category.View/Create/Update/Delete |
| POST `/api/products/search`, GET `/api/products/{id}` | Đọc quản trị gồm mọi trạng thái | Product.View |
| POST `/api/products` | Tạo Draft + SKU, thêm MAIN=0 nếu Physical | Product.Create; thêm Price.Update khi gửi giá |
| PUT `/api/products/{id}` | Thay nội dung/categoryIds/fields/gallery | Product.Update |
| POST `/api/products/{id}/status` | Draft=1, Published=2, Archived=3 | Product.Publish |
| POST `/api/products/variants/preview` | Preview tổ hợp option, chưa ghi DB | Product.View |
| POST `/api/products/{id}/variants` | Thêm tổ hợp SKU được chọn | Product.Update; Price.Update nếu gửi giá |
| PUT `/api/products/{id}/variants/{variantId}` | Giá, trạng thái active, ảnh SKU | Product.Update; Price.Update nếu đổi giá |
| PUT `/api/products/{id}/default-variant` | Chọn SKU default đang active | Product.Update |
| PUT `/api/products/{id}/options/{optionId}` | Sửa nhãn/thứ tự, thêm value | Product.Update |
| GET `/api/inventory?page=1&size=20` | Balance, version theo SKU/kho | Inventory.View |
| POST `/api/inventory/{id}/adjustments` | Điều chỉnh có operationId | Inventory.Adjust |
| GET `/api/inventory/{id}/movements` | Lịch sử bất biến, phân trang | Inventory.View |
| GET `/api/digital-inventory/variants/{variantId}?page=1&size=20` | Metadata kho số; không có nội dung | DigitalInventory.View |
| POST `/api/digital-inventory/variants/{variantId}/items` | Nhập lô Unique nguyên tử, 1–100 dòng | DigitalInventory.Import |
| PUT `/api/digital-inventory/variants/{variantId}/shared-content` | Upsert một nội dung Shared/SKU | DigitalInventory.Update |
| PUT `/api/digital-inventory/variants/{variantId}/items/{id}/availability` | Bật/tắt Available/Disabled | DigitalInventory.Update |
| POST `/api/digital-inventory/items/{id}/reveal` | Xem nội dung; audit Read theo actor/item, no-store | DigitalInventory.Reveal |
| POST `/api/media` | Multipart `file`, JPEG/PNG <=5MiB | Product.Update |
| GET `/api/media/{id}` | Xem media quản trị | Product.View |
| GET `/api/outbox?state=4&page=1&size=20` | Vận hành pending/processing/completed/dead-letter | Outbox.View |
| POST `/api/outbox/{id}/replay` | Replay dead-letter, có audit | Outbox.Replay |
| POST `/api/catalog/products/search` | Tìm Published Products | Công khai |
| GET `/api/catalog/products/{slug}` | Chi tiết Published Product | Công khai |
| GET `/api/catalog/categories` | Category active | Công khai |
| GET `/api/catalog/media/{id}` | Ảnh có liên kết Product Published | Công khai |

Các request có ID trong body phải khớp route. Đọc lại version sau mỗi write. Stale version hoặc constraint conflict trả 409; client tải lại và quyết định thay đổi, không retry ghi đè mù quáng. Adjustment, checkout và xác nhận tiền có OperationId để replay kết quả thành công theo hợp đồng riêng; request khác cần đọc trạng thái nếu mất response sau commit.

### Tạo schema và Product

POST `/api/product-types`:

```json
{"code":"CLOTHES","name":"Quần áo","fields":[{"code":"MATERIAL","dataType":1,"label":"Chất liệu","helpText":null,"isRequired":true,"isFilterable":true,"choices":[]}]}
```

Dùng id/version/field id từ response, sau đó POST `/api/products`:

```json
{
  "productTypeId":"<type-id>",
  "productTypeVersion":"<type-version>",
  "content":{
    "name":"Áo thun cổ tròn","slug":"ao-thun-co-tron","description":"Cotton",
    "categoryIds":["<ao-nam-id>","<hang-moi-id>"],
    "fields":[{"fieldDefinitionId":"<material-field-id>","fieldType":1,"textValue":"Cotton"}],
    "media":[]
  },
  "options":[
    {"code":"COLOR","name":"Màu","values":[{"code":"BLACK","label":"Đen"}]},
    {"code":"SIZE","name":"Size","values":[{"code":"M","label":"M"},{"code":"L","label":"L"}]}
  ],
  "variants":[
    {"sku":"SHIRT-B-M","price":150000,"compareAtPrice":180000,"options":{"COLOR":"BLACK","SIZE":"M"}},
    {"sku":"SHIRT-B-L","price":160000,"compareAtPrice":null,"options":{"COLOR":"BLACK","SIZE":"L"}}
  ]
}
```

Sản phẩm đơn giản dùng `options:[]` và một variant với `options:{}`. SKU đầu tiên là default. Nháp được thiếu giá hoặc trường required, nhưng phải có SKU default hợp lệ. Publish kiểm tra toàn bộ Product; sửa Product Published cũng phải giữ toàn bộ trạng thái hợp lệ.

PUT Product nhận `{id,version,productTypeVersion,content}`. `categoryIds`, `fields`, `media` là tập mong muốn đầy đủ; mảng rỗng tháo toàn bộ tập tương ứng. Không có giá/SKU trong endpoint này. Tháo ảnh đang được SKU tham chiếu bị từ chối; gỡ ảnh SKU trước qua endpoint variant.

POST status nhận `{id,version,status}`. Không có hard delete/trash/restore cho Product/SKU.

### Cách giao và kho hàng số

CreateProduct thêm `fulfillmentKind`: 1 Physical (mặc định), 2 DigitalUnique, 3 DigitalShared, 4 ManualExtension. Cách giao ở Product, độc lập ProductType; SKU kế thừa. Product detail/summary trả lại kind. Kind không sửa qua EditProduct hoặc payload SKU trong lát này. ManualExtension có thể đặt và xác nhận nhận tiền; giao việc vẫn InProgress, chưa có thao tác gia hạn/PendingVerification và chưa tự cộng thời hạn.

Unique import:

```json
{"productId":"<product-id>","variantId":"<sku-id>","version":"<product-version>","items":[{"reference":"BATCH-01-ITEM-01","content":"<private-code-or-account-content>"}]}
```

Reference là nhãn quản lý ASCII không chứa credential, chuẩn hóa uppercase, unique trong SKU. Nội dung giữ nguyên khoảng trắng/ký tự, tối đa 8000 byte UTF-8; không sửa nội dung Unique sau nhập. Duplicate trong lô/đã có hoặc version cũ trả 409 và rollback toàn bộ. Mất response thì tải metadata; không gửi lô mới bằng nhãn khác để thử lại mù quáng.

Shared PUT nhận `{productId,variantId,version,content,isActive}`; tạo/cập nhật cùng một dòng `SHARED`. Availability PUT nhận `{productId,variantId,stockItemId,version,isAvailable}`. Cả hai dùng **version của Product**. Mọi response metadata gồm id/owner/kind/reference/state/version và `productVersion` mới; dùng productVersion cho write kế tiếp. State 1 Available, 2 Disabled, 3 Reserved, 4 Delivered. Không có API xóa kho hoặc tùy ý đánh dấu đã bán; giao Unique diễn ra sau xác nhận tiền. Reserved/Delivered không được bật/tắt; Delivered giữ owner vĩnh viễn, không nhả hoặc bán lại. Sửa Shared chỉ đổi nội dung cho lần giao sau, không sửa snapshot khách đã nhận.

List/response import/update không trả plaintext hay ciphertext. Reveal trả `{id,content}` sau kiểm quyền riêng và lưu audit; các response kho số no-store. Quyền kiểm cả ở service nên Product.Update hoặc DigitalInventory.View không thay thế được Import/Update/Reveal. Không nhét secret vào custom fields, description hoặc reference công khai; nhập qua kho số.

Public InStock dùng Available=OnHand-Reserved với Physical, dòng Unique Available, nội dung Shared Available, hoặc nhận yêu cầu ManualExtension; luôn xét SKU active. Nội dung Shared/Unique không có trong public Product. Publish vẫn cho sản phẩm hết hàng; quản lý kho không tự publish Draft. Reserved/Delivered có owner dòng đơn; các thay đổi giữ/giao theo đơn không đổi Product.Version, nhưng thay DigitalStockItem.Version.

### Giỏ, đơn hàng và giữ hàng

Lát này dùng đăng nhập Identity hiện có; không nhận CustomerId trong request. Giữ chính sách self-registration của installation: nếu đang tắt thì operator tạo tài khoản khách, hoặc cấu hình luồng đăng ký hiện có trước vận hành. Cart chỉ lưu lựa chọn/số lượng, chưa cam kết hàng. Tài khoản khách không cần Order.View để đọc đơn của chính mình; quyền này dành cho admin đọc mọi đơn.

| Endpoint | Nội dung/quyền |
|---|---|
| GET/PUT `/api/cart` | Giỏ của khách đăng nhập; PUT thay toàn bộ items với version |
| POST `/api/orders/checkout` | Tạo đơn, giữ hàng nguyên tử và clear giỏ; OperationId chống trùng |
| GET `/api/orders/mine?page=1&size=20` | Danh sách đơn của chính khách |
| GET `/api/orders/{id}` | Chi tiết đơn của khách; sai owner trả404 |
| POST `/api/orders/{id}/cancel` | `{version}`; hủy/nhả hàng đơn chờ xử lý của khách |
| GET `/api/admin/orders`, GET `/{id}` | Mọi đơn; Order.View ở controller và service |
| POST `/api/admin/orders/{id}/cancel` | `{version}`; Order.Cancel ở controller và service |
| POST `/api/admin/orders/{id}/payment-confirmations` | Xác nhận đủ tiền chuyển khoản thực nhận; Payment.Confirm ở controller và service |
| POST `/api/admin/orders/{id}/retry-fulfillment` | `{version}`; Order.Fulfill; thử lại đơn Paid + NeedsReview sau bổ sung kho/khắc phục key |
| GET `/api/orders/{id}/digital-deliveries` | Nội dung đã giao của chính khách đã trả tiền; audit đọc, no-store; sai owner trả404 |

GET cart chưa tồn tại trả Id Guid.Empty, Version null, Items rỗng. PUT đầu tiên dùng version null; các lần sau dùng Version trả về. Ví dụ:

```json
{"version":null,"items":[{"variantId":"<sku-id>","quantity":2}]}
```

Response cart gồm Items với tên/SKU/giá hiện tại/IsAvailable theo số lượng, Subtotal, ShippingAmount, Total và CanCheckout từ server. SKU ẩn/ngừng bán vẫn có thể xuất hiện trong giỏ cũ với IsAvailable=false; checkout sẽ từ chối. Tổng giỏ quá giới hạn tiền Order vẫn đọc/sửa được, giữ Version và CanCheckout=false để khách giảm số lượng; không lưu giỏ xong rồi mất khả năng đọc. Giỏ rỗng cũng CanCheckout=false. Max50 SKU/giỏ và quantity1..999.

Checkout:

```json
{"cartVersion":"<cart-version>","operationId":"<new-guid>","paymentMethod":1,"recipientName":"<name>","email":"<email>","phone":"<phone>","shippingAddress":"<required-for-physical>","expectedTotal":300000}
```

PaymentMethod Prepaid=1 hoặc CashOnDelivery=2; COD chỉ thuần vật lý. Server tính giá/ship, đối chiếu ExpectedTotal, rồi lưu snapshot tên/SKU/options/cách giao/giá/số lượng. Tổng đổi trả409 trước khi giữ hàng, client tải lại giỏ và xin xác nhận. Cùng OperationId và payload chuẩn hóa trả lại đơn cũ mà không clear giỏ/giữ hàng lần hai; payload khác trả409. Nếu client đổi cartVersion/tổng sau conflict thì dùng OperationId mới; không retry ghi đè.

Đơn trả trước giữ mặc định30phút theo `Ordering__ReservationMinutes`; physical Reserved không giảm OnHand, Unique chuyển Reserved theo dòng đơn. Shared/Extension không giữ độc quyền. OrderStatus PendingPayment=1/AwaitingProcessing=2/Cancelled=3/Expired=4/Completed=5. PaymentStatus Unpaid=1/Paid=2 và FulfillmentStatus Pending=1/NeedsReview=2/InProgress=3/Completed=4 độc lập với nhau. OrderDto chi tiết và OrderSummary danh sách đều trả hai trạng thái cùng PaidAtUtc nullable. Hủy/hết hạn chỉ áp dụng đơn chưa trả, nhả holds một lần; version cũ trả409. Paid không bị expiry hoặc được hủy qua API này. Worker và lượt checkout thu gom expiry bounded batch; chỉ nghiệp vụ xác nhận tiền mới có thể tiếp tục đơn hết hạn khi đủ hàng. Chưa có PaymentAttempt, refund hoặc gateway/webhook; email thông báo đã có qua outbox.

Response cart/order no-store và không có secret/ciphertext/stock item ID. Phí ship phẳng cấu hình `Ordering__FlatShippingAmount` (mặc định0, whole VND), chỉ đơn có vật lý; thiết lập phí phù hợp trước khi chạy shop. Worker `Ordering__WorkerIntervalSeconds` mặc định15, batch50 (max100); tắt cùng `Commerce__WorkerEnabled=false` cho kiểm thử/operator, không tắt khi cần xử lý đơn tự động trừ khi có runner khác.

### Xác nhận nhận tiền và giao số

Nguồn nhận tiền hiện tại là **nhân viên kiểm tra chuyển khoản thực tế rồi xác nhận**. Chưa chọn/nghiệm thu payment provider; không có callback công khai hoặc API cho khách tự khai đã thanh toán. Cấp Payment.Confirm và Order.Fulfill riêng bằng operator CLI; Order.View/Cancel không thay thế hai quyền này. [Hồ sơ payment/delivery](features/payment-digital-delivery.md) ghi cardinality, transaction và các phần còn lại.

POST `/api/admin/orders/{id}/payment-confirmations`:

```json
{"version":"<order-version>","operationId":"<new-guid>","reference":"BANK-TRANSFER-001","amount":300000,"currency":"VND","receivedAtUtc":"2026-10-18T10:00:00Z"}
```

Amount phải đúng toàn bộ Total snapshot của đơn, Currency chỉ VND; không hỗ trợ ghi nhận thiếu/thừa tiền trong lát này. ReceivedAtUtc bắt buộc UTC và không được ở tương lai. Reference là mã giao dịch ASCII dài tối đa100, cho chữ/số/dấu `.`/`-`/`_`, được trim/chuẩn hóa uppercase để chống dùng lại. Không nhập thông tin đăng nhập vào reference. Chỉ ghi tối đa một receipt đầy đủ cho mỗi đơn. Sai input/số tiền trả400, stale version/đã trả/reference trùng trả409, thiếu auth/quyền trả401/403.

OperationId của receipt unique toàn shop, độc lập với OperationId checkout. Gửi lại cùng order/reference/amount/currency/received time trả trạng thái đơn hiện tại, kể cả Version đã thay đổi; không ghi receipt hoặc cấp hàng lần hai. Dùng cùng operation với nội dung khác trả409. Khi mất response, retry đúng payload/OperationId; không tự tạo mã khác để xác nhận lại khoản tiền.

Khi xác nhận tiền, server kiểm đủ toàn bộ hàng theo snapshot đơn dưới khóa. Unique giữ đúng owner được giao tiếp, hàng đã nhả do hết hạn phải được giữ lại đủ; Shared cần nội dung Available. Nếu đủ, Unique qty2 nhận hai snapshot khác nhau; Shared qty2 nhận một snapshot. Shared đổi link sau đó không đổi nội dung khách đã nhận. Đơn chỉ có Unique/Shared hoàn tất tự động; đơn có Physical/ManualExtension vẫn InProgress, Paid không đồng nghĩa đã gửi hàng hoặc đã gia hạn.

Thanh toán thực sự muộn hoặc thông báo/xác nhận muộn đều đối chiếu tồn hiện tại khi holds đã được nhả. Thiếu một dòng vẫn giữ Paid + NeedsReview, không tạo phần giữ hàng/cấp phát mới; holds cũ giữ nguyên. Giải mã/mã hóa snapshot lỗi cũng giữ Paid + NeedsReview, audit metadata `DigitalContentUnavailable`. Sau khi nhập thêm hàng hoặc khôi phục đúng key, nhân viên dùng retry-fulfillment với Version mới; không xác nhận tiền lần nữa. Đơn Cancelled nhận tiền vẫn Cancelled + Paid + NeedsReview, không tự hồi sinh hoặc cho retry giao; hoàn tiền/xử lý hủy sau nhận tiền là mốc sau.

GET `/api/orders/{id}/digital-deliveries` trả mảng `{id,orderItemId,sequence,content}` của chính khách, chỉ khi Paid. Đơn chưa trả trả409, đơn người khác trả404; đơn Paid còn chờ xử lý có thể trả mảng rỗng. Các snapshot được giải mã riêng, đọc có audit và response no-store; không có ciphertext/stock ID trong response.

Giao Unique/Shared tạo một email outbox trong cùng transaction. Email chỉ thông báo vào Đơn hàng của tôi, không gửi mã, mật khẩu, link bí mật hoặc attachment. SMTP lỗi không làm mất hàng đã giao hoặc đảo Paid; worker retry/dead-letter/replay. Retry payment/fulfillment đã thành công không tạo thêm notification. SMTP có thể gửi lặp nếu crash sau send/trước ack; Message-Id ổn định chỉ hỗ trợ nhận diện. Bật worker, cấu hình SMTP và kiểm thử adapter thật trước vận hành.

### Tồn kho

```json
{"inventoryItemId":"<balance-id>","version":"<balance-version>","operationId":"<new-guid>","delta":10,"reason":"Nhập lô đầu"}
```

Gửi lại đúng operationId/target/delta/reason trả movement cũ kể cả version đã cũ; khác nội dung trả 409. Giảm kho dùng delta âm; DB balance không dưới Reserved. Inventory DTO trả OnHand/Reserved/Available. Không chỉnh quantity qua Product form, không sửa/xóa movement. Endpoint adjustment chỉ dùng cho Physical; hàng số đọc từ DigitalInventory.

### Trường tùy chỉnh và search

FieldType 1=text, 2=number, 3=boolean, 4=date, 5=single-select. Mỗi value chỉ gửi một cột tương ứng: textValue/numberValue/booleanValue/dateValue/choiceId. Date ở dạng `YYYY-MM-DD`.

```json
{"pageNumber":1,"pageSize":20,"categoryId":"<category-id>","minimumPrice":100000,"maximumPrice":200000,"inStock":true,"fields":[{"fieldId":"<material-field-id>","operator":"eq","textValue":"Cotton"}],"optionValueIds":["<black-value-id>"]}
```

Field operators: text/boolean eq; number/date eq hoặc range với numberTo/dateTo; choice in với choiceIds. Các field kết hợp AND, mỗi field choice dùng OR trong tập lựa chọn. Giá/options/stock phải khớp cùng SKU; paging/count cùng predicate. ProductType/schema quản trị cung cấp thông tin dựng form; frontend storefront có thể lấy cấu hình bộ lọc đã chọn từ phần quản trị. Product detail trả label của field và choice để hiển thị, không yêu cầu client suy nhãn từ ID.

Giá lọc tuân theo VND nguyên đồng, từ 0 đến 9999999999999999. Giá trị lọc number phải nằm trong miền `decimal(18,4)` (trị tuyệt đối nhỏ hơn 100000000000000, tối đa 4 chữ số thập phân); API trả 400 nếu vượt miền hoặc cần làm tròn, để hai database có cùng kết quả. Lỗi quy tắc nghiệp vụ trả lý do trong `ProblemDetails.detail`; lỗi máy chủ/database không công khai chi tiết nội bộ ở Production.

## Deployment, backup và worker

Docker chạy non-root, mount `/data` bền vững; image dùng `Storage__Root=/data/files`, `DataProtection__KeysPath=/data/keys`, log stdout. Không bỏ volume khi thay container. Một replica trong v1; nhiều replica cần shared media volume hoặc một adapter storage đã nghiệm thu. Worker trong Host cần ứng dụng chạy liên tục.

Sau proxy, đặt `ReverseProxy__Enabled=true` và `ReverseProxy__KnownProxies__0=<trusted-ip>`. Header từ địa chỉ khác không được tin. CORS khác origin cấu hình `Cors__Origins__0=https://store.example`; không dùng wildcard credentials. Giữ TLS tại reverse proxy và bảo vệ quyền đọc volume chứa key.

`Commerce`: MaximumOptions=3, MaximumVariants=200, WorkerEnabled=true, WorkerIntervalSeconds=5, LeaseSeconds=120, MaximumAttempts=8. Không dùng Hangfire memory ở Production. Module Catalog tắt sẽ dừng worker và routes, giữ nguyên model/data.

Worker log định kỳ backlog, oldestAgeSeconds, deadLetters; cảnh báo nếu backlog/tuổi tăng kéo dài hoặc deadLetters >0. API outbox trả trạng thái để operator điều tra/replay sau khi sửa nguyên nhân. Replay giữ nguyên message identity và có audit. File missing được coi là đã xóa; permission denied/offline phải retry/dead-letter.

Backup database, media và Data Protection key ring cùng cửa sổ vận hành. Receipt/delivery/stock owner phải cùng recovery point; sau restore cũ cần đối chiếu tiền ngân hàng và mã đã giao ngoài cửa sổ backup trước khi mở cấp phát lại. Tạm dừng write/worker khi lấy snapshot nhất quán; kiểm thử restore trên installation cô lập, chạy migration/preflight/health, thử đọc media, nội dung kho số/snapshot giao và session/token. Không dùng cache làm nguồn giá, kho hoặc authorization. Chỉ bật adapter SMTP/blob/OAuth/Redis sau smoke với cấu hình thực của môi trường.

## Kiểm chứng

- `dotnet test Boilerate.sln --filter "Category!=ProviderIntegration"`: domain/boundaries.
- Set `BOILERATE_TEST_MSSQL`, `BOILERATE_TEST_POSTGRES` tới server test; `dotnet test Boilerate.sln`: cả hai provider, migration và commerce integration.
- Linux: `docker build -t boilerate-ci .`, đặt `BOILERATE_CONTAINER_IMAGE=boilerate-ci`, chạy tests filter `Runtime=Container`; test dùng database/volume/container ngẫu nhiên rồi cleanup.
- `pwsh scripts/verify-template.ps1`: pack, rename, restore/build/tests và CLI migrate/bootstrap/module matrix.

Kết quả được version trong [báo cáo kiểm chứng](verification/2026-10-17-commerce.md), tiến độ theo [feature plan](features/catalog-v1.md) và [lịch sử](history/README.md). Raw logs cục bộ ở `artifacts/commerce-review/report.md`; CI được cấu hình không đồng nghĩa CI đã chạy thành công.
