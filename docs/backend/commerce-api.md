# Vận hành và API commerce v1

Thiết kế quan hệ, khóa và kiểu cột: [commerce-database.md](commerce-database.md). Catalog mặc định tắt. Bật `Modules__Catalog__Enabled=true` rồi restart; schema luôn tồn tại dù module tắt.

## Khởi tạo

1. Cấu hình provider/connection/JWT secret của installation mới theo README.
2. Apply đủ tám migrations InitialFoundation, CommerceCatalog, DigitalInventory, OrderingCheckout, PaymentDigitalDelivery, PhysicalShipping, ManualRenewals và RefundsReturns bằng EF CLI của provider đã chọn; Host không tự migrate. Installation đã ở baseline repo chỉ apply migration còn thiếu. PhysicalShipping có ID `20261018151738` trên SQL Server và `20261018151746` trên PostgreSQL. ManualRenewals có ID `20261018160428`/`20261018160441` tương ứng; thêm schema Renewals, không tự tạo việc cho đơn Paid cũ; dùng provision rõ ràng khi cần. RefundsReturns có ID `20261019112431`/`20261019112442` tương ứng; thêm ledger hậu mãi, không backfill khoản hoàn hoặc hàng đã nhận bên ngoài.
3. Bootstrap admin, hoặc chạy `--register-capabilities` rồi `--grant-admin-capabilities` có chủ đích với Catalog bật. Restart không tự cấp quyền mới. Shipping có các cặp `Shipping.View`, `Shipping.Dispatch`, `Shipping.Deliver`; Renewal có `Renewal.View`, `Renewal.Process`, `Renewal.Verify`, `Renewal.Reveal`. Hậu mãi có `AfterSales.View`, `Refund.Confirm`, `Return.Receive`; return nhập lại kho còn cần `Inventory.Adjust`. `Payment.Confirm` dùng riêng để xác nhận tiền, kể cả COD. Đăng ký capability không tự cấp grant cho nhân viên; Process không suy ra Verify hoặc Reveal.
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

Các request có ID trong body phải khớp route. Đọc lại version sau mỗi write. Stale version hoặc constraint conflict trả 409; client tải lại và quyết định thay đổi, không retry ghi đè mù quáng. Adjustment, checkout, xác nhận tiền, shipment, refund/return và các thao tác renewal có OperationId để replay kết quả thành công theo hợp đồng riêng; request khác cần đọc trạng thái nếu mất response sau commit.

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

CreateProduct thêm `fulfillmentKind`: 1 Physical (mặc định), 2 DigitalUnique, 3 DigitalShared, 4 ManualExtension. Cách giao ở Product, độc lập ProductType; SKU kế thừa. Product detail/summary trả lại kind. Kind không sửa qua EditProduct hoặc payload SKU trong lát này. ManualExtension tạo request sau Paid, khách khai tài khoản đích rồi nhân viên claim/start/xác minh theo API bên dưới; hệ thống không tự cộng thời hạn hoặc thực hiện gia hạn tại dịch vụ ngoài. Trạng thái kiểm chứng renewal được theo dõi riêng trong [hồ sơ](features/manual-renewals.md).

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
| POST `/api/admin/orders/{id}/payment-confirmations` | Xác nhận đủ tiền thực nhận, Source mặc định ManualBankTransfer hoặc ManualCOD sau giao đủ; Payment.Confirm ở controller và service |
| POST `/api/admin/orders/{id}/retry-fulfillment` | `{version}`; Order.Fulfill; thử lại đơn Paid + NeedsReview sau bổ sung kho/khắc phục key |
| GET `/api/orders/{id}/digital-deliveries` | Nội dung đã giao của chính khách đã trả tiền; audit đọc, no-store; sai owner trả404 |
| GET `/api/orders/{id}/shipments` | Tiến độ giao vật lý của chính khách; sai owner trả404, no-store |
| GET `/api/admin/orders/{id}/shipments` | Các lần giao của đơn; Shipping.View ở controller và service |
| POST `/api/admin/orders/{id}/shipments` | Xuất giao số lượng đã giữ; Shipping.Dispatch ở controller và service |
| POST `/api/admin/orders/{id}/shipments/{shipmentId}/deliver` | Xác nhận shipment đã giao; Shipping.Deliver ở controller và service; không tự Paid |

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

