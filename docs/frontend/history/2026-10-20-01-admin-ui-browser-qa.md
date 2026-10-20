# Kiểm chứng Chrome desktop và mobile nền UI

Ngày: 2026-10-20 (Asia/Saigon)  
Khu vực: FE  
Hồ sơ: [FE-admin-ui-foundation](../features/admin-ui-foundation.md)  
Bằng chứng: [Báo cáo kiểm chứng](../verification/2026-10-19-admin-ui-foundation.md)

## Điều kiện chạy lại

Sau người dùng Continue và restart, inventory thực tế xác nhận Chrome/IAB có sẵn, localhost hoạt động. Điều này gỡ lỗi permission của [phiên trước](2026-10-19-04-admin-ui-browser-qa.md); không xem lần báo bật setting ban đầu là đã có browser QA.

## Kết quả đã có

- Chrome desktop 1440×900: list/editor Light/Dark với nội dung tiếng Việt dài; document/body rộng đúng 1440, không overflow ngang trang. Có ảnh list, editor và datepicker/select/popover/dialog hai theme theo báo cáo.
- Resize bằng bàn phím tới 320px rồi reload giữ giá trị; collapse rồi reload giữ đóng/`aria-expanded=false`; mở lại và đưa về 240px. Dark filter Draft có 5 dòng, chọn tất cả 5 rồi clear hoạt động.
- Mobile 390×844: list/editor hai theme, document rộng 390px; bảng cuộn bên trong (viewport 336px, scroll width 1003px), ArrowRight làm scrollLeft tăng 40px.
- Navigation mobile đưa focus vào panel sau khi hiện; Escape trả focus nút mở. Inspector Light/Dark là modal, form phía sau inert, focus close và Escape trả về nút mở.
- Published editor đi qua mobile nav kích hoạt guard khi form dirty; chọn ở lại giữ nội dung, bỏ thay đổi ở Light về list. Gallery Dark drawer/dialog có tên/focus và Escape trả về trigger.
- Gallery Light drawer/dialog cũng pass focus/Escape; dialog có bounds 16–374px/rộng 358px tại viewport 390px. Mobile ở đây là Chrome viewport emulation, không phải thiết bị vật lý.
- Checkbox/switch toggle, gallery table select-all 6 dòng và pagination 4–6/6 pass. Keyboard Tab hiển thị tooltip tiếng Việt/Escape đóng; toast thấy ở mobile Light và desktop Dark. Loading spinner computed `spin`/`0.7s` xác nhận normal motion đang bật.
- Refresh list cuối: pagination 11–14/previous, `den` 1–2, empty/reset pass; System pressed=true sau reload; gallery tab mobile ArrowRight/Enter đúng; fresh console errors `[]`.

Các sửa hỗ trợ QA gồm focus sau visibility, đóng nav trước guard, inspector modal/inert/backdrop, gallery drawer/dialog focus, aria PrimeNG tiếng Việt, tooltip keyboard và motion drawer dùng token 240ms. Không đổi BE/API/auth.

## Kiểm tra bản cuối và hoàn tất local

Sau sửa cuối, lint/format exit 0; full suite **23/23 tests/5 files** lúc **00:06:54** (theme 4, store 4, editor 8, shell 4, focus helper 3). Production build exit 0 lúc **00:07:38 ngày 20/10** (`2026-10-19T17:07:38.979Z`), initial **514.39kB raw / 121.28kB transfer**. Đây là các lệnh riêng và full suite, không phải một lần `check` gộp sau sửa cuối.

Raw screenshots `final-*.jpg` được soát đúng định dạng: **38 JPEG** gồm 15 desktop 1440×900, 20 mobile 390×844 và 3 desktop datepicker/multiselect crop 1440×667. Không có mobile crop; inspector hai theme đã chụp lại khi mở thật. IAB PNG cũ giữ nguyên. Screenshot tool timeout CDP một lần rồi AX phục hồi, không phải lỗi app. Không có phép đo pixel Webflow.

Step 5 hoàn tất **phạm vi local QA**, chưa suy ra người dùng đã duyệt thiết kế hoặc có key license hợp lệ. Deployment là mốc riêng ngoài task UI này.

Người dùng yêu cầu bật animation và không cần giảm chuyển động: normal motion bật mặc định; giữ CSS prefers-reduced-motion như accessibility fallback. Không yêu cầu người dùng đổi OS và không coi OS-QA hoãn là blocker step 5. Tool hiện cũng không emulate OS preference này. Local QA không thay review thiết kế của người dùng, license key hoặc nghiệm thu deployment. Chưa tạo commit.
