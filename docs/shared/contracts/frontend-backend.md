# Phối hợp và tích hợp BE–FE

Cập nhật: 2026-10-19. Tài liệu này hướng dẫn dùng hợp đồng hiện có; không bổ sung endpoint hoặc cam kết mọi luồng đã được nghiệm thu deployment.

## Nguồn chính thức

| Nội dung cần tra | Nguồn | Trách nhiệm |
|---|---|---|
| Mục tiêu và chính sách bán/giao | [Nghiệp vụ](../README.md) | BE và FE cùng cập nhật quyết định |
| Endpoint commerce, request/response, enum và quyền | [API BE](../../backend/commerce-api.md) | BE duy trì cùng code; FE dùng làm đầu vào tích hợp |
| Schema wire trên môi trường chạy | Swagger UI `/swagger`, JSON `/swagger/v1/swagger.json` khi Host ở Development | BE xuất từ code; đối chiếu cùng phiên bản source, không dùng snapshot không rõ ngày |
| Identity/phiên/quyền | [README dự án](../../../README.md), [TokensController](../../../src/Host/Controllers/Identity/TokensController.cs), [PersonalController](../../../src/Host/Controllers/Personal/PersonalController.cs) | BE xác thực/phân quyền; FE quản lý trải nghiệm phiên và hiển thị thao tác |
| Quan hệ và dữ liệu lưu trữ | [Database BE](../../backend/commerce-database.md) | BE sở hữu; FE thao tác thông qua API |
| Tiến độ và giới hạn | [Roadmap BE](../../backend/roadmap.md), [roadmap FE](../../frontend/roadmap.md), feature plans tương ứng | Mỗi bên giữ trạng thái và bằng chứng của mình |

Host hiện chỉ bật Swagger trong Development theo [Program.cs](../../../src/Host/Program.cs). Chưa xuất thêm SDK FE hay OpenAPI snapshot vào Git ở bước chuẩn hóa này. Môi trường tích hợp, URL gốc, origin/CORS/proxy và lưu token cần được ghi khi chọn nền tảng FE.

## Quy ước cho client

- **Schema sản phẩm:** ProductType quyết định bộ trường; Product có một ProductType, nhiều category qua `categoryIds`. Không suy ra field schema từ category. Options tạo lựa chọn SKU; custom fields mô tả sản phẩm. Dùng preview tổ hợp và các ID từ response theo API.
- **Tiền và kho:** hiển thị VND nguyên đồng. Tổng đơn/giá áp dụng/khả năng giữ hàng do BE kiểm tra; FE hiển thị báo giá và kết quả BE trả, không tự xác nhận còn hàng hoặc Paid. Kho vật lý và nội dung số dùng API riêng.
- **Version:** lấy version mới sau write, đúng chủ thể theo endpoint (Product, ProductType, InventoryItem, Order hoặc Renewal). Khi 409, tải lại trạng thái và giúp người dùng đối chiếu; không gửi ghi đè liên tục với version cũ.
- **Retry/idempotency:** chỉ endpoint có `operationId` mới dùng replay theo hợp đồng của endpoint đó. Một thao tác người dùng giữ cùng ID khi retry; nội dung khác phải là thao tác mới. Mất response không chứng minh thao tác thất bại; đọc trạng thái hoặc replay đúng ID trước khi làm lại.
- **Quyền:** UI có thể ẩn/khóa nút theo quyền hiện tại, nhưng BE luôn quyết định quyền/ownership. Quyền Product.Update không thay thế Price.Update, Product.Publish, Inventory.Adjust hoặc các quyền nghiệp vụ chuyên biệt.
- **Lỗi:** xử lý theo HTTP status và payload thật. Middleware nghiệp vụ dùng ProblemDetails, có `detail`, `code`, `traceId`, và `errors` khi validation; xem [implementation](../../../src/Infrastructure/Middleware/ExceptionMiddleware.cs). Không giả định mọi 401/403/validation do framework đều có đủ các extension đó. Không hiển thị stack trace hoặc raw server payload cho khách.
- **Thời gian và bí mật:** gửi timestamp UTC theo schema endpoint; đổi sang múi giờ hiển thị ở UI. Nội dung mã/tài khoản, target/evidence chỉ đọc qua endpoint có quyền; không lưu vào URL, analytics, log hoặc cache bền vững. Tôn trọng các response no-store.
- **Trạng thái:** Paid, giao số, shipment, gia hạn và hậu mãi có tiến độ riêng. PendingVerification cần xác minh kết quả lần trước; UI không cung cấp đường tắt “thực hiện lại”. Full refund/Closed không tự thu hồi mã đã giao hoặc chứng minh đã gia hạn thất bại.

## Bàn giao từng lát tính năng

1. Ghi một kịch bản và tiêu chí chấp nhận chung; liên kết feature plan BE/FE hoặc tạo hồ sơ điều phối trong `shared/features/` nếu thay đổi cả hai.
2. BE ghi hợp đồng, quyền, ví dụ thành công/lỗi, version/replay, dữ liệu demo và giới hạn. Nếu thiếu endpoint, cập nhật kế hoạch/hợp đồng cùng code trước khi coi là đã hỗ trợ.
3. FE ghi các trạng thái loading/empty/error/success, quyền, responsive và hành vi khi dữ liệu cũ hoặc mất kết nối. Nếu dùng mock, đánh dấu rõ còn bước nối API.
4. Chạy các ca liên quan trên BE thật; mỗi bên lưu bằng chứng của mình, hồ sơ điều phối liên kết kết quả cả hai. Kiểm thử BE đạt không thay thế kiểm thử màn hình hoặc deployment FE.
5. Thay đổi phá vỡ client phải nêu ảnh hưởng và thứ tự triển khai trong hồ sơ. Không giữ hai bản hợp đồng khác nhau trong docs BE và docs FE.