Đơn trả trước giữ mặc định30phút theo `Ordering__ReservationMinutes`; physical Reserved không giảm OnHand, Unique chuyển Reserved theo dòng đơn. Shared/Extension không giữ độc quyền. OrderStatus PendingPayment=1/AwaitingProcessing=2/Cancelled=3/Expired=4/Completed=5/Closed=6. PaymentStatus Unpaid=1/Paid=2 và FulfillmentStatus Pending=1/NeedsReview=2/InProgress=3/Completed=4/Stopped=5 độc lập với nhau. Closed đi cùng Stopped; Paid vẫn giữ lịch sử sau hoàn tiền. OrderDto chi tiết và OrderSummary danh sách đều trả hai trạng thái cùng PaidAtUtc nullable. Hủy/hết hạn chỉ áp dụng đơn chưa trả và chưa xuất giao, nhả holds một lần; version cũ trả409. Paid không bị expiry hoặc được hủy qua API này. COD đã xuất dù một phần cũng không được hủy/nhả tồn qua API cũ. Worker và lượt checkout thu gom expiry bounded batch; chỉ nghiệp vụ xác nhận tiền mới có thể tiếp tục đơn hết hạn khi đủ hàng. Hoàn tiền/nhận hàng trả dùng API hậu mãi riêng bên dưới; chưa có PaymentAttempt/gateway/webhook. Email thông báo đã có qua outbox.

Response cart/order no-store và không có secret/ciphertext/stock item ID. Phí ship phẳng cấu hình `Ordering__FlatShippingAmount` (mặc định0, whole VND), chỉ đơn có vật lý; thiết lập phí phù hợp trước khi chạy shop. Worker `Ordering__WorkerIntervalSeconds` mặc định15, batch50 (max100); tắt cùng `Commerce__WorkerEnabled=false` cho kiểm thử/operator, không tắt khi cần xử lý đơn tự động trừ khi có runner khác.

### Xác nhận nhận tiền và giao số

Các timestamp của Order response là UTC, serialize có `Z`: PaidAtUtc/ExpiresAt nullable và CreatedOn của danh sách. Giá trị đọc lại từ SQL Server/PostgreSQL giữ cùng instant; client chuyển sang múi giờ hiển thị, không tự coi giá trị không có timezone là giờ local.

Nguồn nhận tiền hiện tại là **nhân viên kiểm tra tiền thực nhận rồi xác nhận**: chuyển khoản, hoặc COD sau khi giao đủ đơn thuần vật lý. Chưa chọn/nghiệm thu payment provider; không có callback công khai hoặc API cho khách tự khai đã thanh toán. Cấp Payment.Confirm và Order.Fulfill riêng bằng operator CLI; Order.View/Cancel không thay thế hai quyền này. [Hồ sơ payment/delivery](features/payment-digital-delivery.md) và [bổ sung shipping/COD](features/physical-shipping.md) ghi cardinality, transaction và các phần còn lại.

POST `/api/admin/orders/{id}/payment-confirmations`:

```json
{"version":"<order-version>","operationId":"<new-guid>","reference":"BANK-TRANSFER-001","amount":300000,"currency":"VND","receivedAtUtc":"2026-10-18T10:00:00Z","source":"ManualBankTransfer"}
```

Amount phải đúng toàn bộ Total snapshot của đơn, Currency chỉ VND; không hỗ trợ ghi nhận thiếu/thừa tiền trong lát này. ReceivedAtUtc bắt buộc UTC và không được ở tương lai. Reference là mã giao dịch ASCII dài tối đa100, cho chữ/số/dấu `.`/`-`/`_`, được trim/chuẩn hóa uppercase để chống dùng lại trong cùng Source. Không nhập thông tin đăng nhập vào reference. Source có thể bỏ qua để giữ mặc định `ManualBankTransfer`; chỉ nhận đúng `ManualBankTransfer` hoặc `ManualCOD`. ManualCOD yêu cầu đơn COD đã giao thành công toàn bộ số lượng vật lý; chưa giao đủ hoặc sai phương thức trả409. Delivered không tự tạo receipt. Chỉ ghi tối đa một receipt đầy đủ cho mỗi đơn. Sai input/số tiền trả400, stale version/đã trả/reference trùng trả409, thiếu auth/quyền trả401/403.

