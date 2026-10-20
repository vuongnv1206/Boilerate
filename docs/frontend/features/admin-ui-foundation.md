# FE-admin-ui-foundation — Giao diện quản trị theo Webflow

Ngày bắt đầu: `2026-10-19`  
Cập nhật gần nhất: `2026-10-20`  
Trạng thái: `Đã kiểm chứng local; chờ duyệt thiết kế/license`  
Khu vực phụ trách: `FE`  
Mốc roadmap: [Nền giao diện quản trị](../roadmap.md)  
Hồ sơ liên quan: [Khung Frontend](frontend-root-structure.md), [quy trình chung](../../shared/development-workflow.md)

## 1. Mục tiêu và phạm vi

**Vấn đề:** Frontend mới có thư mục sibling; cần thống nhất và triển khai giao diện quản trị trước các tính năng nghiệp vụ.

**Kết quả mong muốn:** Workspace Angular và PrimeNG lấy Webflow CMS/Ecommerce làm tham chiếu chính; có shell compact, hai theme, bộ control, danh sách và editor tương tác với dữ liệu minh họa.

| Trong mốc này | Hoãn lại | Giả định cần giữ |
|---|---|---|
| 5 step UI đã được người dùng duyệt; quy chuẩn source/design; resize/collapse; control gallery; list/editor; kiểm chứng UI | API, đăng nhập/phiên/quyền, Catalog thật, storefront, triển khai production | Tiếng Việt, desktop ưu tiên, Webflow workspace là chuẩn thị giác; dữ liệu demo không chứng minh nghiệp vụ |

Tiêu chí chấp nhận:

- [x] Có quy chuẩn thiết kế và tham chiếu ghi rõ nguồn; phân biệt số đo triển khai với số đo đã đo từ ảnh.
- [x] Rail → panel ngữ cảnh → vùng làm việc; editor thay list và có inspector bên phải (kiểm chứng local).
- [x] Panel resize/collapse, lưu lựa chọn; giao diện đáp ứng viewport nhỏ (Chrome local).
- [x] System/Light/Dark có bootstrap trước paint; System/reload và overlay theo theme đã kiểm local. Không tuyên bố đã đo filmstrip first paint.
- [x] Control gallery có trạng thái thích hợp; tìm/lọc/chọn/phân trang và editor demo được kiểm local.
- [x] Build và UI thực tế có bằng chứng; normal motion, bàn phím, tiếng Việt dài đã kiểm. Reduced-motion OS-QA hoãn theo người dùng ngày 20/10, CSS fallback giữ nguyên.

## 2. Quyết định thiết kế

### Domain, database và API

Không thay đổi domain, database, migration, API hoặc quyền Backend. Hợp đồng nghiệp vụ tiếp tục ở [API BE](../../backend/commerce-api.md); mốc này không tích hợp API.

### Giao diện và tích hợp

| Màn hình/luồng | Nguồn dữ liệu | Trạng thái | Tiêu chí kiểm tra |
|---|---|---|---|
| Workspace | State phía trình duyệt | Panel mở/đóng, kích thước, theme | Reload, bàn phím, responsive |
| Danh sách | Fixture minh họa trong FE | Query, filter, selection, pagination, empty | Thao tác thực tế, tiếng Việt dài |
| Editor | Fixture đang chọn, chỉnh sửa trong memory | Pristine/dirty/save demo/validation | Giữ điều hướng; inspector; thông báo lưu rõ phạm vi demo |
| Control gallery | Giá trị minh họa | Default/hover/focus/invalid/disabled/readonly/loading/selected tùy control | Theme, overlay, bàn phím, reduced motion |

Thiết kế chính thức nằm ở `Frontend/DESIGN.md`; hướng dẫn code ở `Frontend/AGENTS.md`; skill nhỏ trong `Frontend/.agents/skills/admin-ui-design/`. Các kích thước ban đầu là baseline triển khai, chưa được mô tả là số đo pixel chính xác của Webflow.

### Nhật ký quyết định

| Ngày | Quyết định | Lý do | Tác động |
|---|---|---|---|
| 2026-10-19 | Angular + PrimeNG; tùy biến thành workspace Webflow | Người dùng duyệt kế hoạch UI trước tính năng | Không triển khai phiên/API ở mốc này |
| 2026-10-19 | Dùng dữ liệu demo có nhãn rõ | Đánh giá UI độc lập tiến độ nghiệp vụ | Lưu demo không tạo dữ liệu BE |
| 2026-10-19 | Giữ nguyên thư viện get-design nguồn, chỉ tạo hướng dẫn chọn lọc | Người dùng cho phép đưa tài nguyên vào dự án | Ghi provenance, không chạy script nguồn |
| 2026-10-19 | Chạy UI không có PrimeUI key; bổ sung key sau | Người dùng đã xác nhận | Giữ warning/watermark của vendor; cấu hình license tùy chọn, nghiệm thu license còn chờ |
| 2026-10-20 | Bật normal animation, không yêu cầu giảm chuyển động hoặc OS-QA lúc này | Người dùng chỉ đạo rõ | Giữ fallback prefers-reduced-motion trong CSS; OS-QA được hoãn, không chặn hoàn tất local step 5 |

