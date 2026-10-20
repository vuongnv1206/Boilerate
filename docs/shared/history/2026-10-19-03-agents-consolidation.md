# 2026-10-19 — Hợp nhất toàn bộ hướng dẫn vào AGENTS.md

- Yêu cầu bổ sung: dùng AGENTS.md cho toàn bộ rule, không còn file hướng dẫn riêng theo công cụ. [Kế hoạch](../features/documentation-structure.md) bổ sung bước 4 trước khi sửa.
- Gộp nguyên tắc chung và nguồn tham khảo từ file rule cũ vào AGENTS.md; giữ nguyên các quy ước phát triển/tài liệu của repo trong phần riêng. Xóa CLAUDE.md, cập nhật README/index, nội dung pack và danh sách file bắt buộc của runner template.
- Giữ các mô tả/kết quả kiểm chứng cũ như lịch sử tại thời điểm ghi; sửa liên kết tới file đã hợp nhất và dẫn bản ghi này để tránh link hỏng.
- File ảnh hưởng: AGENTS.md, CLAUDE.md (xóa), README.md, Boilerate.Template.csproj, scripts/verify-template.ps1, docs index/shared index và hồ sơ/history/verification liên quan. Không đổi code nghiệp vụ, API hoặc database.
- Kiểm chứng: đối chiếu nội dung nguyên tắc/quy ước với commit trước đạt; link check và parse runner đạt. Pack/install/generate cô lập đạt, 17/17 file bắt buộc có trong template, giữ nội dung AGENTS khi đổi tên; không còn CLAUDE.md trong package hoặc dự án sinh ra. Chi tiết/phạm vi ở [báo cáo](../verification/2026-10-19-documentation.md); không chạy lại build/test nghiệp vụ hai DB.
- Commit trước: `d972293` bổ sung rule riêng; bước này thay thế cách tổ chức đó theo yêu cầu mới. Không sửa lại bằng chứng cũ thành kết quả của AGENTS.md.
- Tiếp theo: commit thay đổi hợp nhất; FE/runtime tiếp tục theo roadmap riêng.
