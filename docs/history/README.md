# Lịch sử phát triển

- 2026-10-18: [Chuyển trọng tâm sang kế hoạch nghiệp vụ](2026-10-18-05-business-discovery.md).
- 2026-10-18: [Sản phẩm cấu hình phức tạp và mục tiêu nhiều shop](2026-10-18-06-business-products-multishop.md).
- 2026-10-18: [Chốt installation riêng và bổ sung hàng số](2026-10-18-07-separate-shops-digital-goods.md).
- 2026-10-18: [Chốt giao hàng số sau thanh toán](2026-10-18-08-digital-delivery-flow.md).
- 2026-10-18: [Cấp phát Unique/Shared và gia hạn có xác minh](2026-10-18-09-digital-allocation-renewal.md).
- 2026-10-18: [Sản phẩm linh hoạt, đơn chung và trả trước hàng số](2026-10-18-10-product-flexibility-mixed-cart.md).
- 2026-10-18: [Giữ hàng và MVP gọn cho shop nhỏ](2026-10-18-11-small-shop-mvp.md).
- 2026-10-18: [Thiết kế cách giao và kho số trước code](2026-10-18-12-digital-inventory-design.md).
- 2026-10-18: [Triển khai và kiểm chứng cách giao/kho số](2026-10-18-13-digital-inventory-implementation.md).
- 2026-10-18: [Thiết kế giỏ/đơn và giữ hàng trước code](2026-10-18-14-ordering-checkout-design.md).
- 2026-10-18: [Triển khai giỏ, đơn snapshot và giữ hàng](2026-10-18-15-ordering-checkout-implementation.md).
- 2026-10-18: [Thiết kế nhận tiền và giao hàng số](2026-10-18-16-payment-delivery-design.md).
- 2026-10-18: [Nhận tiền và tự động giao Unique/Shared](2026-10-18-17-payment-delivery-implementation.md).
- 2026-10-18: [Email thông báo giao hàng số qua outbox](2026-10-18-18-digital-delivery-email.md).
- 2026-10-18: [Nhất quán timestamp UTC khi đọc lại đơn](2026-10-18-19-order-timestamp-utc.md).
- 2026-10-18: [Thiết kế giao vật lý và thu COD](2026-10-18-20-physical-shipping-design.md).
- 2026-10-18: [Domain và database giao vật lý](2026-10-18-21-shipping-domain-database.md).
- 2026-10-18: [API giao vật lý và nhận COD](2026-10-18-22-shipping-api-cod.md).
- 2026-10-18: [Độ chính xác timestamp shipment](2026-10-18-23-shipment-timestamp-precision.md).
- 2026-10-18: [Thiết kế gia hạn có claim/xác minh](2026-10-18-24-manual-renewals-design.md).
- 2026-10-19: [Hoàn thiện domain/database gia hạn](2026-10-19-01-renewal-domain-database.md).
- 2026-10-19: [API gia hạn và kiểm chứng theo bước](2026-10-19-02-renewal-api-verification.md).
- 2026-10-19: [Regression và đồng bộ docs gia hạn](2026-10-19-03-renewal-regression-docs.md).
- 2026-10-19: [Thiết kế hoàn tiền và nhận hàng trả](2026-10-19-04-refunds-returns-design.md).

- 2026-10-18: [Lập kế hoạch nghiệm thu vận hành](2026-10-18-01-operations-plan.md).
- 2026-10-18: [Gate runtime và trusted proxy](2026-10-18-02-runtime-gates.md).
- 2026-10-18: [Native recovery và runner](2026-10-18-03-recovery-runner.md).
- 2026-10-18: [Tổng hợp kết quả và bước còn lại](2026-10-18-04-operations-results.md) — các commit `5692394`, `08ddfdd` và commit docs tổng hợp tra bằng `git log -- docs/history/`.

Mỗi bước ghi mục tiêu, thay đổi, quyết định, kiểm chứng và bước tiếp theo. Không đưa raw logs hoặc secrets vào lịch sử. Các mục ngày 2026-10-17 dưới đây là bản ghi hồi cứu cho đợt code đã triển khai trước khi áp dụng quy ước docs song song; số thứ tự là cách phân nhóm commit, không phải giờ thực hiện lịch sử được suy đoán.

| Bước | Bản ghi | Commit |
|---|---|---|
| 00 | [Thiết kế và quy trình tài liệu](2026-10-17-00-design.md) | `140580b` |
| 01 | [Dependency có advisory](2026-10-17-01-dependencies.md) | `3566f5c` |
| 02 | [Catalog, database, kho và media/outbox](2026-10-17-02-commerce.md) | `3fe7c13` |
| 03 | [Thông báo lỗi nghiệp vụ ở Production](2026-10-17-03-errors.md) | `e7ca07c` |
| 04 | [Runtime và gate Linux/container](2026-10-17-04-runtime.md) | `bf26d4e` |
| 05 | [Đóng gói template và loại test artifacts](2026-10-17-05-template.md) | `926cad9` |
| 06 | [Tổng hợp kiểm chứng và bàn giao](2026-10-17-06-verification.md) | Tra cứu bằng commit subject trong bản ghi |

Sau đợt này, tạo bản ghi ngay khi thực hiện từng bước và cập nhật feature plan trong cùng thay đổi. Không sửa mất kết quả cũ khi có lượt test mới; bổ sung kết quả mới cùng phạm vi source của nó.

Dùng `git show <hash>` để đọc thay đổi và `git log -- docs/history/` để tra commit của bản ghi tổng hợp cuối. Hash của commit 06 không tự ghi vào nội dung của chính nó.
