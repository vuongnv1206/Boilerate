# Cách giao sản phẩm và kho hàng số

Ngày bắt đầu/cập nhật: 2026-10-18, Asia/Saigon. Trạng thái: **đã triển khai và kiểm chứng local**, bước 1 của [Shop MVP](shop-mvp.md). Chưa nghiệm thu deployment thật.

## 1. Mục tiêu và phạm vi

Mục tiêu là một phần mềm cho một shop thích ứng với các mặt hàng muốn bán; Webflow là tham khảo trải nghiệm quản trị sản phẩm. Shop chọn cách giao được hỗ trợ khi tạo Product, sau đó quản lý mã/tài khoản Unique hoặc nội dung Shared theo SKU. Dùng lại ProductType, fields, categories, options/SKU hiện có; không tạo module theo tên mặt hàng hoặc mô hình quản lý nhiều shop.

Trong lát kho số ban đầu: cách giao Physical/DigitalUnique/DigitalShared/ManualExtension; nhập kho Unique, lưu nội dung Shared, bật/tắt bản ghi, danh sách metadata và xem nội dung bằng quyền riêng. Cập nhật các lát tiếp nối: [Ordering](ordering-checkout.md) đã thêm reservation, [nhận tiền/giao số](payment-digital-delivery.md) thêm Delivered ownership và snapshot giao số/email outbox. Yêu cầu gia hạn/PendingVerification và gateway còn để sau; không coi chúng đã triển khai từ việc có kho số.

Tiêu chí chấp nhận: tạo đúng cách giao; dữ liệu có chủ sở hữu và ràng buộc hai DB; import nguyên lô hoặc rollback; stale version/duplicate reference conflict; nội dung được mã hóa, không có trong public API/danh sách/audit; ACL kiểm ở API và service; availability phản ánh đúng nguồn kho và không cho điều chỉnh kho vật lý của hàng số.

## 2. Quyết định thiết kế

### Domain và database

Chọn cách giao ở **Product**, toàn bộ SKU kế thừa. `ProductType` chỉ quyết định bộ trường. `FulfillmentKind` bất biến sau tạo trong lát này, tránh đổi nguồn tồn khi đã có dữ liệu; muốn chuyển cách giao cần nghiệp vụ riêng sau này. Product cũ được gán Physical=1 khi migrate. Draft có thể chưa nhập kho; Publish không yêu cầu còn hàng.

| Quan hệ | Cardinality | Cách lưu/ràng buộc |
|---|---|---|
| ProductType → Product | 1–N, không đổi | ProductTypeId FK hiện có |
| Product ↔ Category | N–N, không đổi | ProductCategories hiện có |
| Product → SKU | 1–N, không đổi | ProductVariants.ProductId |
| Product/SKU → DigitalStockItem | 1–N với Unique; tối đa 1 Shared/SKU | FK (ProductId,VariantId) → ProductVariants(ProductId,Id); FK (ProductId,FulfillmentKind) → Products(Id,FulfillmentKind) |

| Bảng/cột | Kiểu và khóa | Constraints/index/concurrency |
|---|---|---|
| Catalog.Products.FulfillmentKind | int NOT NULL default 1; 1 Physical, 2 DigitalUnique, 3 DigitalShared, 4 ManualExtension | CHECK IN (1,2,3,4); AK (Id,FulfillmentKind); Product.Version hiện có |
| DigitalInventory.DigitalStockItems | Id Guid PK; ProductId/VariantId Guid; FulfillmentKind int; Reference ASCII(100); ProtectedContent nvarchar(max) SQL Server / varchar(16000) PostgreSQL, domain giới hạn 16000 ký tự; State int; audit attribution và Version(32) từ CommerceEntity | Kind IN (2,3); State Available=1/Disabled=2; UQ (VariantId,Reference); filtered UQ VariantId WHERE Kind=3; indexes (ProductId,VariantId,State), (ProductId,FulfillmentKind); Version concurrency |

`Reference` là nhãn quản lý ASCII chuẩn hóa uppercase, unique trong SKU; không dùng credential làm nhãn. Unique content bất biến; nhập lại reference trả conflict, không tạo dòng thứ hai. Shared dùng reference cố định `SHARED`, cập nhật nội dung cùng dòng. Không suy ra Shared có hai quyền sử dụng hoặc giá miễn phí khi tăng số lượng.

DigitalStockItem bảo vệ ID, kind, reference, state và thay Shared content; nội dung nhập không trống, tối đa 8000 byte UTF-8, giữ nguyên ký tự. Infrastructure mã hóa bằng ASP.NET Data Protection, purpose version 1 và ID của bản ghi. Cột ProtectedContent không được đưa vào audit; audit giữ metadata/thay đổi version. Xem nội dung tạo audit theo ID, không ghi nội dung. Phải giữ/backup key ring với database; không dùng key tạm cho triển khai thật.

Write khóa theo ProductType → Product như Catalog hiện có, kiểm Product.Version và quyền DigitalInventory, ghi dữ liệu/audit trong CommerceUnitOfWork. Mọi write kho số Touch Product; response metadata có ProductVersion mới. Không gọi dịch vụ ngoài. Import tối đa 100 dòng, cùng transaction; duplicate trong lô hoặc ở DB rollback toàn bộ. Retry với version cũ trả 409; đọc lại metadata trước khi gửi lại. Bản ghi có Version riêng để chuẩn bị trạng thái cấp phát ở mốc Order.

FK Restrict/NoAction; không soft-delete hoặc hard-delete kho số qua API/persistence, dùng Disabled. Unique không sửa nội dung sau nhập. Mốc sau bổ sung Reserved/Allocated và liên kết đơn bằng migration riêng; hiện chưa thể coi dòng là đã bán.

