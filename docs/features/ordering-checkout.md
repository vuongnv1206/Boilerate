# Giỏ hàng, đơn hàng và giữ hàng — Shop MVP bước 2

Ngày bắt đầu/cập nhật: 2026-10-18, Asia/Saigon. Trạng thái: đang thiết kế/triển khai theo [MVP](shop-mvp.md). Source đầu đợt `31a0109`.

## 1. Mục tiêu và phạm vi

Khách đăng nhập dùng một giỏ, tạo đơn chung vật lý/số, xem và hủy đơn của mình. Server giữ đủ hàng nguyên tử khi tạo đơn; hết hạn đơn trả trước thì nhả hàng. Không tạo PaymentAttempt/Refund/Shipment hoặc giao bí mật cho khách trong lát này. Thanh toán đến muộn và giao sau nhận tiền thuộc bước Payment tiếp theo.

Lựa chọn triển khai đơn giản: dùng tài khoản Identity hiện có, chưa có giỏ khách vãng lai; phí ship cố định cấu hình ở server (mặc định 0, chỉ đơn có vật lý); địa chỉ bắt buộc khi có vật lý. COD chỉ thuần vật lý, đơn COD giữ hàng tới khi hủy hoặc bước xử lý đơn tiếp theo; không áp TTL chờ thanh toán. Đây là quyết định kỹ thuật/phạm vi cho lát đầu, không tích hợp carrier/payment mới.

Tiêu chí: giỏ không giữ kho; giá/tên/options snapshot lúc checkout; version và tổng tiền kỳ vọng chống thay giá âm thầm; retry checkout cùng OperationId trả đơn cũ, payload khác conflict; hai người không giữ món cuối; thiếu một món rollback cả đơn/giữ/cart; hết hạn/hủy nhả đúng một lần; khách không xem/hủy đơn người khác; quyền admin riêng; không lộ kho số.

## 2. Thiết kế domain/database

| Quan hệ | Cardinality và ownership | Bảng/khóa/ràng buộc |
|---|---|---|
| Customer → Cart | 1–0..1 | Ordering.Carts.CustomerId UQ; Guid attribution, không cascade/FK tới Identity string ID |
| Cart → CartItem | 1–0..50 | CartItems PK (CartId,VariantId); FK Cart Restrict và SKU Restrict |
| Cart → Order | 1–N | Orders.CartId FK Restrict; giỏ giữ lại khi checkout |
| Customer → Order | 1–N | CustomerId attribution, index (CustomerId,CreatedOn,Id) |
| Order → OrderItem | 1–1..50 | OrderItems.Id PK; OrderId FK Restrict; UQ (OrderId,VariantId); AK (Id,VariantId) |
| OrderItem → PhysicalStockReservation | 1–0..1 | OrderItemId UQ, FK (OrderItemId,VariantId) → OrderItems(Id,VariantId) |
| InventoryItem → PhysicalStockReservation | 1–N | FK (InventoryItemId,VariantId) → InventoryItems(Id,VariantId); item AK bổ sung |
| OrderItem → Unique DigitalStockItem đang giữ | 1–0..N | Nullable FK (ReservedForOrderItemId,VariantId) → OrderItems(Id,VariantId); chỉ Unique state Reserved mới có owner |