OperationId của receipt unique toàn shop, độc lập với OperationId checkout. Gửi lại cùng order/reference/amount/currency/received time/source trả trạng thái đơn hiện tại, kể cả Version đã thay đổi; không ghi receipt hoặc cấp hàng lần hai. Hash chuyển khoản cũ được giữ tương thích; ManualCOD thêm Source vào hash. Dùng cùng operation với nội dung khác, kể cả đổi nguồn nhận tiền, trả409. Khi mất response, retry đúng payload/OperationId; không tự tạo mã khác để xác nhận lại khoản tiền.

Khi xác nhận tiền trước khi có lần xuất giao nào, server kiểm đủ toàn bộ hàng theo snapshot đơn dưới khóa. Unique giữ đúng owner được giao tiếp, hàng đã nhả do hết hạn phải được giữ lại đủ; Shared cần nội dung Available. Nếu đủ, Unique qty2 nhận hai snapshot khác nhau; Shared qty2 nhận một snapshot. Shared đổi link sau đó không đổi nội dung khách đã nhận. Đơn chỉ có Unique/Shared hoàn tất tự động; đơn có Physical/ManualExtension vẫn InProgress, Paid không đồng nghĩa đã gửi hàng hoặc đã gia hạn. Mỗi ManualExtension line của đơn Paid không Cancelled/Closed có một request, kể cả đang NeedsReview; chỉ thu nhận target trong lúc NeedsReview, chưa cho Claim/Start. Với COD đã xuất một phần hoặc toàn bộ, xác nhận chuyển khoản đối chiếu các shipment đã có và trạng thái hoàn tất; không giữ lại số lượng ban đầu đã xuất, không tạo movement hoặc cấp lại hàng số.

Thanh toán thực sự muộn hoặc thông báo/xác nhận muộn đều đối chiếu tồn hiện tại khi holds đã được nhả. Thiếu một dòng vẫn giữ Paid + NeedsReview, không tạo phần giữ hàng/cấp phát mới; holds cũ giữ nguyên. Giải mã/mã hóa snapshot lỗi cũng giữ Paid + NeedsReview, audit metadata `DigitalContentUnavailable`. Sau khi nhập thêm hàng hoặc khôi phục đúng key, nhân viên dùng retry-fulfillment với Version mới; không xác nhận tiền lần nữa. Đơn Cancelled nhận tiền vẫn Cancelled + Paid + NeedsReview, không tự hồi sinh hoặc cho retry giao; có thể ghi hoàn tiền thực tế bằng API hậu mãi. COD Closed sau nhận hàng trả vẫn được ghi chuyển khoản thực nhận muộn, giữ Closed/Stopped; receipt không mở lại giao hàng hay request gia hạn.

GET `/api/orders/{id}/digital-deliveries` trả mảng `{id,orderItemId,sequence,content}` của chính khách, chỉ khi Paid. Đơn chưa trả trả409, đơn người khác trả404; đơn Paid còn chờ xử lý có thể trả mảng rỗng. Các snapshot được giải mã riêng, đọc có audit và response no-store; không có ciphertext/stock ID trong response.

Giao Unique/Shared tạo một email outbox trong cùng transaction. Email chỉ thông báo vào Đơn hàng của tôi, không gửi mã, mật khẩu, link bí mật hoặc attachment. SMTP lỗi không làm mất hàng đã giao hoặc đảo Paid; worker retry/dead-letter/replay. Retry payment/fulfillment đã thành công không tạo thêm notification. SMTP có thể gửi lặp nếu crash sau send/trước ack; Message-Id ổn định chỉ hỗ trợ nhận diện. Bật worker, cấu hình SMTP và kiểm thử adapter thật trước vận hành.

### Giao hàng vật lý và thu COD

