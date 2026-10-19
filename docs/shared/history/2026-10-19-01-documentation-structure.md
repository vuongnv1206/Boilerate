# 2026-10-19 — Tách tài liệu backend, frontend và dùng chung

- Mục tiêu: chuẩn bị phát triển FE song song, tách nguồn kỹ thuật BE khỏi nghiệp vụ và quy trình chung.
- Đọc index/roadmap/workflow và tạo [kế hoạch](../features/documentation-structure.md) trước khi di chuyển. Chuyển 67 file cũ bằng mapping từng file trong workspace; tính lại liên kết tương đối, giữ tên feature/history/verification và các kết quả/lệnh đã chạy trước đây.
- BE có index, roadmap, database/API/runbook, feature plans/history/verification riêng trong `docs/backend/`. Nghiệp vụ/discovery/workflow/template vào `docs/shared/`.
- Thêm index/roadmap FE ở trạng thái chưa triển khai và [hướng dẫn tích hợp](../contracts/frontend-backend.md) trỏ tới API, auth/ACL, version/replay, lỗi và trạng thái thực tế của BE. Chưa chọn stack FE hoặc thêm UI.
- Cập nhật root README, plan, AGENTS, index BE và quy trình/mẫu dùng cho cả BE/FE/shared. Hướng dẫn tra lịch sử file trước khi move dùng `git log --follow`.
- File ảnh hưởng: các Markdown được di chuyển, các index/lộ trình/quy trình mới và root README/plan/AGENTS; không đổi code nghiệp vụ/API/database.
- Kiểm chứng: đích move 67/67 tồn tại; 46 bản ghi cũ ngoài index giữ nguyên nội dung trừ đích liên kết. Kiểm tra liên kết và diff được ghi tại [báo cáo](../verification/2026-10-19-documentation.md); không chạy lại regression BE hoặc nghiệm thu FE/deployment.
- Còn lại/tiếp theo: thêm file quy tắc generic ở root, đóng gói và sinh template để kiểm tra tài liệu/quy tắc đi cùng dự án mới. FE và nghiệm thu deployment theo roadmap riêng.
