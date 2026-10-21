# Catalog v1 — kế hoạch và tiến độ

Cập nhật vận hành gần nhất: 2026-10-18; xem [operations acceptance](operations-acceptance.md).

2026-10-21: cải tổ trải nghiệm catalog và các API cần thiết được theo dõi tại [Catalog workspace](catalog-workspace.md): unified save, ảnh/default, summary/filter/sort/count và bulk publish/archive; CSV hoãn. Không thay schema hoặc identity của Product/SKU hiện có.

Phối hợp BE–FE được theo dõi tại [hồ sơ dùng chung](../../shared/features/catalog-workspace.md); contract kỹ thuật giữ tại API BE, trạng thái giao diện giữ tại repo Frontend.

Lát UI vận hành SKU/tồn (2026-10-21): bổ sung bộ lọc tùy chọn `productId` và `variantId` cho `GET /api/inventory`. Hai bộ lọc kết hợp bằng AND, áp dụng trước count/phân trang và giữ nguyên quyền Inventory.View cùng DTO; không thay đổi database hay mutation. Mục tiêu là để product editor đọc đúng số dư thay vì quét toàn kho. Kiểm chứng SQL Server và FE đang thực hiện.

## Mục tiêu và giới hạn

Một shop bán hàng vật lý, VND nguyên đồng, một kho MAIN không âm. Quản trị sản phẩm linh hoạt bằng ProductType/schema, options/SKU và category. Giữ modular monolith, DDD tại Product/ProductType/InventoryItem; handler điều phối use case, quyền và persistence.

Tài liệu này ghi lại kế hoạch đã được duyệt và tiến độ thực tế từ đợt triển khai 2026-10-16 đến review 2026-10-17. Các bước bên dưới được tổng hợp hồi cứu từ code và bằng chứng đã có; không giả định rằng mỗi bước đã có commit riêng ở thời điểm triển khai.

## Thiết kế được chốt

- ProductType → Product: **1–N**, ProductTypeId bắt buộc và chưa hỗ trợ đổi loại.
- Product ↔ Category: **N–N** qua ProductCategories; không có category chính.
- Category cha → con: **1–N**, adjacency list; chặn chu trình.
- Product → SKU/options/gallery/field values; SKU là nguồn giá. Một Product luôn có SKU và đúng một default hoạt động.
- Giá và tồn kho không phải cột nguồn chuẩn trên Product. StockMovement bất biến, adjustment có OperationId idempotent.
- Media có durable upload intent, vòng đời Ready/Deleting/Deleted và transactional outbox cleanup; generic domain events vẫn chạy trong process sau commit.

Chi tiết đầy đủ: [ERD và từ điển database](../commerce-database.md), [hợp đồng API/vận hành](../commerce-api.md).

## Các bước và tiêu chí hoàn thành

| Bước | Công việc | Tiêu chí và kết quả hiện tại |
|---|---|---|
| 1 | Chốt phạm vi, cardinality, ownership, transaction, xóa và migration | Đã chốt trong thiết kế database; không tạo Ordering/Payment/Shipping ở mốc này |
| 2 | Cập nhật dependency có advisory, giữ locked restore | Đã pin MessagePack và cập nhật lock files; audit lưu tại báo cáo kiểm chứng |
| 3 | ProductType/typed fields, category, Product/options/SKU, giá và publication | Đã triển khai; custom field đúng kiểu, FK ghép và version; API JSON thay contract mẫu |
| 4 | Kho MAIN và adjustment/history | Đã triển khai và kiểm thử cạnh tranh, không âm, idempotency, lịch sử append-only |
| 5 | Media lifecycle, outbox worker, retry/dead-letter/replay | Đã triển khai và kiểm thử rollback/restart/lease/concurrent cleanup ở môi trường local |
| 6 | API public, bộ lọc typed và ACL chuyên biệt | Đã kiểm thử Draft không lộ, cập nhật Published có hiệu lực, quyền giá/publish/kho và so sánh số trên hai DB |
| 7 | Review transaction/event, lỗi production và filter bounds | Đã sửa và kiểm thử hồi quy; xem [review](../commerce-review.md) |
| 8 | Runtime non-root, key ring/media volume, proxy/CORS, Linux CI gate | Local key/proxy gate 5/5; operator/recreate image gate đã viết; **chưa nghiệm thu container/Linux/CI thực tế** |
| 9 | Template đóng gói/đổi tên và tài liệu | Đã kiểm tra package không mang log/TRX/runtime data; template tên mới chạy bộ ca review 9/9 |
| 10 | Nghiệm thu vận hành và adapter được bật | Native backup/restore DB+media+keys/outbox local đạt 2/2; **còn Linux/container, deployment restore và adapter thật**, theo [roadmap](../roadmap.md) |

## Bằng chứng và phần còn lại

[Báo cáo kiểm chứng](../verification/2026-10-17-commerce.md) phân biệt các lượt test và thời điểm source, không cộng các lượt chồng lặp thành tổng ca duy nhất. [Lịch sử từng bước](../history/README.md) liên kết thay đổi, quyết định và commit.

Phạm vi Catalog v1 ban đầu không gồm reservation/checkout/Orders/payment/shipping. Các lát Ordering, Payment, Shipping, Renewal và hậu mãi đã được bổ sung ở các hồ sơ riêng trong [roadmap](../roadmap.md); không dùng giới hạn hồi cứu của Catalog v1 để suy ra trạng thái code hiện tại. Payment attempts/gateway còn ở giai đoạn sau. Không xem milestone Catalog là đã nghiệm thu production.