| Bảng/cột chính | Kiểu/constraint/index/concurrency |
|---|---|
| Ordering.Carts | Id Guid PK, CustomerId Guid UQ; CommerceEntity audit/Version(32) |
| Ordering.CartItems | CartId/VariantId Guid composite PK, Quantity int CHECK 1..999 |
| Ordering.Orders | Id Guid PK; CartId/CustomerId/OperationId Guid; RequestHash ASCII(64); Number ASCII(40) UQ; Status int; PaymentMethod int; Currency(3)=VND; Subtotal/ShippingAmount/Total decimal(18,2) CHECK whole/nonnegative, total=sum; RecipientName(256), Email(256), Phone(30), ShippingAddress(1000)?; ExpiresAt UTC?; CommerceEntity audit/Version; UQ(CustomerId,OperationId); index(Status,ExpiresAt) |
| Ordering.OrderItems | Id Guid PK; OrderId/ProductId/VariantId Guid; Name(256)/Sku(100)/OptionDescription(2000) snapshots; FulfillmentKind int1..4; UnitPrice decimal(18,2), Quantity int1..999; Price whole/nonnegative; FK (ProductId,VariantId)→SKU; không chứa secret; aggregate Order bảo vệ ít nhất một item và tổng |
| Ordering.PhysicalStockReservations | Id Guid PK; OrderItemId/VariantId/InventoryItemId Guid; Quantity int>0; State Held=1/Released=2; CommerceEntity audit/Version |
| Inventory.InventoryItems.Reserved | int NOT NULL default0; CHECK 0<=Reserved<=OnHand; Available=OnHand-Reserved, inventory adjustment không giảm dưới Reserved |
| DigitalInventory.DigitalStockItems | State thêm Reserved=3; ReservedForOrderItemId Guid?; CHECK Available/Disabled không owner, Reserved phải Unique và owner bắt buộc; FK composite như trên; domain cấm bật/tắt khi đang giữ |

OrderStatus: PendingPayment=1 (trả trước), AwaitingProcessing=2 (COD), Cancelled=3, Expired=4. PendingPayment có ExpiresAt; COD không TTL. Không có trạng thái Paid/Delivered trong lát này. Carts/Orders/PhysicalStockReservations có version; OrderItem snapshot bất biến sau insert, history không hard-delete/soft-delete. CartItems thay đổi rõ ràng trong transaction. FK mặc định Restrict/NoAction, không cascade từ User/Product/SKU sang lịch sử.

Domain: Cart bảo vệ số lượng/trùng/giới hạn; Order bảo vệ snapshot/tổng/COD+hàng số và trạng thái hủy/hết hạn; InventoryItem Reserve/Release bảo vệ số không âm; DigitalStockItem Reserve/ReleaseUnique bảo vệ owner/mode và chống giữ hai lần. Shared chỉ cần nội dung Available, không giữ độc quyền hoặc nhân đôi link; Extension không tiêu thụ hàng.

### Transaction và idempotency

Một khóa DB ổn định CommerceLocks `ordering` serialize các mutation Order/Cart ở shop nhỏ. Tiếp theo khóa ProductType theo ID → Product theo ID → InventoryItem theo ID; digital mutations dùng cùng product lock với quản trị. Không giữ transaction khi gọi ngoài. Serial gate là lựa chọn đơn giản, chưa tối ưu throughput nhiều shop.

Checkout: khóa ordering → tìm OperationId cùng customer và so hash payload → khóa cart/kiểm version → khóa schema/products → kiểm Published/SKU active/giá/total kỳ vọng → Reserve physical/Unique → tạo Order/items, reservation history → clear cart/Touch → Save audit+nghiệp vụ cùng UoW. Mã băm chứa cartVersion/contact/method/expectedTotal; payload chuẩn hóa để so retry. Không tin giá hay phí ship client gửi, chỉ dùng expectedTotal kiểm khớp tổng server.

Hết hạn/hủy: khóa ordering → order và các product/type liên quan → balances → release toàn bộ holds → chuyển trạng thái trong cùng transaction. Worker bounded batch và nhiều instance an toàn nhờ gate/version/state; chạy lại không nhả lần hai. Checkout dọn lô đơn hết hạn trước để thu hồi kho kể cả worker chưa chạy. Hết hạn dư backlog có thể cần worker/những lượt tiếp theo, không bao giờ bán vượt tồn.

