# Kiểm chứng nền UI quản trị

Ngày: 2026-10-19 (Asia/Saigon)  
Cập nhật: 2026-10-20 (Asia/Saigon)  
Hồ sơ: [FE-admin-ui-foundation](../features/admin-ui-foundation.md)  
Phạm vi: đã triển khai và kiểm chứng local nền UI theo step 1–5. Duyệt thiết kế với người dùng, license key và deployment vẫn là các trạng thái nghiệm thu riêng còn chờ.

## Môi trường và nguồn

Workspace Windows `D:\MyCode\BoilerateWorkspace`; source FE ở sibling `Frontend/`. Đọc workflow/template/roadmap và thư viện local get-design có Git HEAD `f3c8f6c841f795b32eb369649ad9ec172626edab`. Không thay đổi nguồn tham chiếu hoặc chạy scripts của get-design/Impeccable.

## Các lần kiểm tra thực tế

| Kiểm tra | Lệnh/thao tác | Kết quả |
|---|---|---|
| Skill format/frontmatter | `python C:\Users\admin\.codex\skills\.system\skill-creator\scripts\quick_validate.py D:\MyCode\BoilerateWorkspace\Frontend\.agents\skills\admin-ui-design` | Pass: `Skill is valid!` |
| Đọc source và provenance | Đọc Webflow local DESIGN, selected Impeccable refs, `skills-lock.json`, `git remote -v`, `git rev-parse HEAD` | Có nguồn/phiên bản; không copy hoặc chạy nguồn |
| Kiểm tra liên kết Markdown lần đầu | Python duyệt link tương đối của 15 file FE/docs | Phát hiện link thư mục verification chưa tồn tại; tạo báo cáo thực tế này để hoàn thiện đích liên kết |
| Kiểm tra liên kết Markdown sau sửa | Python duyệt link tương đối của 16 file FE/docs | Pass: 0 đích tương đối bị thiếu |
| Kiểm tra tài liệu sau cập nhật bước 2–4/license | Python duyệt link tương đối của 17 file FE/docs; `git -C Boilerate diff --check` | Pass: 0 đích thiếu; whitespace diff exit 0, chỉ cảnh báo LF/CRLF |
| Soát tài liệu cuối sau check 23:34 | Python kiểm tra 17 file, screenshot path, `git diff --check` và skill validator | Pass: 0 đích tương đối thiếu; screenshot tồn tại; diff exit 0 (LF/CRLF warnings); `Skill is valid!` |
| Soát tài liệu sau local QA ngày 20/10 | Python duyệt 19 Markdown, các link ảnh JPG và `git -C Boilerate diff --check` | Pass: 0 đích tương đối thiếu; diff exit 0, chỉ LF/CRLF warnings; PIL xác nhận 38 JPEG đúng định dạng |
| Whitespace diff Backend repo | `git -C Boilerate diff --check` | Exit 0; Git chỉ cảnh báo chuyển LF/CRLF trên các file đã đổi |
| Contrast token pairs | Python tính WCAG relative luminance từ các giá trị hex đang triển khai | Light accent/white 4.59:1, muted/white 4.70:1, field-border/white 3.09:1; Dark accent/on-accent 7.29:1, muted/surface 5.28:1, field-border/surface 3.30:1 |

Các tỷ lệ trên là kiểm tra cặp token, chưa thay thế kiểm tra trạng thái hover/focus/disabled hoặc ảnh runtime.

## Môi trường cài đặt trong quá trình triển khai

Runtime bundled được xác nhận Node `24.19.0`; Angular/core/CLI `22.2.3`, PrimeNG `22.1.2`. Node 20 global giữ nguyên. Lần cài bằng npm 10 gặp lỗi peer resolver; lần thử lại bằng npm `12.2.0` cài thành công 425 packages theo kết quả của tiến trình triển khai. `Frontend/run.ps1` hỗ trợ chọn runtime tương thích và cài bằng npm 12.2.0. Cài dependency thành công chưa chứng minh build hoặc UI chạy.

## Các kết quả ứng dụng đã có

Các lần đầu dưới đây được ghi riêng để giữ lịch sử; lần `check` gộp sau các sửa cuối nằm ở phần tiếp theo. Kết quả do tiến trình triển khai chính ghi nhận và chuyển vào báo cáo.

