# Tổng quan vận hành Admin

- Ngày bắt đầu: 2026-10-25
- Trạng thái: Đã triển khai; BE và rollout local đã kiểm chứng, browser/live cuối đợt theo báo cáo tích hợp
- Phạm vi: Một shop, VND, ngày báo cáo Asia/Ho_Chi_Minh; desktop/tablet, Light/Dark
- Hợp đồng: [Backend](../../backend/features/admin-overview.md); [Frontend](../../../../Frontend/docs/features/admin-overview.md)

## Mục tiêu và invariant

Chủ shop mở Admin để biết việc cần xử lý, tiền đã nhận/hoàn và SKU sắp hết. GET tổng quan chỉ đọc; không expire đơn, release claim hoặc cập nhật dữ liệu nghiệp vụ. Quyền từng phần được kiểm tại BE; FE không dùng số 0 thay cho dữ liệu bị cấm.

## Phần đã triển khai

1. Query tổng hợp BE và predicates queue dùng chung với danh sách Orders/Renewals; tiền dựa trên business timestamps.
2. FinancialSummary.View và operator filter --function; migration chỉ thêm index thời điểm nhận/hoàn tiền ở hai provider.
3. Màn Tổng quan, điều hướng và quyền OR, từng phần có trạng thái riêng; ngày/ngưỡng/trang trong URL.
4. Regression và integration SQL Server cô lập kiểm tra quyền, ranh giới ngày, tồn khả dụng, hàng đợi và GET chỉ đọc; ảnh desktop/tablet Light/Dark đã được review trước khi lưu baseline.
5. Backup/verify DB Development trước migration; đăng ký và cấp riêng FinancialSummary.View cho Admin. Giữ nguyên toàn bộ 55 dòng quyền cũ, không cấp capability khác.

## Quyết định đã chốt

- Tổng quan là trang đầu khi có Order.View, Renewal.View, Inventory.View, DigitalInventory.View hoặc FinancialSummary.View; tài khoản khác dùng fallback cũ.
- Finance có Hôm nay/7 ngày/30 ngày/Tùy chọn, mặc định 7 ngày, tối đa 90 ngày; ngày Việt Nam inclusive, backend dùng nửa khoảng UTC.
- Số tiền summary bảo toàn chính xác VND lớn vượt giới hạn số nguyên an toàn của JavaScript; hợp đồng wire và hiển thị thuộc tài liệu BE/FE.
- Ngưỡng kho chung 0–999, mặc định 5; ngưỡng 0 chỉ hết hàng. Physical tracked và Unique hữu hạn; Shared chỉ cảnh báo khi thiếu nội dung khả dụng.
- Finance được cấp ban đầu đúng cho Admin; grant khác giữ nguyên. StorefrontSettings không thuộc rollout này.
- PostgreSQL runtime, SMTP và staging chỉ được ghi nhận theo kết quả thực chạy; mobile tiếp tục hoãn.

## Kiểm chứng

Đã chạy kiểm chứng BE, FE check và review 12 ảnh Work/Finance/Stock tại 1440×900 và 1024×768, cả Light/Dark. Các trường hợp thu hồi quyền, tiền lớn vượt giới hạn JavaScript, hoàn khác kỳ, nhận tiền sau hủy, PendingVerification sau đóng và tồn khả dụng có regression. Kết quả browser/live cuối đợt, source/build, rollout và giới hạn được giữ tại [báo cáo tích hợp](../../../../Frontend/docs/verification/2026-10-26-admin-overview.md); raw artifacts bị ignore.

PostgreSQL đã có model/migration nhưng chưa có connection test và chưa chạy runtime cho mốc này. Local SQL Server không thay cho nghiệm thu staging; mobile và SMTP thật tiếp tục nằm ngoài phạm vi đã chốt.