Mặc định `Ordering:ReservationMinutes=30` cấu hình 1..1440; `Ordering:FlatShippingAmount=0` VND nguyên đồng; worker interval/batch có giới hạn. TimeProvider DI dùng UTC, tests điều khiển thời gian không ngủ. Hủy đơn chưa có thanh toán/giao trong lát này; bước Payment phải thu hẹp quyền hủy theo paid/fulfilled state khi thêm.

Migration `OrderingCheckout` additive cho hai DB sau DigitalInventory, không sửa baseline cũ. Backfill Reserved=0/owner null, preserve Catalog/kho hiện có. Down mất lịch sử Order và không an toàn khi còn hold: guard từ chối rollback khi có holds trước khi drop; backup trước mọi migration deployment.

## 3. API và quyền

Customer endpoints yêu cầu đăng nhập; ownership kiểm ở service bằng current user, không nhận CustomerId trong body. Admin Order.View/Cancel, đăng ký/grant qua operator CLI, không tự cấp quyền lúc startup. Routes trong Catalog namespace để module flag hiện có tắt chung.

| Endpoint | Hợp đồng |
|---|---|
| GET /api/cart | CartDto với version và items/tên/giá hiện tại; chưa có cart trả Id empty/Version null/items rỗng |
| PUT /api/cart | SaveCart(Version nullable khi chưa có, Items[VariantId,Quantity]); thay toàn bộ giỏ; không giữ kho |
| POST /api/orders/checkout | Checkout(CartVersion,OperationId,PaymentMethod,RecipientName,Email,Phone,ShippingAddress?,ExpectedTotal); server tính tổng, snapshot và giữ hàng |
| GET /api/orders/mine | Page<OrderSummary> của khách |
| GET /api/orders/{id} | OrderDto chỉ của khách |
| POST /api/orders/{id}/cancel | Version, hủy/nhả holds của khách |
| GET /api/admin/orders và /{id} | Admin Order.View |
| POST /api/admin/orders/{id}/cancel | Admin Order.Cancel, version; service kiểm permission |

Response no-store. Order/cart không có ProtectedContent hoặc ID kho số để khách lấy trước khi trả tiền. Checkout không gửi email hoặc giao hàng. Stale version/total mismatch/OperationId khác payload/không đủ kho trả conflict; input sai trả400, ownership sai trả404, thiếu auth/ACL trả401/403.

## 4. Kế hoạch và nhật ký

| Bước | Đầu ra | Trạng thái |
|---|---|---|
| 1 | Kế hoạch/cardinality/API/transaction | Đã ghi trước code |
| 2 | Domain + contracts + persistence + migration | Đang triển khai |
| 3 | Cart/checkout/query/cancel/expiry/worker và ACL | Đang triển khai |
| 4 | Domain, hai provider, concurrency/idempotency/rollback/expiry/security | Chưa chạy |
| 5 | Docs/API/verification/history và commit | Theo bước |

2026-10-18: user yêu cầu tiếp tục. Đọc index/roadmap/MVP và code trước khi thiết kế. Không đổi mục tiêu một shop, quản trị linh hoạt tham khảo Webflow. Lát này không có frontend/admin UI.

## 5. Kiểm chứng và nghiệm thu

Chưa có kết quả kiểm thử cho lát này tại thời điểm tạo hồ sơ. [Báo cáo](../verification/2026-10-18-ordering-checkout.md) ghi lệnh/kết quả sau thực hiện. Build/local tests không xác nhận deployment/Linux/Docker/payment carrier thật.

## 6. Lịch sử commit

Source trước bước: `31a0109`. Hash commit mới chỉ ghi sau khi tồn tại; tra `git log -- docs/features/ordering-checkout.md`.

## 7. Còn lại

Hoàn tất bước 2–5. Sau đó Payment: nhận tiền/retry/webhook, trả muộn thử giữ lại toàn bộ hoặc chờ xử lý, delivery Unique/Shared, email; Extension/PendingVerification và vận chuyển/hủy-hoàn sau nhận tiền ở bước tiếp theo.
