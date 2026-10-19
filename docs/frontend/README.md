# Frontend — FE

Cập nhật: 2026-10-19. **Chưa triển khai FE**; khu vực này chuẩn bị tài liệu để làm song song với BE. Chưa chọn framework, thư viện UI, thiết kế màn hình hoặc cách triển khai FE.

## Bắt đầu

1. Đọc [nghiệp vụ dùng chung](../shared/README.md) và [hướng dẫn tích hợp BE–FE](../shared/contracts/frontend-backend.md).
2. Chọn lát đầu trong [roadmap FE](roadmap.md); tạo `docs/frontend/features/<feature>.md` theo [mẫu chung](../shared/templates/feature-plan.md) trước khi code.
3. Dùng [API BE](../backend/commerce-api.md) và OpenAPI ở môi trường phát triển; liên kết feature BE tương ứng để biết giới hạn đã có.
4. Sau mỗi bước, ghi history tại `docs/frontend/history/`, liên kết từ index tại đó khi tạo bản ghi đầu; lưu báo cáo kiểm chứng trong `docs/frontend/verification/`.

## Định hướng quản trị sản phẩm

Webflow là tham khảo trải nghiệm quản trị: chủ shop tự tạo loại sản phẩm, trường nhập liệu, options và SKU. Form FE cần đọc schema từ BE để hiển thị trường theo loại sản phẩm; category là tập phân loại nhiều lựa chọn, độc lập với bộ trường. Không dựng form cố định riêng cho từng ngành hàng.

Các chi tiết về bố cục, thao tác, khả năng truy cập và responsive sẽ được chốt trong hồ sơ FE trước khi triển khai. Trình kéo thả website và workflow builder chưa thuộc phạm vi đã chốt.

Kiểm thử BE hiện có chỉ là đầu vào tích hợp. Chỉ ghi FE đã kiểm chứng khi có UI và kết quả chạy FE thực tế; mock phục vụ phát triển phải được đánh dấu và còn bước nối API thật.