| Lần kiểm tra | Phạm vi | Kết quả thực tế |
|---|---|---|
| Production build đầu tiên | Angular app, cấu hình production | Exit 0; initial bundle 508.87kB raw, 119.77kB estimated transfer. Chưa dùng số liệu này cho các build về sau |
| Lint sau sửa | Toàn bộ `src` | Pass theo lần chạy triển khai; không có lint error còn lại ở lần này |
| Unit test ban đầu | Theme controller, demo store, item editor | 8/8 pass ở lần đầu |
| Unit test bổ sung giữa quá trình | Bộ unit tests sau các ca kiểm tra mới | 12/12 pass ở lần này; không cộng dồn với 8 tests lần đầu hoặc 18 tests bản cuối |
| IAB local | App ở localhost, theme Dark | Render thành công; chưa đủ để đóng Light/overlay/responsive/keyboard |

Lệnh tái chạy từ `Frontend/`: `.\run.ps1 lint`, `.\run.ps1 test`, `.\run.ps1 build`, `.\run.ps1 start`. Launcher chọn runtime tương thích. Không nêu version source/commit giả: mốc này chưa tạo commit.

## Kiểm tra source ngày 19/10 lúc 23:36

Thời điểm kết thúc lần này: **2026-10-19 23:36:11 Asia/Saigon** (`2026-10-19T16:36:11Z`). Chạy từ `Frontend/` bằng Node 24.19.0 qua launcher, sau sửa focus ở shell. Lần 23:33 trước đó cũng pass, được chạy lại vì có thay đổi source. Kết quả mới hơn sau QA ngày 20/10 nằm ở phần tiếp theo.

| Lệnh | Kết quả | Phạm vi |
|---|---|---|
| `.\run.ps1 check` | Exit 0 | Lint pass; 18/18 unit tests trong 4 files; production build pass |
| `.\run.ps1 format:check` | Pass | Source và cấu hình thuộc lệnh format check |
| Production build trong check | Initial bundle 511.88kB raw, 120.65kB estimated transfer | Số liệu lần mới nhất; lần 23:33 trước đó là 120.58kB estimated transfer, không dùng cho bản cuối |

Phân bố unit tests: theme controller **4**, demo store **4**, item editor **7**, admin shell **3**. Các ca bổ sung kiểm tra tên toàn khoảng trắng bị chặn, trim giá trị; điều hướng chỉ đổi query với guard thật qua `RouterTestingHarness`; mobile navigation `aria-modal`/`inert`, Escape và focus restoration; resize được clamp, preference phục hồi khi tạo lại shell; skip link chuyển focus. Các ca mobile/focus này chạy trong **jsdom**, không thay thế browser mobile.

Một lần shell test trước đó thất bại do mock jsdom thiếu `addListener` và cách phản ánh thuộc tính `inert`; mock/cách kiểm tra được sửa và lần 18 tests cuối pass. Không gộp lần thất bại thành pass hoặc suy ra browser native đã được xác nhận.

Sửa shell cuối: mở trợ giúp đóng drawer/trap đang mở; khi kết thúc điều hướng mobile, focus chuyển về main. Lần check mới nhất pass sau các sửa này; chưa có fresh browser confirmation. Dev server từng báo lỗi metadata HMR tạm thời khi đổi imports; build sau full reload phục hồi, output chỉ còn license warning theo tiến trình triển khai. Điều đó chưa thay thế browser QA bản cuối.

## Hành trình browser thực tế và ảnh

Các thao tác dưới đây chạy trong IAB local, **trước các sửa cuối về typography/accessibility**. Dark được quan sát ở viewport ban đầu **1280×720**; các thao tác Light và ảnh đã lưu dùng **1440×900**. Chưa hoàn tất đối chiếu hai theme cùng viewport. Chúng xác nhận phiên UI tại thời điểm thao tác; ảnh không chứng minh giao diện sau các sửa cuối.

