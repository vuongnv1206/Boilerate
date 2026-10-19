# 2026-10-18 — Giỏ, đơn snapshot và giữ hàng

## Thay đổi và lý do

Triển khai [bước2MVP](../features/shop-mvp.md) theo [thiết kế Ordering](../features/ordering-checkout.md), một shop có sản phẩm linh hoạt; chưa thêm payment hoặc dịch vụ ngoài.

- Cart của khách đăng nhập, thay số lượng có version và quote giá/ship/tổng server; giỏ chưa giữ hàng.
- Checkout chung vật lý/số, snapshot, expectedTotal và OperationId/hash chống tạo lại đơn. Giá thay đổi cần xác nhận lại. Server giữ đủ toàn bộ, clear cart và ghi audit trong cùng UoW.
- Vật lý Reserved/Available với reservation history; Unique Reserved theo OrderItem và composite FK đúng SKU. Shared dùng chung nội dung, không giữ độc quyền; Extension chưa tiêu thụ kho.
- TTL trả trước mặc định30phút/cấu hình; COD chỉ vật lý, không TTL chờ trả trước. Hủy/expiry nhả đúng một lần, worker bounded batch; owner/admin ACL và no-store, không lộ secret.
- Model/migrations hai provider additive; Down chặn khi còn holds. OrderItem snapshot và lịch sử không hard-delete. Cập nhật Inventory.Adjust theo thứ tự khóa type→product→balance để tránh deadlock với checkout; Cart diff giữ đúng EF tracking cho dòng SKU được sửa.

## File ảnh hưởng

Domain.Ordering + InventoryItem/DigitalStockItem; Application.Ordering/Inventory DTO; Infrastructure OrderingService/Settings/worker, mapping/context/audit/bootstrap; OrdersController và ACL; hai migrators/snapshots; Ordering domain/integration/migration tests và FreshBaselineMigrationTests. Hợp đồng tại [database](../commerce-database.md), [API](../commerce-api.md), [runbook](../operations-runbook.md), [verification](../verification/2026-10-18-ordering-checkout.md); index/MVP/roadmap/business docs giữ trạng thái hiện hành.

## Kiểm chứng

Build cuối đạt0errors/1836analyzer-style warnings ở phần build lại. Domain riêng12/12, provider riêng22/22 và migration2/2 đạt. Sau review sửa giỏ vượt miền tiền luôn đọc/sửa được và thêm CanCheckout, regression bản cuối đạt **222 passed,0 failed,0 skipped**, gồm24 Ordering integration trên hai DB và2 migration cases. Migration guard thực sự chặn Down còn hold, Down/Up sau cancel giữ Catalog/balance/ciphertext và decrypt đúng. Không gộp số ca trùng giữa các lượt; [báo cáo](../verification/2026-10-18-ordering-checkout.md) ghi lệnh/artifacts/phạm vi không chạy.

Có lỗi thiếu using lúc tích hợp đã sửa/build lại. Read-only review phát hiện deadlock lock-order và giỏ quá tổng tiền không đọc được, đã sửa trước commit và kiểm regression. Raw log/TRX/keys không commit. Mocks quyền trong service tests không được coi là kiểm adapter ngoài; HTTP tests dùng ACL/Identity thật của fixture. Kiểm46 file Markdown/248 relative links, đối chiếu TRX counters và `git diff --check` đều đạt.

## Còn lại và bước tiếp

Chưa có nhận tiền, late-payment reacquire, giao Unique/Shared/email, yêu cầu gia hạn/PendingVerification, Shipment hoặc xử lý hủy/hoàn sau thanh toán. Bước tiếp theo là Payment/giao số theo MVP. Frontend admin tham khảo Webflow và runtime deployment chưa nghiệm thu bởi lát này.

Commit thiết kế đã tồn tại: `dfcc92f`. Hash triển khai tra bằng `git log -- docs/history/2026-10-18-15-ordering-checkout-implementation.md` sau commit.
