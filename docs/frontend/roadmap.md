# Lộ trình frontend

Cập nhật: 2026-10-19. Đây là **thứ tự đề xuất để chuẩn bị FE**, chưa phải các bước đã code hoặc nghiệm thu. Lộ trình BE tiếp tục ở [roadmap riêng](../backend/roadmap.md).

| Bước | Đầu ra dự kiến | Điều kiện kiểm chứng | Trạng thái |
|---|---|---|---|
| 0. Chốt nền tảng FE | Phạm vi admin/storefront, framework, UI, cấu trúc source, URL môi trường và thiết kế ban đầu | Quyết định được ghi; có feature plan, lệnh dev/build và cách nối BE | Chưa bắt đầu |
| 1. Khung ứng dụng và phiên/quyền | Điều hướng, đăng nhập, phiên hết hạn, quyền thao tác và xử lý lỗi chung | Nối BE thật; kiểm tra thiếu quyền, mất phiên, lỗi tải và route trực tiếp | Chưa bắt đầu |
| 2. Quản trị Catalog linh hoạt | Loại/trường, category N–N, form schema, options/SKU, media, giá và publication | Tạo/sửa/xuất bản qua API; giữ đúng version, không mất form khi conflict; không bỏ qua quyền giá/publish/kho | Chưa bắt đầu |
| 3. Storefront và mua hàng | Danh sách/bộ lọc/chi tiết, giỏ chung, checkout, Đơn hàng của tôi | Chỉ hiện Published; tổng tiền/giữ hàng do BE xác nhận; COD đúng loại đơn, dữ liệu riêng đúng owner | Chưa bắt đầu |
| 4. Vận hành đơn | Nhận tiền, kho số/giao số, shipment/COD, gia hạn và hậu mãi | Phản ánh trạng thái riêng từng nghiệp vụ; thao tác cần quyền/version/replay; không lộ nội dung số | Chưa bắt đầu |
| 5. Nghiệm thu FE + BE | Luồng mua/quản trị thực, responsive, accessibility cơ bản và triển khai | Có báo cáo theo môi trường/API thật; lỗi và phần hoãn được ghi rõ | Chưa bắt đầu |

Mỗi bước có thể chia lát nhỏ theo [workflow chung](../shared/development-workflow.md). Chưa tạo trước hồ sơ “đã triển khai” hoặc số liệu kiểm thử cho các bước chưa chạy.

## Quyết định còn mở trước bước 0

- Ưu tiên màn hình admin nào và storefront có triển khai cùng đợt đầu không.
- Framework, thư viện UI/design tokens và cách tổ chức source FE trong repository.
- Môi trường BE để tích hợp, origin/proxy/CORS và chính sách lưu/làm mới phiên ở FE.
- Thiết kế màn hình và bộ dữ liệu demo không chứa nội dung tài khoản/mã thật.

Các quyết định này được xử lý trong nhiệm vụ FE tiếp theo; nhiệm vụ chuẩn hóa docs hiện tại không tự chọn thay.
