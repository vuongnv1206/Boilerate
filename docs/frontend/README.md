# Frontend — FE

Cập nhật: 2026-10-20. Đã triển khai và kiểm chứng local **nền UI quản trị Angular + PrimeNG** trong sibling `Frontend/`, theo workspace Webflow CMS/Ecommerce. Mốc này dùng dữ liệu minh họa; chưa kết nối API hoặc nghiệm thu nghiệp vụ. Người dùng review thiết kế, license key và deployment vẫn tách riêng. Xem [hồ sơ UI](features/admin-ui-foundation.md) và bằng chứng từng lần.

Mã nguồn giao diện đặt tại [Frontend/](../../../Frontend/README.md) khi mở workspace `BoilerateWorkspace`. Backend vẫn giữ ở `src/` và `tests/` trong dự án `Boilerate`; không tạo thêm thư mục `Backend/`.

FE hiện có repo private riêng [Boilerate.Frontend](https://github.com/vuongnv1206/Boilerate.Frontend). [Nền codebase](features/admin-codebase-foundation.md) bổ sung kiến trúc/runtime/HTTP/theme và CI; tài liệu kỹ thuật và kết quả sau refactor nằm trong [docs FE](../../../Frontend/docs/architecture.md), giữ khả năng build/CI độc lập với BE.

## Bắt đầu

1. Đọc [nghiệp vụ dùng chung](../shared/README.md) và [hướng dẫn tích hợp BE–FE](../shared/contracts/frontend-backend.md).
2. Đọc [roadmap FE](roadmap.md) và [hồ sơ UI hiện tại](features/admin-ui-foundation.md); feature tiếp theo dùng [mẫu chung](../shared/templates/feature-plan.md) trước khi code.
3. Dùng [API BE](../backend/commerce-api.md) và OpenAPI ở môi trường phát triển; liên kết feature BE tương ứng để biết giới hạn đã có.
4. Sau mỗi bước, ghi history tại `docs/frontend/history/`, liên kết từ index tại đó khi tạo bản ghi đầu; lưu báo cáo kiểm chứng trong `docs/frontend/verification/`.

## Định hướng quản trị sản phẩm

Webflow là tham khảo trải nghiệm quản trị: chủ shop tự tạo loại sản phẩm, trường nhập liệu, options và SKU. Form FE cần đọc schema từ BE để hiển thị trường theo loại sản phẩm; category là tập phân loại nhiều lựa chọn, độc lập với bộ trường. Không dựng form cố định riêng cho từng ngành hàng.

Thiết kế hiện tại ở [Frontend/DESIGN.md](../../../Frontend/DESIGN.md), với rail, panel ngữ cảnh resize/collapse, topbar mỏng, toolbar riêng và editor có inspector bên phải. [PRODUCT.md](../../../Frontend/PRODUCT.md) ghi phạm vi demo; [AGENTS.md](../../../Frontend/AGENTS.md) ghi folder/naming/code conventions. Trình kéo thả website và workflow builder chưa thuộc phạm vi.

Kiểm thử BE hiện có chỉ là đầu vào tích hợp. Chỉ ghi FE đã kiểm chứng khi có UI và kết quả chạy FE thực tế; mock phục vụ phát triển phải được đánh dấu và còn bước nối API thật.
