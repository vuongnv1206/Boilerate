# Giỏ hàng, đơn hàng và giữ hàng — Shop MVP bước 2

Ngày bắt đầu/cập nhật: 2026-10-18, Asia/Saigon. Trạng thái: **đã triển khai và kiểm chứng local**, bước 2 của [MVP](shop-mvp.md); chưa nghiệm thu deployment. Source đầu đợt `31a0109`.

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
| GET /api/cart | CartDto với version, items/tên/giá hiện tại, subtotal/shipping/total và CanCheckout; chưa có cart trả Id empty/Version null/items rỗng |
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
| 2 | Domain + contracts + persistence + migration | Đã triển khai; fresh/constraints/Down guard/Down-Up hai provider đạt |
| 3 | Cart/checkout/query/cancel/expiry/worker và ACL | Đã triển khai; provider/HTTP/stock race/expiry đạt |
| 4 | Domain, hai provider, concurrency/idempotency/rollback/expiry/security | Regression cuối222/222 đạt, gồm12 domain Ordering,24 integration Ordering và2 migration cases; không cộng các lượt trùng |
| 5 | Docs/API/verification/history và commit | Đã cập nhật hợp đồng, báo cáo/history và commit theo lát |

2026-10-18: user yêu cầu tiếp tục. Đọc index/roadmap/MVP và code trước khi thiết kế. Không đổi mục tiêu một shop, quản trị linh hoạt tham khảo Webflow. Lát này không có frontend/admin UI.

2026-10-18: code domain/API/persistence/worker đã tích hợp. Sửa Cart.ReplaceItems theo diff để giữ identity EF của dòng cùng SKU; đổi Inventory.Adjust sang type→product→balance sau review deadlock; bổ sung Cart quote gồm shipping/total và canonical decimal trong RequestHash. Domain12/12 và22provider cases đạt, bao gồm nhiều worker/checkout/adjust đồng thời. Các lượt build tích hợp ban đầu có lỗi thiếu using CommerceLock, đã sửa/build lại; chưa coi là kết quả nghiệm thu deployment.

2026-10-18: review phát hiện Money bounds trên MapCart sau SaveCart có thể làm giỏ đã commit không đọc được khi tổng quá lớn hoặc giá tăng. Giữ quote/cart/version đọc được, thêm CanCheckout=false cho tổng không thể checkout/giỏ rỗng; Order vẫn bảo vệ miền tiền khi tạo. Bổ sung provider regression giảm/tăng/xóa giỏ ở giới hạn này. Migration guard/Down-Up đạt2/2; regression cuối cần chạy lại trên bản sửa.

2026-10-18: build cuối đạt0errors/1836warnings ở phần build lại; regression sau sửa quote/lookup locks đạt **222 passed,0 failed,0 skipped**, gồm cả24 integration Ordering và2 migration cases trên hai DB. Kiểm chứng/lệnh ở [báo cáo](../verification/2026-10-18-ordering-checkout.md), tổng kết ở [history](../history/2026-10-18-15-ordering-checkout-implementation.md). Đây là local verification; chưa chấp nhận deployment thật.

## 5. Kiểm chứng và nghiệm thu

Domain riêng12/12, provider riêng22/22, migration riêng2/2 và regression cuối222/222 đều đạt; regression bao gồm24 Ordering integration cases sau thêm bản sửa giỏ và lặp lại các ca đã chạy riêng. [Báo cáo](../verification/2026-10-18-ordering-checkout.md) ghi phạm vi/lệnh. Không chạy Container/Recovery/Template smoke trong lượt cuối; local tests không xác nhận deployment/Linux/Docker/payment/carrier thật.

## 6. Lịch sử commit

Source trước bước: `31a0109`; `dfcc92f` ghi thiết kế trước commit code. Hash commit triển khai chỉ ghi sau khi tồn tại; tra `git log -- docs/features/ordering-checkout.md`.

## 7. Còn lại

Bước tiếp theo Payment: nhận tiền/retry/webhook, trả muộn thử giữ lại toàn bộ hoặc chờ xử lý, delivery Unique/Shared, email; Extension/PendingVerification và vận chuyển/hủy-hoàn sau nhận tiền theo các bước MVP. Chưa có các use case này trong code; deployment/runtime còn gate riêng.