[Hồ sơ shipping](features/physical-shipping.md) có thiết kế và trạng thái kiểm chứng. Service/API đã đạt regression326/326 local trên hai DB; chưa nghiệm thu carrier/deployment thật. Shipping có các cặp quyền riêng View/Dispatch/Deliver ở cả controller và service. Khách chỉ GET shipment của đơn mình, không cần Shipping.View. Mọi response shipping no-store; timestamp UTC có `Z`, DeliveredAtUtc nullable trước giao thành công. Không trả nội dung số, movement nội bộ hoặc dữ liệu kho bí mật.

POST `/api/admin/orders/{id}/shipments` xuất giao trực tiếp:

```json
{"orderVersion":"<order-version>","operationId":"<new-guid>","carrier":"Giao hàng tại shop","trackingNumber":"SHOP-001","items":[{"orderItemId":"<physical-order-item-id>","quantity":1}]}
```

Đây là xác nhận xuất thật, không phải nháp đóng gói. Carrier/TrackingNumber bắt buộc, trim và tối đa100ký tự. Items từ1–50 dòng khác nhau, mỗi quantity1..999, chỉ dòng Physical của đúng đơn. Đơn trả trước phải Paid; COD thuần vật lý có thể xuất trước khi thu tiền. Cancelled/Expired/Closed/NeedsReview bị từ chối. Số lượng vượt phần còn đã giữ hoặc đã mua trả409; lỗi cấu trúc/giới hạn trả400. Trong một transaction, server giảm OnHand và Reserved cùng số lượng, thêm movement âm và ShipmentItem, tăng ConsumedQuantity và Order.Version. Các dòng còn lại tiếp tục được giữ; không gửi API adjustment để xuất thêm lần nữa.

Response ShipmentDto có dạng:

```json
{"id":"<shipment-id>","orderId":"<order-id>","orderVersion":"<new-order-version>","version":"<shipment-version>","carrier":"Giao hàng tại shop","trackingNumber":"SHOP-001","status":1,"dispatchedAtUtc":"2026-10-18T11:00:00Z","deliveredAtUtc":null,"items":[{"orderItemId":"<physical-order-item-id>","quantity":1}]}
```

Dùng OrderVersion từ response để thực hiện lần ghi đơn tiếp theo. Một đơn có nhiều shipment, một dòng đơn có thể giao qua nhiều shipment. Shipment OperationId unique toàn shop; replay cùng order/carrier/tracking và tập item/quantity đã sắp theo ID trả lại shipment cũ, bỏ qua version cũ, không trừ kho lần hai. Cùng operation đổi nội dung trả409. Khi mất response phải retry đúng OperationId/payload.

POST `/api/admin/orders/{id}/shipments/{shipmentId}/deliver`:

```json
{"orderVersion":"<current-order-version>","version":"<current-shipment-version>","operationId":"<new-delivery-guid>"}
```

Status shipment chuyển Dispatched=1 → Delivered=2, thời điểm do server ghi và không nhỏ hơn thời điểm xuất. DispatchedAtUtc/DeliveredAtUtc được chuẩn hóa microsecond trước persist/response đầu để đọc lại hoặc replay trên hai DB không đổi độ chính xác. OperationId delivery có namespace riêng, unique giữa các lần delivery; replay đúng shipment/đơn trả kết quả hiện tại, không thay ngày giao cũ. Dùng operation cho shipment khác, operation mới trên shipment đã Delivered hoặc version cũ đều trả409. GET `/api/admin/orders/{id}/shipments` và GET `/api/orders/{id}/shipments` trả mảng ShipmentDto; sai owner trả404 ở API khách.

Chỉ khi tổng số lượng Delivered của từng dòng vật lý đã đủ mới xét hoàn tất. Đơn mixed còn cần đủ snapshot Unique/Shared và mọi request ManualExtension Completed; còn thiếu request hoặc chưa xác minh hoàn tất thì vẫn InProgress. COD giao đủ mà chưa nhận tiền có FulfillmentStatus.Completed, PaymentStatus.Unpaid, OrderStatus.AwaitingProcessing. Sau đối chiếu thực nhận đủ, gửi payment-confirmations với `source:"ManualCOD"`; lúc đó mới Paid và Order.Completed. Nếu khách chuyển khoản trước khi giao đủ COD, dùng ManualBankTransfer; các lần xuất/nhận sau tiếp tục trên lịch sử đã có. Không tự Paid từ tracking/delivered.