| Thao tác | Kết quả quan sát |
|---|---|
| Mở list trong Light và Dark | Cả hai render được trên desktop |
| Mở dropdown trạng thái ở Light | Overlay mở; có [ảnh thực tế](../../../../Frontend/artifacts/screenshots/desktop-filter-light.png) |
| Search `den` | Trả 2 dòng, hỗ trợ tìm tiếng Việt không dấu |
| Query không có kết quả rồi reset | Hiện empty state, reset trở lại danh sách |
| Lọc Draft rồi chọn tất cả | 5 dòng Draft, checkbox tất cả chọn 5 dòng; clear selection hoạt động |
| Draft editor | Đổi tên, quan sát autosave và khôi phục tên; quay lại giữ filter |
| Published editor | Prompt rời màn có thay đổi chưa lưu được hiển thị; **chưa hoàn tất quyết định trong dialog trên browser** |

Ảnh raw ở `Frontend/artifacts/screenshots/desktop-filter-light.png` (đã kiểm tra file tồn tại; thư mục artifacts bị ignore). Không có ảnh mobile/gallery hoặc bộ ảnh cuối đầy đủ ở thời điểm này. Không có phép đo pixel chính xác từ screenshot Webflow; ảnh bản dựng chỉ là bằng chứng UI cục bộ.

Sau đó Browser bị tắt trong Work settings. Người dùng nói sẽ bật, nhưng chưa có bằng chứng đã bật lại; không thể tiếp tục browser review ở phiên kiểm tra cuối. CSS reduced motion và hit target mobile 40px đã triển khai; **chưa được kiểm tra runtime native**. Không dùng các unit tests jsdom hoặc build để đóng khoảng trống này.

Dev server do tiến trình triển khai mở đã được dừng sau kiểm tra; dùng `Frontend/run.ps1 start` để chạy lại. Lần check 23:36 chạy sau bổ sung khôi phục focus khi đóng help từ điều hướng mobile. Không có bằng chứng browser mới sau sửa này.

## Quyền browser và phiên QA tiếp tục

Khoảng **23:39 ngày 19/10/2026 Asia/Saigon**, người dùng báo đã bật Browser. Công cụ lúc đó vẫn trả `Local browser use is disabled in Work settings`: AX tab hiện tại, `getState`, rồi reset kernel và `getState` mới đều có inventory rỗng. Không có QA/ảnh mới ở giai đoạn này.

Sau người dùng Continue và restart, inventory xác nhận **Chrome browser ID 3 và IAB browser ID 2** có sẵn (đây là browser IDs, không phải số tab; IAB lúc đó có 0 tab). Chrome mở localhost hoạt động. Từ thời điểm xác nhận bằng công cụ này mới tiếp tục QA thực tế, kéo sang ngày **20/10/2026 Asia/Saigon**. [Lịch sử phiên mới](../history/2026-10-20-01-admin-ui-browser-qa.md).

Tiêu chí phân biệt: local browser QA xác nhận UI ở môi trường thử; review thiết kế với người dùng và kiểm tra license key vẫn là các phần nghiệm thu riêng. [Lịch sử phiên QA tiếp tục](../history/2026-10-19-04-admin-ui-browser-qa.md).

## Chrome QA ngày 20/10 — kết quả đã xác nhận

Source được sửa theo phát hiện thực tế rồi thao tác lại. Kết quả browser dưới đây và các lệnh source cuối được ghi riêng; không dùng kết quả build cũ để thay cho bản sau sửa.

