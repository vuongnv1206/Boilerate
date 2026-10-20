# Quy chuẩn nền giao diện quản trị

Ngày: 2026-10-19  
Khu vực: FE  
Hồ sơ: [FE-admin-ui-foundation](../features/admin-ui-foundation.md)

## Đã làm và lý do

- Tạo hồ sơ 5 step UI trước source; ưu tiên thống nhất giao diện Webflow CMS/Ecommerce, hoãn phiên/API và nghiệp vụ theo yêu cầu người dùng.
- Viết `Frontend/PRODUCT.md`, `DESIGN.md` và `AGENTS.md`: mục tiêu, demo boundary, layout/token/theme/motion, folder và tên component.
- Viết `Frontend/design/references/`, `THIRD_PARTY_NOTICES.md` và skill `Frontend/.agents/skills/admin-ui-design/SKILL.md` riêng cho Angular/PrimeNG.
- Đọc có chọn lọc tài liệu local get-design/Impeccable, giữ nguyên nguồn và không chạy script nguồn. Ghi commit/hash provenance; không giả định giấy phép khi không phát hiện license.
- Cập nhật `Frontend/README.md`, FE README và roadmap để không tiếp tục mô tả framework/UI là quyết định chưa chọn.

## Quyết định đáng chú ý

Webflow workspace là tham chiếu bố cục; GetDesign Webflow là phân tích marketing nên chỉ bổ sung ngôn ngữ màu/radius. Kích thước shell là baseline do dự án chọn, chưa phải số đo từ ảnh Webflow. System/Light/Dark và overlay theo root theme được quy định ngay đầu. Motion dùng phản hồi ngắn, có reduced motion; mốc này không thêm tính năng nghiệp vụ.

## Kiểm chứng thực tế

- Đã đọc workflow, template, roadmap, hồ sơ khung và nguồn tham chiếu chính chủ.
- Bundled `skill-creator/scripts/quick_validate.py` chạy trên skill mới trả `Skill is valid!`.
- Kiểm tra 16 tài liệu FE/source bằng Python: 0 đích liên kết tương đối bị thiếu sau sửa. `git -C Boilerate diff --check` exit 0 (có cảnh báo LF/CRLF). [Báo cáo kiểm chứng](../verification/2026-10-19-admin-ui-foundation.md).
- Chưa có kết quả build/browser tại thời điểm ghi bước 1. Không đánh dấu UI, API hoặc deployment đạt từ tài liệu này.

## Còn lại

Hoàn thành shell, tokens/preset, gallery, list/editor demo và motion trong các step 2–5. Ghi kiểm chứng thật trong báo cáo FE sau khi chạy; nghiệm thu người dùng và deployment giữ riêng. Chưa tạo commit.