Đã xuất dù một phần thì API cancel đơn cũ trả409, kể cả COD chưa trả tiền. Không có sửa/xóa shipment hoặc hủy chuyến; hàng đã nhận lại ghi qua ledger hậu mãi bên dưới, không nhả reservation, bật lại tồn hay sửa ledger cũ để giả lập. Chưa có API carrier/webhook; nhân viên lưu kết quả vận chuyển thực tế. Đơn mixed chỉ hoàn tất khi đủ shipment Delivered, snapshot hàng số và mọi request ManualExtension Completed. Sau Closed vẫn ghi được sự thật Delivered bằng version hiện tại, nhưng không hồi sinh trạng thái đơn.

### Gia hạn thủ công có claim và xác minh

[Hồ sơ renewal](features/manual-renewals.md) ghi thiết kế/trạng thái kiểm chứng; phần này mô tả hợp đồng code, không xác nhận deployment hoặc thao tác dịch vụ ngoài đã nghiệm thu. Mọi response renewal no-store. API được bật cùng Catalog; khi module tắt, routes bị loại nhưng dữ liệu vẫn giữ. Owner chỉ truy cập đúng Order.CustomerId, đơn khác trả404. Admin kiểm quyền tại cả controller và service, không suy từ Order.View/Payment.Confirm.

| Endpoint | Request/kết quả | Quyền |
|---|---|---|
| GET `/api/orders/{orderId}/renewals` | Mảng RenewalDto của đơn; GET không tạo request | Authenticated owner |
| PUT `/api/orders/{orderId}/renewals/{id}/target` | `{version,operationId,accountIdentifier}` → RenewalDto | Owner của đơn Paid, AwaitingTarget/Ready và chưa từng Start |
| GET `/api/orders/{orderId}/renewals/{id}/target` | `{id,content}` chứa định danh của mình, Read audit | Owner của đơn Paid |
| GET `/api/admin/renewals?page=1&size=20&status=2` | Page RenewalAdminDto; page≥1, size1..100; status tùy chọn1..5 | Renewal.View |
| GET `/api/admin/renewals/{id}` | RenewalAdminDto chi tiết | Renewal.View |
| POST `/api/admin/orders/{orderId}/renewals/provision` | `{orderVersion}` → mảng RenewalDto; tạo những request thiếu | Renewal.Process; Paid, không Cancelled/Expired/Closed |
| POST `/api/admin/renewals/{id}/claim` | `{version,operationId}` → RenewalAdminDto | Renewal.Process |
| POST `/api/admin/renewals/{id}/release` | `{version,operationId,claimToken}` → RenewalAdminDto | Renewal.Process, đúng holder/token/lease |
| POST `/api/admin/renewals/{id}/start` | `{version,operationId,claimToken}` → PendingVerification + attempt metadata | Renewal.Process, đúng holder/token/lease |
| POST `/api/admin/renewals/{id}/verify` | `{version,operationId,attemptId,applied,newExpiresAtUtc,note,evidence}` → RenewalAdminDto | Renewal.Verify |
| GET `/api/admin/renewals/{id}/target` | `{id,content}` định danh đã giải mã; Read audit | Renewal.Reveal |
| GET `/api/admin/renewals/{id}/attempts/{attemptId}/evidence` | `{id,content}`; content là chuỗi JSON chứa Note/Evidence đã giải mã; Read audit | Renewal.Verify |
| GET `/api/admin/renewals/{id}/history` | OperationId/Action/ActorId/StatusAfter/OccurredAtUtc | Renewal.View |

RenewalDto gồm Id/OrderId/OrderItemId/Version/Status, snapshot Name/Sku/OptionDescription/Quantity, TargetPreview dạng mask, CompletedAtUtc/VerifiedExpiresAtUtc nullable. RenewalAdminDto bọc Renewal, ClaimedBy/ClaimUntilUtc/ClaimToken nullable và Attempts metadata (Id/OperationId/StartedBy/StartedAtUtc/Outcome/VerifiedBy/VerifiedAtUtc/NewExpiresAtUtc). List không trả token; detail và response thao tác chỉ trả ClaimToken cho chính holder trong claim còn hạn. Owner DTO không có token/attempt evidence. Không DTO thường nào trả full target, TargetKey, ciphertext, note hoặc evidence; đọc nội dung chỉ qua endpoint chuyên biệt và audit commit trước khi trả.

