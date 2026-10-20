# FE — Tổ chức lại feature Angular

Ngày: 2026-10-20. Trạng thái: Source, local và CI web đã hoàn thành trong repo FE; mobile hoãn theo yêu cầu người dùng.

Người dùng yêu cầu thực hiện code, dùng cặp tên chức năng / chức năng-io cho danh sách / thêm-sửa; tách component/store/service và base HTTP. [Kế hoạch source FE](../../../../Frontend/docs/features/content-feature-architecture.md) sở hữu các quyết định, bước và kiểm chứng. Không đổi BE/API/database; UI Webflow giữ nguyên và dữ liệu vẫn mock. Lịch sử BE có sẵn không đưa vào commit FE.

[Quy tắc code](../../../../Frontend/docs/coding-rules.md) và [báo cáo kiểm chứng mới](../../../../Frontend/docs/verification/2026-10-20-content-architecture.md) nằm trong FE. Các tham chiếu BE, eco-fe và NanoFE được đánh giá trước khi chọn architecture; route thực đã dùng feature mới, không chỉ tạo folder/tài liệu. [CI web 39441412958](https://github.com/vuongnv1206/Boilerate.Frontend/actions/runs/39441412958) đạt source `9f6e371`; kết quả và phạm vi được sở hữu bởi báo cáo FE. Đã đọc [package seed 1.0.1](../../../../Frontend/docs/reference-audits/template-seeding-1.0.1.md) cho định hướng nối API thật/SQL Server; chưa seed hoặc thay đổi BE source.
