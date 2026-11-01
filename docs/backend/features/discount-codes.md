# Mã giảm giá — Backend

Ngày bắt đầu: 2026-10-30. Trạng thái: đã có source và đang kiểm chứng tích hợp. Phối hợp FE–BE tại [hồ sơ chung](../../shared/features/discount-codes.md); hợp đồng request/response và lỗi tại [API commerce](../commerce-api.md#mã-giảm-giá).

## Phạm vi và invariant

Một mã giảm phần trăm hoặc số tiền nguyên VND trên tiền hàng; shipping không giảm. Cart giữ mã và hiển thị lỗi khi điều kiện thay đổi. Backend tính lại tại checkout; giảm không làm tiền hàng bằng 0. Đơn lưu snapshot và phân bổ nguyên VND xác định theo dòng. Không seed hoặc thay grant hiện có.

Discount sở hữu code unique chuẩn hóa, điều kiện, hiệu lực UTC và version; Orders lưu snapshot, FK Restrict và cờ giữ lượt. Mỗi order giữ tối đa một lượt; số lượt là count các order đang giữ, được tuần tự hóa bằng `ordering` gate cùng checkout/cancel/expiry. Hủy/hết hạn chưa thanh toán nhả lượt. Receipt đến muộn vẫn được ghi; nếu lượt đã nhả thì thử giữ lại, nếu hết lượt để Paid/NeedsReview và chưa giao/gia hạn. Retry fulfillment phải giữ lại lượt trước khi giao. History refund không thay đổi snapshot.

## Bước triển khai và kiểm chứng

1. Domain, migration hai provider, constraint và API quản trị với ACL Discount.View/Create/Update.
2. Cart/checkout, snapshot, quota, cancel/expiry và thanh toán muộn trong transaction hiện có.
3. Kiểm thử domain, API, provider concurrency và browser live trên database riêng; không áp migration vào database ứng dụng trong quá trình phát triển.
4. Cập nhật hợp đồng, hướng dẫn rollout và kết quả thực chạy; PostgreSQL chỉ ghi nhận đạt khi chạy thực tế.

Ngày 2026-11-01, regression backend ngoài provider đạt 237/237; kiểm thử tích hợp Discount trên SQL Server đạt, gồm tranh chấp lượt cuối và thanh toán muộn. Bộ browser live trên SQL Server test cô lập đạt 96/96, trong đó luồng mã giảm giá chạy trên bốn cấu hình. Bộ live dùng bản frontend trước các chỉnh sửa trình bày cuối; chưa nghiệm thu staging hoặc PostgreSQL runtime. Không có code seeding mã giảm giá và không ghi đè quyền operator.