Target nhận1–256ký tự sau trim, giữ nguyên case thật và chặn control character; không nhập mật khẩu. Có thể sửa khi AwaitingTarget=1 hoặc Ready=2 và chưa từng Start. Claim chuyển Claimed=3, lease mặc định15phút, cấu hình `Ordering__RenewalClaimMinutes` từ1–120. Hai request cùng canonical target bị tuần tự hóa toàn shop; claim còn hạn hoặc PendingVerification của request khác trả409. Claim hết hạn chưa Start có thể reclaim và được ghi lịch sử. Release đúng claim còn hạn trở Ready; token cũ không dùng được sau reclaim.

Start phải thành công trước khi nhân viên thao tác ngoài: server ghi attempt Pending=1 và request PendingVerification=4 trong transaction rồi trả. ClaimToken/lease bị xóa; target khóa vĩnh viễn sau lần Start đầu. Mất kết nối, restart hoặc thời gian trôi qua không mở lại yêu cầu này. Nếu response Start bị mất, retry đúng OperationId hoặc đọc lại metadata/history trước khi quyết định hành động; không thực hiện bên ngoài lần nữa khi chưa đối chiếu kết quả lần trước.

Verify không yêu cầu cùng nhân viên Start nhưng phải có Renewal.Verify. Note bắt buộc sau trim, tối đa1200ký tự; Evidence tùy chọn tối đa1800ký tự. Applied=true cần NewExpiresAtUtc UTC (`Z`) thực tế lớn hơn thời điểm xác minh của server; thành Outcome Applied=2/Request Completed=5. Applied=false bắt buộc expiry null, thành Outcome NotApplied=3/Request Ready; cần claim/start mới để thử ngoài lần mới. Không sửa kết quả cũ hoặc đổi target sau NotApplied. Note/Evidence được bảo vệ theo AttemptId. Qty2 là số gói đã mua cho cùng target, không tự suy ra ngày hết hạn; nhân viên nhập kết quả thật của dịch vụ.

Các write dùng Renewal.Version, riêng provision dùng Order.Version. OperationId duy nhất toàn bộ renewal operation history; replay đúng actor/request/action/nội dung chuẩn hóa trả trạng thái hiện tại trước kiểm version, không tạo attempt/lịch sử lần hai. Version và ClaimToken không thuộc hash replay. Đổi payload/actor/request/action hoặc version cũ của thao tác mới trả409. Dữ liệu sai kiểu/giới hạn trả400. Process/Start chờ đơn Paid và hết NeedsReview; Verify lưu kết quả thực tế đã xảy ra rồi reconcile nếu đơn đủ điều kiện. Chỉ khi đủ mọi physical/digital/renewal line thì đơn mới Completed; tạo request hoặc thanh toán không tự hoàn tất gia hạn. Provision cho đơn Paid cũ là thao tác operator rõ ràng, GET hoặc replay receipt không tự backfill. Closed chặn provision/target/claim/start mới; Verify và đọc target/evidence/lịch sử đã có vẫn hoạt động. PendingVerification không được mở khóa do hoàn tiền, và kết quả xác minh sau hoàn tiền không mở lại đơn Closed.

### Hoàn tiền thực tế và nhận hàng trả

[Hồ sơ hậu mãi](features/refunds-returns.md) ghi thiết kế, tiến độ và kiểm chứng; các endpoint này ghi sự thật nhân viên đã kiểm tra, không chuyển tiền hoặc gọi carrier. Không tự quyết chính sách đủ điều kiện đổi trả. Routes bật cùng Catalog, mọi response no-store. Controller và service kiểm quyền riêng; owner khác trả404. Metadata không chứa tài khoản thanh toán/credential; Reference là mã chứng từ ASCII, không nhập số tài khoản/mật khẩu.

