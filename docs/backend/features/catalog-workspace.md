# Catalog workspace — editor Webflow

Cập nhật 2026-10-22. Source BE đã triển khai, SQL Server local đã migrate và dùng qua UI thật. PostgreSQL có migration nhưng chưa chạy lượt mới do thiếu cấu hình test. Phối hợp tại [hồ sơ shared](../../shared/features/catalog-workspace.md); hợp đồng tại [Commerce API](../commerce-api.md).

## Editor danh mục theo Webflow

Hợp đồng giao diện ở [Collections FE](../../../../Frontend/docs/features/live-admin-collections.md). `PUT /api/categories/{id}/products` nhận delta `{productId,version,included}` và trả version mới; quyền `Product.Update`, tìm kiếm dùng `POST /api/products/search` với `Product.View`. Một batch tối đa1.000 sản phẩm; toàn bộ batch chạy trong CommerceUnitOfWork, giữ danh mục khác, chỉ touch/audit Product nếu membership thật sự đổi. Category gate đồng bộ với edit/delete; khóa ProductType tăng dần rồi Product theo ProductTypeId/Id để cùng thứ tự với ghi Product. Một Product vẫn tối đa100 Category. Không đổi schema/migration.

FE lưu Category trước rồi gửi delta membership. Nếu membership conflict, phần Category đã lưu còn draft chip được giữ để đối chiếu/retry; batch membership rollback toàn bộ. Hai root dùng hai mutation nên không có transaction xuyên suốt cả editor.

## Hành vi và transaction

POST create/PUT editor nhận cấu hình option/variant đầy đủ bằng ID. Nhãn và SKU được sửa, mã nội bộ tự sinh và ổn định. SKU không bắt buộc hoặc duy nhất; default dùng ID, ID tạm được map sang ID thật trong response. Ba nhóm và 50 biến thể mới; dữ liệu cũ vượt giới hạn không bị cắt.

Khóa type/product rồi validate toàn bộ, retire các bản ghi bị loại, cập nhật selections/ảnh/default và tồn đích trong cùng CommerceUnitOfWork transaction. Product.Version/Type.Version chống ghi đè. Giá đổi cần Price.Update; số lượng/tracking đổi cần Inventory.Adjust, InventoryVersion và operation ID cho adjustment. Kho không dưới Reserved. Biến thể giữ ID khi tổ hợp không đổi; xóa trục tái tạo từ mặc định, xóa hết trục giữ một biến thể đơn. Dòng cũ được retire, không xóa FK/đơn/reservation/movement.

Ảnh bổ sung lưu VariantMedia; ProductMedia là tập liên kết phục vụ preview/public và cleanup, giới hạn 30 ảnh bổ sung mỗi biến thể cộng ảnh chính. Cleanup giữ asset đang được variant lịch sử tham chiếu. Kích thước/cân nặng nullable, không âm. Tắt tracking cho physical hiển thị vô hạn, không giữ/consume kho; OrderItem chụp TrackInventory để đơn cũ vẫn theo đúng chính sách lúc mua. Shipment không theo dõi tồn không có StockMovementId; nhận hàng trả cho dòng này chỉ được RestockQuantity=0.

Product.Delete yêu cầu quyền riêng, đánh dấu IsDeleted/Archived; search, detail public/admin, checkout và category count bỏ sản phẩm đã xóa. Đơn/kho cũ vẫn đọc/hoàn tất. Published save có hiệu lực ngay; không có queued publish, thuế tự động hoặc membership định kỳ.

## Migration

WebflowCatalogEditor: SQL Server 20261022104349, PostgreSQL 20261022104358. Thêm IsDeleted/IsRetired/TrackInventory/kích thước/VariantMedia/OrderItem.TrackInventory; StockMovementId của shipment nullable; index SKU thành non-unique, combination unique chỉ với variant chưa retire. Default tracking=true bảo toàn đơn/sản phẩm cũ. Up không reset dữ liệu. Down kiểm dữ liệu không thể biểu diễn ở schema cũ (SKU trống/trùng, tracking=false, retired/deleted, ảnh/kích thước hoặc shipment không có movement) trước DDL và từ chối downgrade gây mất dữ liệu.

## Kiểm chứng

Hai hồi quy Webflow trên SQL Server: cấu hình đầy đủ/ID/SKU trống-trùng/ảnh/tồn/retirement/delete, và checkout-COD/ship/deliver không theo dõi tồn với SKU trống. UI local đã tạo/sửa/lưu/reload sản phẩm 2×2, ảnh, giá, default, stock, đổi nhãn và xóa cấu hình, duplicate/archive/delete. Build/lint và ghi nhận responsive/theme nằm ở hồ sơ FE. Không chạy lại toàn bộ suite; các thống kê broad suite trước đây không đại diện phiên bản editor mới. Chưa commit hoặc nghiệm thu deployment ngoài local.

Danh mục editor v2: endpoint và FE source đã build. Chưa chạy test/API HTTP hoặc browser E2E cho membership delta, conflict rollback, quyền hay thao tác cuối trang; API local hiện tại chưa nạp Host build mới nên chưa xác nhận runtime.