| Môi trường/luồng | Kết quả thực tế |
|---|---|
| Desktop 1440×900 Light/Dark | List/editor với tiếng Việt dài render; body/document rộng 1440px, không overflow ngang trang; ảnh hai theme cùng viewport |
| Theme/overlays desktop | Datepicker/select/popover/dialog được mở ở Light/Dark; có ảnh thực tế; Dark status chọn bằng Space hoạt động |
| Resize/collapse/reload | Keyboard End đưa panel tới 320px, reload giữ; collapse/reload giữ đóng và aria-expanded=false; mở lại và reset 240px |
| List Dark | Filter Draft 5 dòng; chọn tất cả 5 và clear selection hoạt động |
| Mobile 390×844 hai theme | List/editor document rộng 390px; bảng cuộn bên trong 336px/1003px; ArrowRight tăng scrollLeft 40px |
| Navigation mobile | Focus sau panel hiện; Escape đóng và focus về nút mở |
| Inspector mobile hai theme | Role modal và form inert; focus close; Escape trả focus nút mở |
| Published form → mobile navigation | Guard dialog có focus bên trong, main không còn inert sau nav đóng; Stay giữ input; Discard ở Light về list |
| Gallery mobile Dark | Drawer role dialog/tên “Tùy chọn hiển thị”, focus `gallery-drawer-name`; Escape về drawer trigger. Dialog Escape về dialog trigger |
| Gallery mobile Light | Drawer/dialog focus và Escape về trigger pass; dialog trong viewport 390px có left 16px/right 374px/width 358px |
| Control interaction | Checkbox và switch đổi trạng thái; gallery table chọn đủ 6 dòng và chuyển trang hiển thị 4–6/6 |
| Tooltip/notification | Tab hiển thị tooltip tiếng Việt, Escape đóng; toast hiển thị ở mobile Light và desktop Dark |
| Normal animation | Loading spinner có computed animation name `spin`, duration `0.7s`; animation bình thường đang bật |
| List sau refresh cuối | Pagination chuyển tới 11–14 rồi previous; search `den` hiển thị 1–2; empty heading/reset hoạt động |
| System theme | Nút System có pressed=true sau reload; theme mode được phục hồi |
| Gallery tabs mobile | ArrowRight hai lần rồi Enter kích hoạt tab phản hồi |
| Console sau reload sạch | Error logs `[]`; warning/license vendor khi thiếu key vẫn giữ |

Sửa theo QA: helper focus đợi visibility qua frame (bounded 60 frames); đóng nav trên NavigationStart trước guard; inspector focus trap/backdrop/inert/Escape; drawer/dialog gallery có tên, close button và return focus; PrimeNG aria translation tiếng Việt; tooltip hỗ trợ bàn phím hai theme; drawer motion dùng token 240ms; icon mobile có aria-label. Những sửa này không thay hợp đồng/API/auth.

Ảnh trong `Frontend/artifacts/screenshots/`: [desktop list Light](../../../../Frontend/artifacts/screenshots/final-desktop-list-light.jpg), [Dark](../../../../Frontend/artifacts/screenshots/final-desktop-list-dark.jpg), [editor Light](../../../../Frontend/artifacts/screenshots/final-desktop-editor-light.jpg), [Dark](../../../../Frontend/artifacts/screenshots/final-desktop-editor-dark.jpg), [mobile list Light](../../../../Frontend/artifacts/screenshots/final-mobile-list-light.jpg), [Dark](../../../../Frontend/artifacts/screenshots/final-mobile-list-dark.jpg), [inspector Light](../../../../Frontend/artifacts/screenshots/final-mobile-inspector-light.jpg), [Dark](../../../../Frontend/artifacts/screenshots/final-mobile-inspector-dark.jpg). Các ảnh overlay/date picker/dialog/drawer khác dùng cùng tiền tố `final-*`. Screenshot tool trả JPEG bytes nên file `final-*` dùng `.jpg`; ảnh IAB cũ `desktop-filter-light.png` giữ nguyên. Không có phép đo screenshot Webflow; không chứng nhận pixel-perfect.

Ảnh Mobile Light mới: [gallery](../../../../Frontend/artifacts/screenshots/final-mobile-gallery-light.jpg), [drawer](../../../../Frontend/artifacts/screenshots/final-mobile-drawer-light.jpg), [dialog](../../../../Frontend/artifacts/screenshots/final-mobile-dialog-light.jpg). Các ca mobile dùng **Chrome viewport emulation 390×844**, không phải thiết bị vật lý.

Đã sửa nhãn checkbox header gallery bằng external label gắn inputId sau khi phát hiện nhãn ban đầu của vendor không đủ; bộ source cuối được kiểm tra bên dưới. Người dùng ngày 20/10 yêu cầu **bật animation, không cần giảm chuyển động**: normal motion bật mặc định, không có yêu cầu đổi OS hoặc test reduced-motion OS để đóng step 5. Fallback `prefers-reduced-motion` vẫn giữ trong CSS vì accessibility. Tool không hỗ trợ emulate OS reduced motion; phần OS-QA được hoãn, không ghi fail hoặc blocker. Một trả lời bất đồng bộ nói sẽ bật preference nhưng chưa xác nhận active; lần đo cuối vẫn là spin/0.7s normal, không đổi OS.

