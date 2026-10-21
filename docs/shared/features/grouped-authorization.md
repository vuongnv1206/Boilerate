# Quản trị phân quyền theo nhóm

Ngày bắt đầu: 2026-10-21. Trạng thái: Đã triển khai; chờ nghiệm thu triển khai.

Mục tiêu: quản trị nhóm chức năng, action, vai trò và gán vai trò cho tài khoản hiện có; FE dùng quyền Function–Action từ BE thay danh sách permission constants. Một ứng dụng, một cấp nhóm; nhóm chỉ tổ chức danh mục, không quyết định quyền. Không tạo/sửa hồ sơ hoặc bật/tắt tài khoản trong mốc này.

Hợp đồng BE/database: [grouped-authorization](../../backend/features/grouped-authorization.md). Giao diện và tiêu chí FE: [System administration](../../../../Boilerate.Frontend/docs/features/system-administration.md).

Thứ tự: BE/schema và migration hai provider → ngữ cảnh authorization và checker FE → màn quản trị theo ảnh tham chiếu → regression, browser và API thật trong môi trường cô lập. Giữ nguyên công việc Orders có sẵn. Không áp dụng migration lên database đang dùng.

Tiêu chí: grant cũ được giữ; API kiểm CentralAdmin và Function–Action; UI nhóm/chức năng/action hoàn toàn từ API; lọc/chọn nhanh không làm mất lựa chọn; lỗi giữ draft; không suy quyền View; kiểm riêng fixture và API/database thật.

Ngày 2026-10-21, người dùng chốt kiểm chứng runtime chỉ SQL Server; PostgreSQL giữ migration/source, chưa chạy. Bằng chứng tích hợp được giữ tại [FE verification](../../../../Boilerate.Frontend/docs/verification/2026-10-21-grouped-authorization.md).
