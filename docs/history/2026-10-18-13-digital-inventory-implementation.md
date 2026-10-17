# 2026-10-18 — Triển khai cách giao và kho hàng số

## Mục tiêu và thay đổi

Hoàn tất bước 1 BE của [MVP](../features/shop-mvp.md) theo [thiết kế trước code](../features/digital-inventory.md). Người dùng tiếp tục làm rõ: mục tiêu là **một phần mềm cho một shop thích ứng mặt hàng muốn bán**, quản trị sản phẩm tham khảo Webflow; không xây nền tảng quản lý nhiều shop. Đã sửa README/business discovery/roadmap để bỏ cách hiểu nhiều installation là mục tiêu phát triển.

- Product có Physical/DigitalUnique/DigitalShared/ManualExtension, độc lập bộ trường ProductType và Category N–N. SKU kế thừa mode; mode bất biến sau tạo trong lát này.
- DigitalStockItem và mappings hai DB bảo vệ owner/mode bằng composite FK, reference unique, tối đa một Shared/SKU, trạng thái/version và Restrict. Không tạo balance vật lý cho SKU số; chặn dùng physical adjustment sai mode.
- Thêm service/handler/controller nhập Unique, Shared upsert, metadata list, bật/tắt và Reveal. Mọi service operation kiểm ACL riêng; write kiểm/touch Product.Version trong UoW. Import lỗi không ghi dở lô.
- Nội dung mã hóa theo item ID bằng Data Protection. Metadata/public API/audit không có plaintext/ciphertext; Reveal có quyền riêng và audit Read. Bảo vệ key ring/backup được ghi trong runbook.
- Public availability đọc đúng nguồn kho. Thêm migration additive DigitalInventory cho mỗi provider; giữ nguyên InitialFoundation/CommerceCatalog. Cập nhật helper constructor, fresh migration test, test domain/provider/HTTP/concurrency/upgrade.

## File liên quan

`src/Domain/Catalog`, `src/Domain/DigitalInventory`, `src/Domain/Auditing/TrailType.cs`; `src/Application/Catalog`; `src/Infrastructure/Commerce`, persistence/configuration/context/bootstrap và audit; `src/Host/Controllers/Catalog/DigitalInventoryController.cs`; `src/Shared/Authorization`; hai `src/Migrators.*`; các test DigitalInventory/Commerce/Recovery/FreshBaseline; [database](../commerce-database.md), [API](../commerce-api.md), [runbook](../operations-runbook.md), [feature](../features/digital-inventory.md), [verification](../verification/2026-10-18-digital-inventory.md), [roadmap](../roadmap.md), docs index/business/MVP và README.

## Kiểm chứng thực tế

- Build tích hợp đạt 0 errors, 2140 analyzer/style warnings; rebuild có migrations/test mới đạt 0 errors, 957 warnings cho phần được build lại. Có lỗi build tạm trong lúc tích hợp (file output bị giữ, thiếu implementation/constructor param), đã sửa và chạy lại; không gọi các lượt đó là test đạt.
- Domain: 10 passed, 0 failed/skip.
- Provider trên SQL Server LocalDB và PostgreSQL 16: 16 passed, 0 failed/skip, gồm DB constraints, nguyên lô/rollback, stale/concurrent version, HTTP ACL/no-store, giữ dữ liệu khi upgrade từ CommerceCatalog.
- Regression ngoài Container/Recovery/Template smoke: **184 passed, 0 failed/skip**. Regression chứa các ca đã chạy riêng, không cộng thành 210 test độc lập.
- Lệnh/artifact và phạm vi không chạy: [báo cáo](../verification/2026-10-18-digital-inventory.md). Kiểm 42 file Markdown/208 relative links và `git diff --check` đạt; không đưa TRX/log/keys/content thật vào Git.

## Còn lại và bước tiếp

Chưa có Cart/Order/reservation, thanh toán, cấp phát theo đơn, email hoặc yêu cầu gia hạn/PendingVerification. Tiếp theo là Cart/Order/giữ hàng 30 phút cho đơn trả trước theo MVP. Frontend quản trị giống Webflow chưa nằm trong lát BE này. Runtime Linux/Docker/CI/adapter thật còn gate riêng trước vận hành.

Commit thiết kế đã tồn tại: `cd12a21`. Hash triển khai tra bằng `git log -- docs/history/2026-10-18-13-digital-inventory-implementation.md` sau khi commit.