| Endpoint | Input/kết quả | Quyền |
|---|---|---|
| GET `/api/orders/{orderId}/after-sales` | AfterSalesDto: OrderId/OrderVersion/Closed, ReceivedAmount/RefundedAmount/RefundableAmount và mảng Refunds/Returns | Authenticated owner |
| GET `/api/admin/orders/{orderId}/after-sales` | Cùng summary/history | AfterSales.View |
| POST `/api/admin/orders/{orderId}/refunds` | `{orderVersion,operationId,reference,amount,currency,refundedAtUtc,reason,source}` → `{refund,orderVersion,closed}` | Refund.Confirm |
| POST `/api/admin/orders/{orderId}/returns` | `{orderVersion,operationId,reference,reason,items,closeRemainingCod}` → `{return,orderVersion,closed}` | Return.Receive; thêm Inventory.Adjust nếu có RestockQuantity>0 |

Ví dụ ghi khoản đã hoàn:

```json
{"orderVersion":"<order-version>","operationId":"<new-guid>","reference":"RF-001","amount":100000,"currency":"VND","refundedAtUtc":"2026-10-19T08:00:00Z","reason":"Đã đối chiếu khoản hoàn thực tế","source":"ManualBankTransfer"}
```

Source mặc định ManualBankTransfer, hoặc ManualCash, không dùng ManualCOD cho khoản hoàn. Amount phải >0, VND nguyên đồng và tổng các khoản hoàn không vượt receipt đã thu của đơn. Refund thời điểm UTC không trước receipt hoặc sau clock server, so sánh ở độ chính xác microsecond chung của hai DB; cùng microsecond với receipt được chấp nhận kể cả receipt SQL Server cũ giữ tick100ns. Reference1–100ký tự sau trim, chỉ ASCII chữ/số/dấu chấm/gạch dưới/gạch ngang; unique sau uppercase theo Source. Reason bắt buộc sau trim≤1000ký tự. RefundDto trả Id/ReceiptId/Reference/Source/Amount/Currency/Reason/RefundedAtUtc. API không giữ thông tin nhận tiền hoặc tự gọi ngân hàng.

Hoàn một phần không tự dừng giao. Khi tổng hoàn bằng toàn bộ receipt, kể cả phí ship, Order thành Closed=6/FulfillmentStatus.Stopped=5; PaymentStatus/PaidAtUtc vẫn giữ lịch sử đã thu. Chỉ nhả phần physical chưa xuất và Unique còn Reserved; phần đã xuất phải nhận lại qua return riêng. Unique Delivered/Shared snapshot không bị thu hồi hay đưa vào kho để bán lại. Claim renewal chưa Start được release về Ready có lịch sử; PendingVerification vẫn khóa tới khi Verify. Sau Closed không dispatch/retry fulfillment/provision/target/claim/start mới; sự thật delivery/verification phát sinh muộn vẫn được ghi mà không mở lại đơn.

Ví dụ nhận lại hai món đã xuất, một món còn bán được:

```json
{"orderVersion":"<order-version>","operationId":"<new-guid>","reference":"RETURN-001","reason":"Đã kiểm hàng nhận lại","closeRemainingCod":false,"items":[{"shipmentId":"<shipment-id>","orderItemId":"<physical-line-id>","quantity":2,"restockQuantity":1}]}
```

Items1–50 cặp ShipmentId/OrderItemId khác nhau, Quantity1..999 và RestockQuantity0..Quantity; cumulative Quantity không vượt số đã xuất của đúng shipment/order/SKU. Chấp nhận shipment Dispatched hoặc Delivered, kể cả sau full refund; không sửa trạng thái/lượng giao lịch sử. RestockQuantity0 vẫn ghi đã nhận hàng, không tạo movement. Restock>0 dùng balance của movement xuất gốc, tăng OnHand bằng movement dương, không thay Reserved/ConsumedQuantity; client không chọn balance/VariantId/movement. Không tự refund theo số lượng nhận lại. PhysicalReturnDto gồm Id/Reference/Reason/ReceivedAtUtc/CloseRemainingCod/Items(ShipmentId,OrderItemId,Quantity,RestockQuantity), timestamp nhận từ clock server.

