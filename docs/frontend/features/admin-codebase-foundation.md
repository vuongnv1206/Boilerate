# FE-codebase — Nền codebase Admin

Ngày bắt đầu: `2026-10-20`  
Trạng thái: `Hoàn tất nền codebase local/CI`  
Khu vực phụ trách: `FE`  
Roadmap: [Frontend](../roadmap.md)

## Mục tiêu và phạm vi

Xây nền kỹ thuật cho admin Angular hiện tại: repository riêng, ranh giới feature/layout, cấu hình runtime, HTTP/error/form primitives, theme và kiểm tra tự động. Giữ UI Webflow CMS/Ecommerce, System/Light/Dark, normal motion và dữ liệu demo. Không đổi API/database BE; chưa triển khai auth/session/access, nghiệp vụ thật, storefront hoặc template phát hành.

Người dùng đã duyệt repo GitHub **private** `vuongnv1206/Boilerate.Frontend`, nhánh `main`, local sibling `Frontend/`, commit nhỏ và push. Giữ nguyên repo/lịch sử BE. Tài liệu kỹ thuật được đặt trong repo FE; hồ sơ này điều phối tiến độ và dẫn bằng chứng, không sao chép hợp đồng [BE–FE](../../shared/contracts/frontend-backend.md).

## Quyết định và các bước

| Bước | Đầu ra và điều kiện hoàn tất | Trạng thái |
|---|---|---|
| 0 Git/baseline | Private remote, local main, source nhập theo nhóm; không key/artifacts; baseline riêng không làm mất working files | Hoàn tất, đã push baseline và code |
| 1 Ranh giới | Shell nhận navigation qua DI; lazy feature routes/public entries; lint chặn dependency sai | Hoàn tất local, 17 probes pass |
| 2 Nền ứng dụng | Một runtime config có validation; functional HTTP/error; pagination theo BE; typed form errors; tests mock | Hoàn tất local |
| 3 Theme/shared UI | Semantic CSS variables là nguồn chung; preset mapping; SCSS có phạm vi; reusable presentation có nhu cầu thật | Hoàn tất local và browser regression |
| 4 CI/nghiệm thu | Format/lint/unit/build; Chromium desktop/tablet/mobile, light/dark, overlays; CI độc lập; source push | Hoàn tất local và CI thực tế |

## Tiêu chí chấp nhận

- Source FE có repo private riêng và các commit có ý nghĩa; không thay lịch sử BE.
- Core/layout/shared không import feature internals; alias và relative imports được kiểm tra.
- Config sai báo rõ trước bootstrap; HTTP normalizer chấp nhận payload không tin cậy; không retry mutation/toast tự động.
- UI demo không gọi BE, không lưu dữ liệu nghiệp vụ bền vững; menu/count và dirty guard vẫn hoạt động.
- Theme, overlay, focus và motion không hồi quy; test trên 1440×900, 1024×768, 390×844.
- Kết quả local, CI thực tế, thiết kế được người dùng duyệt và deployment được ghi riêng.

## Nhật ký triển khai

### 2026-10-20 — Bắt đầu

- Đã khảo sát source FE/BE, đọc quy chuẩn và duyệt kế hoạch với người dùng.
- Phát hiện `.env.example` có license key; sẽ bảo toàn trong cấu hình local ignored và thay example bằng placeholder trước commit đầu. Không ghi giá trị vào tài liệu/log.
- Kiểm chứng mới: chưa chạy; kết quả UI cũ không dùng làm bằng chứng sau refactor.
- Tiếp theo: nhập baseline, tách boundaries rồi chia luồng HTTP và theme.

## Kiểm chứng và công việc còn lại

Kết quả và commit thực tế có một nguồn tại [báo cáo repo FE](../../../../Frontend/docs/verification/2026-10-20-codebase.md), [history](../../../../Frontend/docs/history/README.md) và [kiến trúc](../../../../Frontend/docs/architecture.md). Đã có 93 tests/13 files, 17 architecture probes, production build và 24 browser tests pass. GitHub private remote đã tạo/push; [CI run 39417291346](https://github.com/vuongnv1206/Boilerate.Frontend/actions/runs/39417291346) source `dd7fd71` hoàn tất success lúc 18:50:11 ngày 20/10/2026. Bảy snapshot lịch sử build độc lập đạt. Nghiệm thu API, auth, license, user design approval và deployment nằm ngoài mốc này.