Chỉ Physical tạo InventoryItem MAIN/StockMovement. Unique InStock khi có dòng Available; Shared khi nội dung dùng chung Available; ManualExtension là nhận yêu cầu nhân viên, không tiêu thụ balance vật lý. Mọi trường hợp vẫn cần SKU hoạt động. Kiểm tra mode khi điều chỉnh vật lý, không có nguồn tồn số trùng trong OnHand.

Thêm migration DigitalInventory cho SQL Server và PostgreSQL, không sửa InitialFoundation/CommerceCatalog. Migration additive trên baseline Catalog mới của repo, default Physical bảo toàn dữ liệu đã có. Down loại bỏ dữ liệu kho số nên cần backup trước; không đưa script/log/key/secret vào Git.

### API, quyền và vận hành

CreateProduct thêm FulfillmentKind mặc định Physical; ProductDto/ProductSummary trả cách giao. ProductContent/EditProduct không nhận nội dung kho số hoặc sửa kind.

| Use case | Endpoint dự kiến | Quyền |
|---|---|---|
| Danh sách metadata theo SKU | GET /api/digital-inventory/variants/{variantId} | DigitalInventory.View |
| Nhập mã/tài khoản Unique | POST /api/digital-inventory/variants/{variantId}/items | DigitalInventory.Import |
| Tạo/cập nhật Shared | PUT /api/digital-inventory/variants/{variantId}/shared-content | DigitalInventory.Update |
| Bật/tắt bản ghi | PUT /api/digital-inventory/variants/{variantId}/items/{id}/availability | DigitalInventory.Update |
| Xem nội dung riêng lẻ | POST /api/digital-inventory/items/{id}/reveal | DigitalInventory.Reveal |

Write nhận ProductId/VariantId và Product.Version; route/body không khớp trả 400, sai owner/mode/không tồn tại không ghi dữ liệu; stale/duplicate trả 409. Không nhận nội dung kho trong payload tổng hợp Product. Metadata không trả ciphertext/plaintext. Response kho số no-store; reveal chỉ giải mã sau kiểm quyền và lưu audit. Bootstrap manifest bổ sung capability qua operator CLI hiện có, không tự grant khi HTTP startup. Catalog module off giữ cơ chế route/module hiện có.

### Quyết định 2026-10-18

Chọn cấp Product thay vì ghi đè mode theo SKU và một bảng kho số thay vì tách bảng cho mã/link/account. Cách này đáp ứng shop nhỏ và giữ mode tách khỏi schema dữ liệu; các nghiệp vụ mới bổ sung sau theo nhu cầu, không dùng workflow builder.

## 3. Kế hoạch từng bước

| Bước | Công việc | Trạng thái |
|---|---|---|
| 1 | Thiết kế phạm vi, cardinality, constraints, API/quyền | Đã ghi; đang triển khai theo thiết kế này |
| 2 | Domain, EF mappings, audit protection và migrations hai DB | Đã triển khai; fresh/upgrade và constraints đạt trên hai DB |
| 3 | Service/handlers/controllers, ACL/bootstrap và public availability | Đã triển khai; kiểm service/HTTP hai DB đạt |
| 4 | Domain/API/provider regression và kiểm migration trên hai DB | Domain 10/10, provider 16/16 và regression 184/184 đạt; không cộng các lượt trùng ca |
| 5 | Cập nhật hợp đồng, báo cáo verification/history và commit nhỏ | Đã cập nhật hợp đồng/verification/history, commit theo bước |

## 4. Nhật ký triển khai

2026-10-18: bắt đầu từ `8f98dbe`, user yêu cầu tiếp tục code. Ghi thiết kế trước substantive implementation; đã đọc docs index/roadmap/MVP, review model/UoW/ACL/audit hiện hành. Chưa có kết quả test mới ở bước lập hồ sơ.

2026-10-18: triển khai domain, API, encryption/audit, public availability và migration additive hai DB. Build tích hợp đạt sau cập nhật các helper tạo CommerceService. Domain 10/10 và provider 16/16 đạt, gồm upgrade có dữ liệu và import đồng thời. Người dùng làm rõ mục tiêu một shop thích ứng sản phẩm, tham khảo quản trị Webflow; đã đồng bộ tài liệu và không bổ sung mô hình quản lý nhiều shop.

2026-10-18: regression ngoài Container/Recovery/Template smoke đạt 184/184, 0 failed/0 skipped. Cập nhật database/API/runbook/MVP/roadmap và [history triển khai](../history/2026-10-18-13-digital-inventory-implementation.md). Chưa chạy deployment/adapter thật trong lát này.

## 5. Kiểm chứng và nghiệm thu

Đã chạy build, 10 domain cases, 16 provider cases trên SQL Server LocalDB/PostgreSQL 16 và regression 184 cases, đều đạt. Regression chứa lại các ca đã chạy riêng. Kết quả/lệnh ở [báo cáo](../verification/2026-10-18-digital-inventory.md). Linux/Docker/deployment vẫn cần gate riêng; Container/Recovery/Template smoke không chạy lại trong đợt này.

## 6. Lịch sử commit

`cd12a21`: thiết kế/phạm vi trước code. Commit triển khai tra bằng `git log -- docs/features/digital-inventory.md` sau khi tồn tại.

## 7. Công việc còn lại

[Cart/Order/reservation](ordering-checkout.md) đã bổ sung State Reserved và owner cho Unique ở migration tiếp theo. Nội dung hồ sơ kho số này ghi phạm vi ban đầu; hợp đồng hiện hành nằm ở [database](../commerce-database.md)/[API](../commerce-api.md). Chưa giao hàng số sau nhận tiền hoặc gia hạn; runtime/deployment và adapter thật vẫn cần nghiệm thu trước khi vận hành shop.
