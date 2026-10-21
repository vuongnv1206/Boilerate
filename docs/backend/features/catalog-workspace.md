# Catalog workspace — lưu editor và công cụ quản trị

Ngày bắt đầu/cập nhật: 2026-10-21. Trạng thái: source/contract BE đã triển khai, đang kiểm chứng regression. Khu vực: BE; phối hợp giao diện tại [hồ sơ shared](../../shared/features/catalog-workspace.md) và repo Frontend. Roadmap: [backend](../roadmap.md); hợp đồng chuẩn: [commerce API](../commerce-api.md).

## Mục tiêu và phạm vi

Editor lưu nội dung, gallery, nhãn/options, SKU mới, giá, ảnh và default bằng một transaction. Danh sách có thumbnail/schema/metadata, lọc cách giao và sort server-side; category có count; publish/archive nhiều sản phẩm nguyên tử. Duplicate mở bản sao chưa lưu ở FE rồi dùng CreateProduct, không sao chép tồn hoặc lịch sử. CSV là giai đoạn sau, không chặn Catalog.

Giữ ProductType/bộ trường và mọi FulfillmentKind, IDs của sản phẩm/SKU cũ, đơn/kho/reservation và API cũ. Options sau tạo chỉ đổi nhãn/thứ tự, thêm value và tổ hợp; không đổi trục, mã hoặc selections của SKU cũ. Published lưu có hiệu lực ngay; không có draft/live song song hoặc queued publish.

Tiêu chí chấp nhận:

- [x] Một save thay nội dung/options/SKU/ảnh/default thành công hoặc rollback toàn bộ; đổi default và ngừng SKU cũ, đổi ảnh SKU và bỏ ảnh cũ cùng save hoạt động.
- [x] Version Product/schema cũ, thiếu Price.Update hoặc final state sai không ghi một phần.
- [x] Bulk 1–100 explicit IDs/version chỉ Published/Archived; một lỗi rollback toàn lô.
- [x] List/filter/sort/count/metadata đúng; upload cho Product.Create hoặc Product.Update; quyền còn lại không mở rộng.
- [ ] Kiểm thử hai provider khi có kết nối, build và regression; phân biệt kiểm local với nghiệm thu deployment.

## Thiết kế

Không thay schema/database hoặc migrations. Giữ quan hệ/FK/unique/check/deletion hiện có trong [database contract](../commerce-database.md). Không thêm seed hoặc ghi đè dữ liệu operator.

Invariant: mọi Product luôn có đúng một default active; selections và IDs cũ ổn định, ảnh SKU thuộc gallery cuối, giá/required fields của Published hợp lệ. Domain áp dụng final variant/gallery state; service kiểm version/ACL và sở hữu transaction. Khóa type → product → media theo thứ tự ID. Bulk khóa toàn bộ types trước toàn bộ products. Media cleanup/outbox và balance MAIN=0 cho SKU Physical mới nằm cùng transaction; không nhập kho hoặc nội dung số qua save.

API: PUT `/api/products/{id}/editor` nhận content đầy đủ, optionEdits/variantEdits/addedVariants dạng delta và defaultVariantSku; POST create thêm defaultVariantSku tùy chọn, variant input thêm isActive/imageMediaId tùy chọn. POST `/api/products/bulk-status` nhận items id/version và target status. GET `/api/categories/summaries` trả id/productCount. DTO/search mở rộng additive. Publish vẫn quyền Product.Publish; editor Product.Update và Price.Update khi thực sự đổi/khởi tạo giá; upload kiểm Product.Create OR Product.Update trước durable intent.

## Các bước

1. Chốt contract và ghi hồ sơ trước khi sửa source — hoàn tất.
2. Domain/use cases/API/projections và tương thích — source đã triển khai.
3. Tests domain/provider/HTTP — targeted 31/31 đạt trên SQL Server/PostgreSQL; broad regression đang chạy.
4. Cập nhật API/roadmap/history và kết quả kiểm chứng — API đã cập nhật; tổng kết chờ regression.

## Kiểm chứng và phần còn lại

Release build với `RunAnalyzers=false` đạt, 0 lỗi/warnings; Debug copy bị khóa bởi Host đang chạy từ trước, không dừng Host của người dùng. Targeted `CatalogEditorTests|CommerceDomainTests|CommerceIntegrationTests` đạt **31/31, 0 fail/skip** trên SQL Server LocalDB và PostgreSQL 16 cluster cô lập; gồm 12 ca provider/HTTP mới và domain final-state cùng regression Catalog cũ. Raw TRX/log trong `artifacts/catalog-workspace-*`, không commit.

Broad regression lượt đầu phát hiện bốn test migration có hardcode latest `_RefundsReturns` trong khi repo đã thêm `_GroupedAuthorization`. Theo chỉ dẫn điều phối, chỉ thay assertion latest bằng `db.Database.GetMigrations().Last()` trong tests Shipping/Renewal/Ordering/AfterSales; giữ nguyên migration và các kiểm downgrade/history/roundtrip. Đây là bảo trì test baseline có từ trước, không phải lỗi schema Catalog mới. Lượt chạy lại được ghi sau khi hoàn tất.

Không đổi trạng thái runtime/deployment của Catalog v1. CSV tiếp tục hoãn. Chưa tạo commit.
