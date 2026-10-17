# 2026-10-18 — Thiết kế cách giao và kho số trước code

Người dùng yêu cầu tiếp tục triển khai, bắt đầu bước 1 của [MVP](../features/shop-mvp.md). Đã đọc docs index/roadmap/feature plan và các model/UoW/ACL/audit liên quan.

## Quyết định và file liên quan

- Tạo [hồ sơ tính năng](../features/digital-inventory.md) trước substantive implementation: Product chọn Physical/Unique/Shared/ManualExtension; ProductType vẫn là bộ trường. Mode bất biến trong lát này.
- Chốt bảng DigitalStockItems theo SKU, composite FK chống sai owner/mode, reference unique, một Shared/SKU, check/state/version và no hard-delete. Nội dung mã hóa theo ID bằng Data Protection; audit chỉ metadata, Reveal có quyền và audit riêng.
- Import nguyên lô/rollback và Product.Version; không tạo balance vật lý cho SKU số. Giữ các việc Order/reservation/cấp phát/nhận tiền/gia hạn theo đơn ở bước sau.
- Cập nhật [docs index](../README.md), [roadmap](../roadmap.md), [MVP](../features/shop-mvp.md), [history index](README.md), khởi tạo [báo cáo kiểm chứng](../verification/2026-10-18-digital-inventory.md).

## Kiểm chứng và bước tiếp

Đối chiếu thiết kế với quyết định người dùng và kiến trúc hiện hành. Chưa có provider test/migration acceptance cho tính năng mới. Bổ sung domain, mapping, migration hai DB, service/API/ACL và kiểm thử trong commit triển khai kế tiếp; không suy diễn cấu hình/code đang viết thành nghiệm thu production.
