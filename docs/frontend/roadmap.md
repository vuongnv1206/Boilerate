# Lộ trình frontend

Cập nhật: 2026-10-20. Angular + PrimeNG và hướng workspace Webflow CMS/Ecommerce đã được người dùng chọn. **Ưu tiên thống nhất UI trước tính năng**. Source trong sibling `Frontend/`; BE tiếp tục ở [roadmap riêng](../backend/roadmap.md).

## Mốc hiện tại — nền giao diện quản trị

Hồ sơ và tiêu chí chấp nhận: [admin-ui-foundation](features/admin-ui-foundation.md). Tiến độ triển khai không đồng nghĩa kiểm chứng browser hoặc nghiệm thu người dùng/deployment.

| Step | Đầu ra | Điều kiện kiểm chứng | Trạng thái |
|---|---|---|---|
| 1. Tham chiếu và quy chuẩn | DESIGN, PRODUCT, quy tắc folder/naming, skill và provenance | Tài liệu có nguồn, không gọi baseline là số đo Webflow | Đã viết; kiểm tra tài liệu local |
| 2. Workspace | Rail, context panel resize/collapse/persist, topbar, toolbar, inspector | Browser: điều hướng, resize, reload và màn nhỏ | Hoàn tất triển khai/local QA; chờ review thiết kế |
| 3. Theme và control | Tokens, preset PrimeNG, System/Light/Dark và gallery trạng thái | Hai theme, overlay, keyboard, System/reload | Hoàn tất local QA; license bổ sung sau |
| 4. List và editor | Hai màn mẫu tương tác, search/filter/selection/pagination/save demo | Hành trình dữ liệu minh họa; không API | Hoàn tất triển khai/local QA |
| 5. Motion và kiểm chứng UI | Normal motion, build, browser QA và ảnh bản dựng | Bằng chứng thực tế; giữ CSS fallback reduced motion | Hoàn tất local QA; chưa thay duyệt thiết kế/license/deployment |

## Nền codebase Admin — 20/10/2026

[Hồ sơ điều phối](features/admin-codebase-foundation.md): FE có repo private riêng [Boilerate.Frontend](https://github.com/vuongnv1206/Boilerate.Frontend), nhánh main. Đã tách navigation DI/lazy feature boundaries, thêm runtime/HTTP/form primitives và hợp nhất theme/shared presentation. UI vẫn dùng demo, animation bật; chưa API/auth.

Local đã đạt format/lint, 17 architecture probes, 93 tests/13 files, production build và 24 Chromium browser tests trên desktop/tablet/mobile × light/dark. [Báo cáo FE](../../../Frontend/docs/verification/2026-10-20-codebase.md) là nguồn kết quả sau refactor; CI chỉ ghi đạt khi có run thực tế. [Kiến trúc](../../../Frontend/docs/architecture.md) và history kỹ thuật nằm trong repo FE để checkout độc lập.

Mốc codebase hoàn tất local/CI: [run 39417291346](https://github.com/vuongnv1206/Boilerate.Frontend/actions/runs/39417291346) kiểm source `dd7fd71`, mọi step pass, gồm 24/24 browser tests; kết thúc 18:50:11 ngày 20/10/2026. API/auth và nghiệm thu deployment vẫn thuộc mốc sau.

Đợt tiếp theo tổ chức lại **source feature thực** theo yêu cầu `content` / `content-io`, page/store/service, enum/constants và HTTP base: [hồ sơ điều phối](features/content-feature-architecture.md). [Báo cáo mới trong repo FE](../../../Frontend/docs/verification/2026-10-20-content-architecture.md) sở hữu kết quả local/CI; không dùng CI baseline nêu trên thay cho kiểm chứng source mới. [Coding rules](../../../Frontend/docs/coding-rules.md) áp dụng cho các màn tiếp theo.

## Các mốc sau nền UI

Các dòng sau là định hướng; chưa có feature plan hoặc lịch triển khai chi tiết. Sau khi thống nhất nền UI mới chốt từng lát, phiên/quyền và tích hợp.

| Mốc | Kết quả dự kiến | Điều kiện đóng |
|---|---|---|
| Phiên và quyền | Đăng nhập, phục hồi/refresh/logout, route/action theo grants | BE thật; nhiều tab, mất phiên, thiếu quyền |
| Catalog quản trị | Loại/trường, category N–N, form schema, options/SKU, ảnh, giá và publication | API thật, validation/version/conflict, quyền từng thao tác |
| Vận hành quản trị | Nhân viên/quyền, đơn hàng, nhận tiền, kho/giao hàng và hậu mãi theo lát được duyệt | Hợp đồng BE, dữ liệu riêng, replay/concurrency phù hợp |
| Storefront | Danh sách/chi tiết, giỏ chung, checkout và Đơn hàng của tôi | Published, tổng tiền/tồn do BE xác nhận, owner đúng |
| Nghiệm thu tích hợp/deployment | Luồng người dùng thật, responsive, accessibility và hosting | Báo cáo môi trường/API thật; phần hoãn và lỗi được ghi |

## Quyết định đã chốt và còn mở

- Đã chốt: admin trước; Angular mới nhất đã kiểm tra tại triển khai; PrimeNG được tùy biến; tiếng Việt; compact desktop; System/Light/Dark; motion theo trạng thái; demo UI không API.
- Đã chốt: source ở sibling `Frontend/`; tài liệu tiến độ FE ở khu vực này; chuẩn thiết kế và skill ở Frontend.
- Còn mở ở mốc sau: hợp đồng phiên trình duyệt, môi trường BE/origin, chính sách tích hợp và rollout production. Không tự triển khai các mục đó trong mốc UI.
- Bố cục/token ở DESIGN là baseline có thể tinh chỉnh qua review; chưa có nghiệm thu pixel-perfect từ ảnh Webflow đo trực tiếp.
- PrimeUI license key được người dùng bổ sung sau; đã có cấu hình runtime tùy chọn, vẫn giữ cảnh báo/watermark của vendor khi thiếu key. Nghiệm thu license còn chờ.
- Baseline UI trước mốc codebase ngày 20/10: lint/format exit 0, full suite **23/23 tests/5 files**, production build lúc **00:07:38** exit 0 (514.39kB raw/121.28kB transfer), chạy các lệnh riêng. Chrome desktop/mobile viewport QA và **38 JPEG** đã hoàn tất local step 5. Người dùng chọn normal animation bật; giữ CSS fallback reduced motion, OS-QA hoãn và không chặn. Còn duyệt thiết kế, license và mốc deployment riêng. Xem [báo cáo cũ](verification/2026-10-19-admin-ui-foundation.md); kết quả codebase mới ở mục riêng phía trên.

[Lịch sử FE](history/README.md) giữ kết quả từng lần thực hiện; [workflow chung](../shared/development-workflow.md) áp dụng cho các tính năng tiếp theo.