Ảnh cuối được soát bằng PIL: **38 JPEG**, gồm **15 ảnh 1440×900**, **20 ảnh 390×844** và **3 ảnh desktop datepicker/multiselect crop 1440×667**. Không còn file `final-*.png`; IAB artifact cũ `desktop-filter-light.png` giữ nguyên. Không có crop mobile trong bộ cuối. Screenshot tool từng timeout CDP một lần, native AX phục hồi rồi tiếp tục được; không diễn giải lỗi capture này thành lỗi ứng dụng. Các ảnh inspector mobile được chụp lại ở trạng thái panel thực sự mở.

## Source sau sửa cuối — 20/10/2026

Đây là **các lệnh riêng và một lần full test suite**, không phải một lần `run.ps1 check` gộp sau sửa cuối. Chạy trên Node 24.19.0 từ `Frontend/`.

| Lệnh/lần chạy | Thời điểm Asia/Saigon | Kết quả |
|---|---|---|
| Full unit suite | 00:06:54 ngày 20/10 | **23/23 pass trong 5 files**: theme 4, store 4, editor 8, shell 4, focus helper 3 |
| `.\run.ps1 lint` | Khoảng 00:07 ngày 20/10 | Exit 0, toàn bộ source |
| `.\run.ps1 format:check` | Phiên kiểm tra cuối ngày 20/10 | Exit 0 |
| `.\run.ps1 build` | **00:07:38 ngày 20/10** (`2026-10-19T17:07:38.979Z`) | Exit 0; initial **514.39kB raw / 121.28kB estimated transfer** |

Các lần 8/12/18/23 tests được giữ riêng theo thời điểm; không cộng dồn số pass. Các ca mới bao phủ lifecycle/focus helper và guard theo sửa QA. Build/source checks, Chrome local QA và bộ ảnh hoàn tất phạm vi local step 5 với normal animation bật. Việc người dùng duyệt thiết kế và license hợp lệ chưa được suy ra từ kết quả local.

Sau QA đã reset viewport, đóng tab do task mở và dừng dev server do task sở hữu. Chạy lại bằng `Frontend/run.ps1 start`; URL trong README là địa chỉ dev server khi khởi động, không khẳng định tiến trình vẫn chạy.

## PrimeUI key chưa có

Người dùng xác nhận bổ sung key sau. Runtime hiện không có key và giữ cảnh báo console/watermark đỏ của vendor. `PRIMEUI_LICENSE_KEY` là biến tùy chọn; generator tạo `public/runtime-config.js` bị Git ignore, truyền vào `providePrimeNG({license})`. `.env.example` không tự load `.env`. Chưa kiểm tra key hợp lệ, entitlement hoặc giao diện sạch watermark; không suppress/bypass watermark.

## Giới hạn và cách kiểm tra tiếp

- Bản source cuối đạt lint, format, **23/23 tests** và production build qua các lệnh riêng; Chrome desktop/mobile viewport QA và bộ ảnh đã hoàn tất phạm vi local. Chưa có user design approval hoặc nghiệm thu deployment.
- Tham chiếu Webflow hỗ trợ cấu trúc/hành vi; kích thước shell trong DESIGN là baseline triển khai, chưa đo từ ảnh chính chủ. Không chứng nhận pixel-perfect.
- MotionSites Apps là tham chiếu cảm hứng; một fetch riêng bị timeout, không có thời lượng animation được đo.
- OS reduced-motion QA được hoãn theo yêu cầu người dùng, normal animation vẫn bật và fallback CSS giữ nguyên; không dùng việc hoãn này làm blocker. Không tuyên bố đã kiểm thiết bị vật lý, mọi trình duyệt hoặc đo filmstrip first paint; System/reload đã thao tác và bootstrap trước paint đã được triển khai.
- PrimeUI key hợp lệ, kiểm tra license/watermark và nghiệm thu người dùng/deployment còn chờ; không thay bằng kết quả build hoặc fixtures.
- UI dùng fixture không nghiệm thu API, phiên/quyền, nghiệp vụ Catalog hay deployment.