## 3. Kế hoạch từng bước

| Bước | Công việc | Đầu ra/tiêu chí hoàn tất | Phụ thuộc | Trạng thái |
|---|---|---|---|---|
| 1 | Khóa tham chiếu và quy chuẩn | DESIGN, PRODUCT, quy tắc code, skill và nguồn tham chiếu | — | Hoàn tất phần tài liệu |
| 2 | Xây workspace | Rail, panel resize/collapse/persist, topbar, toolbar, inspector; responsive | 1 | Hoàn tất triển khai và kiểm chứng local |
| 3 | Theme và control | Tokens, custom preset PrimeNG, System/Light/Dark, control gallery | 1–2 | Hoàn tất local QA; license key bổ sung sau |
| 4 | Hai màn tương tác | List và editor demo, điều hướng nhất quán | 2–3 | Hoàn tất triển khai và kiểm chứng local |
| 5 | Motion và kiểm chứng | Build, hành trình browser, ảnh bản dựng và báo cáo giới hạn | 2–4 | Hoàn tất local QA; chưa thay user design approval |

## 4. Nhật ký triển khai

### 2026-10-19 — Khởi tạo kế hoạch trước code

- Đã làm: đọc workflow, template, roadmap và hồ sơ khung; tạo hồ sơ này trước triển khai source.
- Kế hoạch thay đổi: thay roadmap ưu tiên tính năng/phiên bằng 5 step UI đã được người dùng yêu cầu.
- Kiểm chứng: đọc trực tiếp tài liệu và đường dẫn; chưa chạy build hoặc trình duyệt.
- Tiếp theo: hoàn thiện quy chuẩn; triển khai shell/theme và hai màn demo.

### 2026-10-19 — Bước 1: Quy chuẩn và nguồn tham chiếu

- Đã làm: `Frontend/PRODUCT.md`, `DESIGN.md`, `AGENTS.md`, `design/references/`, `THIRD_PARTY_NOTICES.md` và skill `admin-ui-design`; cập nhật README FE và roadmap theo 5 step UI.
- Nguồn: tài liệu chính chủ Webflow về workspace/CMS/theme; thư viện get-design và các reference Impeccable chọn lọc. Không di chuyển/xóa nguồn, copy nguyên thư viện hoặc chạy source scripts.
- Thiết kế: rail 44px, topbar 44px, context 240px trong 200–320px, inspector 280px, control 30px là baseline triển khai; chưa đo pixel từ Webflow. Màu/font có thể tinh chỉnh khi đối chiếu browser.
- Kiểm chứng: skill validator trả `Skill is valid!`; kiểm tra liên kết tương đối của 16 tài liệu trả 0 đích thiếu; `git -C Boilerate diff --check` exit 0. [Báo cáo và lệnh](../verification/2026-10-19-admin-ui-foundation.md). Build/browser đang chờ phần triển khai.
- Tiếp theo: hoàn chỉnh source shell/theme/control/màn demo, sau đó kiểm chứng và cập nhật bằng chứng thật.

### 2026-10-19 — Chuẩn bị runtime và đồng bộ tokens

- Đã làm: chọn Node 24.19.0 bundled, Angular/CLI 22.2.3 và PrimeNG 22.1.2; launcher `Frontend/run.ps1` giúp chạy bằng runtime phù hợp mà không thay Node global. README ghi cách dùng.
- Cài đặt: npm 10 resolver thất bại; npm 12.2.0 cài dependency thành công 425 packages theo kết quả tiến trình triển khai. Không coi lần thất bại là pass.
- DESIGN đồng bộ màu token Light/Dark thực tế; tính contrast 6 cặp chính bằng Python, đạt 4.59–7.29:1 cho text và 3.09–3.30:1 cho field boundary ở các cặp đã chọn.
- Còn lại: lint/test/build/browser và kiểm tra trạng thái thực tế; chưa có bằng chứng runtime tại bước này.

### 2026-10-19 — Bước 2–4: Workspace, control và hai màn demo

