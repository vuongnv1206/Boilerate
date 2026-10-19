# 2026-10-19 — Quy tắc generic và tài liệu trong template

- Nguồn yêu cầu: bổ sung một file quy tắc dùng chung từ [tham khảo người dùng gửi](https://github.com/multica-ai/andrej-karpathy-skills/blob/main/CLAUDE.md); xem [hồ sơ](../features/documentation-structure.md).
- Thêm [CLAUDE.md](../../../CLAUDE.md) ở root với nội dung diễn đạt riêng theo bốn chủ đề: làm rõ kết quả, giải pháp vừa đủ, giới hạn phạm vi sửa và kiểm chứng kết quả. Không chứa stack, mô hình commerce, tên dự án hoặc đường dẫn nghiệp vụ; quy ước riêng vẫn ở AGENTS.
- AGENTS, root README và các index dẫn tới một file canonical, không sao chép rule sang BE/FE. Quy tắc cho phép tự xử lý lựa chọn thường lệ có thể đảo lại; chỉ hỏi khi quyết định còn thiếu làm thay đổi đáng kể kết quả.
- Template project thêm CLAUDE.md vào nội dung pack; runner template kiểm tra file rule và các điểm vào docs mới trong dự án sinh ra. Không đổi chức năng hoặc migration BE.
- File ảnh hưởng: CLAUDE.md, AGENTS.md, README.md, docs index/shared index, Boilerate.Template.csproj, scripts/verify-template.ps1 và hồ sơ/báo cáo này.
- Commit trước đã có: `a696182` — tách BE/FE/shared và quy trình tài liệu. Commit rule chỉ ghi hash khi đã tồn tại; tra history của bản ghi này.
- Kiểm chứng local: pack/sinh template cô lập đạt, 18/18 file bắt buộc và 67/67 tài liệu đã move có trong template/package; CLAUDE.md giữ nguyên byte, không thấy artifact bị cấm theo nhóm đã quét. Parse PowerShell runner, link check source/template và diff đạt; số liệu/phạm vi ở [báo cáo](../verification/2026-10-19-documentation.md). Không chạy full template build/test runner hoặc regression nghiệp vụ hai DB cho thay đổi này.
- Tiếp theo: đóng kiểm chứng docs/template; lựa chọn nền tảng và lát FE đầu tiên theo roadmap FE trong nhiệm vụ riêng.
