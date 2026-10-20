# Triển khai workspace và màn demo quản trị

Ngày: 2026-10-19  
Khu vực: FE  
Hồ sơ: [FE-admin-ui-foundation](../features/admin-ui-foundation.md)  
Bằng chứng: [Kiểm chứng UI foundation](../verification/2026-10-19-admin-ui-foundation.md)

## Đã triển khai

- Workspace Angular/PrimeNG có rail, context panel resize/collapse/lưu tùy chọn, topbar và toolbar; lazy routes cho list, editor và control gallery.
- Theme System/Light/Dark, semantic tokens/preset PrimeNG và font Inter tự host; motion ngắn và reduced-motion styles.
- Hai màn dữ liệu minh họa: tìm/lọc/chọn/phân trang, mở editor với inspector, validation, save trong memory và guard thay đổi chưa lưu.
- Launcher chọn Node phù hợp, npm 12.2.0 cho install, không thay Node 20 global.
- License runtime tùy chọn từ `PRIMEUI_LICENSE_KEY`, generator/hook chạy trước start/build; file generated bị ignore, `.env` không tự load.

## Kết quả thực tế và giới hạn

Npm 10 resolver thất bại, cài lại bằng npm 12.2.0 thành công 425 packages. Đã có production build exit 0 (508.87kB raw, 119.77kB transfer ở build đầu), lint toàn source pass sau sửa, unit 8/8 pass và render Dark trong IAB local. Source còn thay đổi; không dùng build đầu làm chứng nhận bản cuối. Hành trình browser rộng hơn và ảnh đối chiếu đang tiếp tục.

Cập nhật cùng ngày: lần unit test mới nhất sau bổ sung đạt 12/12. Kết quả 8/8 là lần ban đầu; hai lần không cộng dồn. Reviewer/browser interaction vẫn đang thực hiện, chưa nghiệm thu UI cuối.

Cập nhật bản cuối lúc **23:36:11 Asia/Saigon**: `.\run.ps1 check` exit 0 với lint pass, **18/18 tests/4 files** (theme 4, store 4, editor 7, shell 3), production build initial **511.88kB raw / 120.65kB transfer**; `.\run.ps1 format:check` exit 0. Lần 23:33 trước đó pass rồi chạy lại sau sửa focus shell (help đóng drawer/trap, kết thúc điều hướng mobile focus main). Giữ riêng các lần 8/12/18 tests. Các ca cuối bao phủ trim/whitespace, query-only dirty guard qua RouterTestingHarness và shell a11y/resize/persist trong jsdom. Một lỗi mock shell test do addListener/inert đã sửa trước lần pass cuối.

Browser trước các sửa cuối đã render desktop Dark 1280×720 và Light 1440×900, mở status overlay, search `den` trả 2 dòng, empty/reset, lọc Draft 5 dòng và chọn/clear 5 dòng; Draft editor autosave/restore và quay lại giữ filter. Published leave prompt đã hiện nhưng chưa hoàn tất lựa chọn trong dialog. Có ảnh `Frontend/artifacts/screenshots/desktop-filter-light.png`, không đại diện các chỉnh typography/accessibility cuối.

Người dùng sẽ bổ sung PrimeUI key sau. Giữ cảnh báo/watermark của vendor khi thiếu key; nghiệm thu license và giao diện sạch watermark còn chờ. Không thay BE/API/auth, không nghiệm thu nghiệp vụ hoặc deployment.

## Tiếp theo

Browser hiện tắt trong Work settings; người dùng nói sẽ bật nhưng chưa xác nhận bật lại. Còn review native bản cuối, mobile/gallery/overlay keyboard, reduced motion và ảnh desktop/editor/gallery/mobile; CSS mobile 40px/reduced motion có nhưng chưa browser verified. Step 2–4 đã triển khai nhưng chưa nghiệm thu cuối; kích thước chỉ là baseline dự án, chưa đo Webflow pixel-by-pixel. License key, review người dùng và deployment vẫn chờ. Chưa tạo commit.