- Đã triển khai: shell Angular có rail, context panel resize/collapse/persist, topbar/toolbar; route lazy cho danh sách, editor và gallery; form và inspector, search/filter/selection/pagination, save demo trong memory và guard giữ thay đổi chưa lưu.
- Theme/control: custom preset PrimeNG, tokens Light/Dark và controller System; root theme cho overlay; font Inter tự host, UI tiếng Việt, motion ngắn và reduced-motion styles.
- License: tùy chọn `PRIMEUI_LICENSE_KEY` được `scripts/runtime-config.mjs` đưa vào file runtime bị ignore và truyền qua `providePrimeNG({ license })`. Hook npm/launcher tạo cấu hình khi start/build; không tự load `.env`, không chứa key thật trong source. Người dùng bổ sung key sau, giữ watermark/warning thực tế.
- Kiểm chứng đã có: production build đầu tiên exit 0 (initial bundle 508.87kB raw, 119.77kB estimated transfer), lint toàn source pass sau sửa, 8/8 unit tests pass; mở được Dark UI trên IAB local. [Báo cáo theo lần chạy](../verification/2026-10-19-admin-ui-foundation.md).
- Còn lại: hoàn tất các hành trình browser/ảnh ở cả theme, kiểm tra bản cuối sau thay đổi tiếp theo và review UI; các step 2–4 là đã triển khai, chưa nghiệm thu cuối. Không thay API/auth/BE.

### 2026-10-19 — Bổ sung kiểm tra trước review

- Tiến trình triển khai xác nhận lần unit test ở bước này đạt 12/12 sau bổ sung ca kiểm tra; kết quả 8/8 phía trên là lần đầu, không cộng dồn.
- Browser tương tác và reviewer vẫn đang thực hiện. Chưa ghi nghiệm thu cuối, chưa thêm bằng chứng ảnh hoặc thao tác chưa được cung cấp.

### 2026-10-19 — Bước 5: Kiểm tra bản cuối và giới hạn browser

- `.\run.ps1 check` mới nhất kết thúc **23:36:11 Asia/Saigon** (`2026-10-19T16:36:11Z`), exit 0: lint pass, **18/18 tests trong 4 files**, production build pass. Initial bundle cuối 511.88kB raw, 120.65kB estimated transfer. `.\run.ps1 format:check` exit 0. Lần 23:33 trước đó đã pass, chạy lại sau sửa focus shell.
- Unit coverage cuối: theme 4, store 4, editor 7, shell 3; có whitespace/trim, query-only navigation guard thật, mobile aria/inert/Escape/focus, resize clamp/persist và skip focus trong jsdom. Một lỗi mock shell test đã sửa trước lần pass cuối.
- Browser đã thử trước các sửa cuối: desktop Dark 1280×720 và Light 1440×900, overlay status Light; search `den` có 2 dòng; empty/reset; Draft filter có 5 dòng, chọn/clear 5 dòng; editor Draft autosave/restore và quay lại giữ filter. Published editor mở được leave prompt nhưng chưa hoàn tất lựa chọn trên browser.
- Có ảnh `Frontend/artifacts/screenshots/desktop-filter-light.png`, không phản ánh các sửa typography/accessibility cuối. Không có ảnh mobile/gallery cuối hoặc số đo pixel Webflow.
- Browser hiện tắt trong Work settings; người dùng dự định bật, chưa xác nhận bật lại. CSS reduced motion/mobile 40px và jsdom tests đã có; runtime mobile/reduced motion/gallery/review người dùng vẫn chờ. Chi tiết trong [báo cáo](../verification/2026-10-19-admin-ui-foundation.md).
- Sửa shell cuối đóng drawer/trap khi mở help và đưa focus về main sau điều hướng mobile; check pass sau sửa, chưa có browser confirmation mới. Lỗi metadata HMR tạm thời khi đổi imports đã hết ở build sau full reload; không tính đó là nghiệm thu browser bản cuối.

### 2026-10-19 — Người dùng báo bật lại Browser

- Khoảng 23:39 Asia/Saigon, người dùng báo đã bật lại Browser nhưng AX/getState và reset kernel vẫn trả inventory rỗng/lỗi permission. Chưa có QA/ảnh mới ở giai đoạn này; ghi đúng việc công cụ chưa nhận quyền. [Lịch sử](../history/2026-10-19-04-admin-ui-browser-qa.md).
- Local QA, người dùng duyệt thiết kế, license hợp lệ và deployment là các trạng thái tách biệt.

### 2026-10-20 — Chrome desktop/mobile QA sau khi quyền hoạt động