CloseRemainingCod=true chỉ cho COD chưa thu, có shipment và mọi quantity đã xuất đều đã nhận lại gồm lần này; dùng ở lần nhận cuối. Khi hợp lệ, dừng giao/nhả phần chưa xuất cùng transaction, không tạo tiền giả. Nếu false, return không tự đóng đơn. Bank receipt thực nhận sau COD Closed vẫn ghi Paid và giữ Closed/Stopped để xử lý hoàn nếu cần; ManualCOD vẫn cần toàn bộ quantity Delivered.

Write kiểm OrderVersion. OperationId unique riêng bảng refund/return, hash gồm actor/đơn/nội dung chuẩn hóa và bỏ version; replay đúng trả record cũ cùng OrderVersion/Closed hiện tại trước kiểm version, không hoàn/nhập kho lần hai. Đổi actor/payload/đơn trả409. Reference unique chặn ghi lại cùng chứng từ bằng operation mới. Dữ liệu sai giới hạn/kiểu/tiền trả400; stale version, quá cap hoặc sai ownership nghiệp vụ trả409; thiếu quyền403. GET lấy gate trong transaction ngắn để version và totals nhất quán, không tạo ledger hoặc provision request. Không sửa/xóa ledger, không có email/outbox hậu mãi mới trong lát này.

### Tồn kho

```json
{"inventoryItemId":"<balance-id>","version":"<balance-version>","operationId":"<new-guid>","delta":10,"reason":"Nhập lô đầu"}
```

Gửi lại đúng operationId/target/delta/reason trả movement cũ kể cả version đã cũ; khác nội dung trả 409. Giảm kho dùng delta âm; DB balance không dưới Reserved. Inventory DTO trả OnHand/Reserved/Available. Dispatch dùng nghiệp vụ consume reservation riêng, giảm đồng thời OnHand và Reserved, không dùng adjustment để xuất phần đã giữ. Không chỉnh quantity qua Product form, không sửa/xóa movement. Endpoint adjustment chỉ dùng cho Physical; hàng số đọc từ DigitalInventory.

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

Backup database, media và Data Protection key ring cùng cửa sổ vận hành. Receipt/refund/return/return items/digital delivery/shipment/stock movement/consumption/stock owner/renewal requests-attempts-operations phải cùng recovery point; sau restore cũ cần đối chiếu tiền ngân hàng/COD đã thu/đã hoàn, hàng đã xuất/nhận lại, mã đã giao và gia hạn đã thao tác ngoài cửa sổ backup trước khi mở cấp phát lại. Renewal dùng purpose `Boilerate.Renewals.Target.v1` + RequestId và `Boilerate.Renewals.Evidence.v1` + AttemptId; giữ cùng application name/key ring. RefundsReturns Down chặn ledger hậu mãi/Closed và commerce history/holds trước DDL; ManualRenewals/PhysicalShipping có guard riêng. Không dùng downgrade để dọn giao dịch hoặc đảo tác động bên ngoài. Tạm dừng write/worker khi lấy snapshot nhất quán; kiểm thử restore trên installation cô lập, chạy migration/preflight/health, thử đọc media, nội dung kho số/snapshot giao/target/evidence và session/token. Không dùng cache làm nguồn giá, kho hoặc authorization. Chỉ bật adapter SMTP/blob/OAuth/Redis sau smoke với cấu hình thực của môi trường.

## Kiểm chứng

- `dotnet test Boilerate.sln --filter "Category!=ProviderIntegration"`: domain/boundaries.
- Set `BOILERATE_TEST_MSSQL`, `BOILERATE_TEST_POSTGRES` tới server test; `dotnet test Boilerate.sln`: cả hai provider, migration và commerce integration.
- Linux: `docker build -t boilerate-ci .`, đặt `BOILERATE_CONTAINER_IMAGE=boilerate-ci`, chạy tests filter `Runtime=Container`; test dùng database/volume/container ngẫu nhiên rồi cleanup.
- `pwsh scripts/verify-template.ps1`: pack, rename, restore/build/tests và CLI migrate/bootstrap/module matrix.

Kết quả được version trong [báo cáo kiểm chứng](verification/2026-10-17-commerce.md), tiến độ theo [feature plan](features/catalog-v1.md) và [lịch sử](history/README.md). Raw logs cục bộ ở `artifacts/commerce-review/report.md`; CI được cấu hình không đồng nghĩa CI đã chạy thành công.
