# 2026-10-18 — Sản phẩm linh hoạt, đơn chung và trả trước hàng số

## Mục tiêu và quyết định

Điều chỉnh phỏng vấn theo chỉ dẫn mới: PC chỉ là ví dụ, mục tiêu là quản trị sản phẩm linh hoạt như Webflow. Không đưa nghiệp vụ PC/tồn linh kiện vào MVP chỉ vì câu trả lời trước chọn cấu hình tạo sẵn.

Người dùng đã chọn:

- A về flex: chủ shop tự tạo loại/trường/options/SKU và chọn cách bán/giao đã được hệ thống hỗ trợ.
- A về giỏ/đơn: vật lý + số chung giỏ/đơn, trả một lần; từng món giao riêng.
- A về thanh toán: đơn có hàng số trả trước toàn bộ, COD chỉ cho đơn thuần vật lý.

Phân tách bộ trường, lựa chọn bán và cách giao là hướng thiết kế đề xuất; chưa có schema/API mới được duyệt chi tiết. Chưa bổ sung rule/workflow builder tùy ý hoặc page builder. Không giả định mọi ví dụ hoặc cách giao bắt buộc chạy trong MVP.

## File liên quan

- [Phạm vi linh hoạt](../business/product-flexibility.md): quyết định, phần đã có và phần chưa triển khai, bước tiếp theo/ca nghiệm thu dự kiến và nguồn Webflow chính thức.
- [Feature plan](../features/business-discovery.md) và [roadmap](../roadmap.md): bỏ giả định PC bắt buộc, cập nhật hành trình mua/chính sách thu tiền.
- [Docs index](../README.md) và [history index](README.md): điều hướng.

## Kiểm chứng

Đã đối chiếu câu trả lời trực tiếp và tài liệu Webflow chính thức được liên kết trong hồ sơ phạm vi. `git diff --check` đạt; kiểm tra 36 file Markdown/141 relative links đạt. Không chạy build/test vì chỉ đổi docs; chưa kiểm thử thực thi các nghiệp vụ mới.

## Còn lại và bước tiếp

Chốt giữ hàng/cam kết hàng, thời hạn và thanh toán muộn; tiếp theo lỗi/hủy/hoàn từng phần, tài khoản/quyền, payment provider và phạm vi khả năng MVP. Chưa thiết kế bảng Order/Payment hoặc sửa Catalog trong vòng này.

Commit tài liệu liền trước đã tồn tại: `2cd1120`, ghi quy tắc cấp phát/gia hạn và các quyết định còn mở.