- Sau người dùng Continue/restart, inventory thực tế có Chrome/IAB và Chrome localhost hoạt động. Đã QA desktop 1440×900 và mobile 390×844: list/editor Light/Dark, nội dung tiếng Việt dài, không overflow ngang trang; bảng mobile cuộn riêng bằng bàn phím.
- Resize End 320px/reload giữ; collapse/reload giữ; mở lại/reset 240px. Dark filter Draft 5, chọn/clear 5 pass. Navigation/inspector mobile focus, modal/inert/Escape/return focus đã thao tác; Published dirty guard Stay giữ input và Discard Light về list.
- Gallery Dark drawer/dialog có tên/focus/Escape về trigger; desktop overlays hai theme và bộ ảnh mới `Frontend/artifacts/screenshots/final-*` đã có. Báo cáo ghi từng ca/ảnh, không dùng ảnh làm bằng chứng pixel-perfect Webflow.
- Sửa lỗi UI tìm được khi QA: đóng nav trước guard, focus sau visibility, inspector modal/trap/backdrop, gallery overlay focus/name, aria PrimeNG tiếng Việt, tooltip keyboard và motion drawer token. Không đổi API/auth.
- Mobile Light drawer/dialog focus/Escape đã pass, dialog rộng 358px nằm trong viewport 390px. Mọi ca mobile dùng Chrome viewport emulation, không có thiết bị vật lý.
- Controls checkbox/switch, table select-all 6 và pagination 4–6/6 pass; tooltip keyboard/Escape tiếng Việt, toast mobile Light/desktop Dark đã thấy. Spinner computed spin/0.7s xác nhận normal motion bật. Nhãn checkbox header gallery được sửa bằng label/inputId.
- Người dùng chọn normal animation bật; OS reduced-motion QA được hoãn, không chặn step 5, fallback CSS giữ nguyên. File ảnh `final-*` chuyển `.jpg` đúng JPEG bytes của tool; ảnh IAB cũ giữ PNG. Local QA không thay người dùng duyệt UI/license/deployment. [History mới](../history/2026-10-20-01-admin-ui-browser-qa.md).
- Fresh list check: page 11–14 rồi previous; search `den` 1–2; empty/reset pass. System pressed=true sau reload; tab gallery mobile ArrowRight/Enter kích hoạt đúng; fresh console errors `[]`.
- Source sau sửa cuối: full suite **23/23 tests/5 files** lúc 00:06:54; lint/format exit 0; build exit 0 lúc **00:07:38 ngày 20/10** (`2026-10-19T17:07:38.979Z`), initial **514.39kB raw / 121.28kB transfer**. Đây là các lệnh riêng, không phải `check` gộp sau sửa cuối.
- Bộ ảnh cuối **38 JPEG**: 15 desktop 1440×900, 20 mobile 390×844 và 3 desktop crop 1440×667. Inspector hai theme đã chụp lại khi mở thật; không còn mobile crop. **Step 5 hoàn tất phạm vi local QA**, chờ người dùng review thiết kế và key license bổ sung sau.

## 5. Kiểm chứng và nghiệm thu

[Báo cáo kiểm chứng](../verification/2026-10-19-admin-ui-foundation.md) ghi kết quả tài liệu/skill và các lần build/lint/test/browser đã có. Kết quả build ban đầu không tự chứng minh các thay đổi về sau hoặc nghiệm thu giao diện.

- Đã triển khai: bước 1–5 với demo UI, normal motion và cấu hình license tùy chọn; chưa thêm API/auth/nghiệp vụ.
- Đã kiểm chứng: tài liệu/skill/link/contrast; bản source cuối lint/format pass, **23/23 tests**, production build pass; Chrome desktop/mobile viewport hai theme, luồng/overlay/focus/animation, System reload và bộ ảnh cuối. Kết quả/lệnh riêng ghi trong báo cáo.
- Chưa nghiệm thu: người dùng duyệt thiết kế, key license và deployment. OS reduced-motion QA hoãn theo người dùng, không chặn step 5. Không tuyên bố thiết bị vật lý hoặc đo filmstrip first paint.
- Giới hạn: nguồn tham chiếu công khai không cung cấp đầy đủ mọi trạng thái Webflow; không tuyên bố pixel-perfect khi chưa đo được. Không có key nên vẫn có warning/watermark PrimeUI.

## 6. Lịch sử commit

Chưa tạo commit cho mốc này.

## 7. Công việc còn lại

| Việc | Lý do chưa hoàn tất | Điều kiện đóng |
|---|---|---|
| Review UI người dùng | Chưa có nghiệm thu bản dựng | Review trực tiếp và ghi các điều chỉnh được chọn |
| PrimeUI key | Người dùng chọn bổ sung sau | Cấu hình key hợp lệ, kiểm tra runtime và trạng thái watermark theo vendor |
| Deployment | Không thuộc phạm vi UI local | Lập mốc môi trường đích và kiểm chứng riêng khi được yêu cầu |

Sau khi người dùng thống nhất nền UI mới lập kế hoạch tính năng và tích hợp API chi tiết. Production vẫn ngoài mốc này.
