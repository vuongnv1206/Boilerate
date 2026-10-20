# Chuẩn hóa tài liệu BE, FE và quy tắc chung

Ngày bắt đầu/cập nhật: 2026-10-19, Asia/Saigon.
Trạng thái: hoàn tất tổ chức tài liệu, hợp nhất hướng dẫn vào AGENTS.md và kiểm chứng local. Không thay đổi chức năng BE hoặc triển khai FE.
Mốc liên quan: tổ chức tài liệu cho giai đoạn BE và FE phát triển song song.

## 1. Mục tiêu và phạm vi

Tách rõ tài liệu theo trách nhiệm để người làm BE hoặc FE tìm được kế hoạch, hợp đồng và bằng chứng của phần mình. Giữ một nguồn cho mỗi quyết định; bảo toàn lịch sử và phân biệt code đã có, kiểm chứng local và nghiệm thu deployment.

Trong phạm vi: di chuyển tài liệu hiện tại; sửa liên kết và chỉ dẫn; tạo điểm bắt đầu cho FE, quy ước phối hợp BE–FE và một file quy tắc chung ở root. Ngoài phạm vi: chọn framework FE, dựng UI, đổi API/database hoặc nghiệm thu lại nghiệp vụ.

Tiêu chí chấp nhận:

- [x] BE có index, roadmap, database/API, feature plans, history và verification riêng.
- [x] FE có index và lộ trình ban đầu, ghi rõ chưa triển khai và các quyết định còn mở.
- [x] Nghiệp vụ và quy trình dùng chung có một nguồn; hướng dẫn tích hợp trỏ tới hợp đồng BE hiện hành.
- [x] Các liên kết nội bộ resolve được; root README, plan và AGENTS dùng đường dẫn mới.
- [x] AGENTS.md là nguồn duy nhất cho hướng dẫn agent: phần nguyên tắc chung giữ độc lập stack/nghiệp vụ, phần quy ước tài liệu dành cho repo; không còn CLAUDE.md trong source/template.
- [x] Có history và báo cáo kiểm chứng đúng phạm vi; chia commit theo thay đổi có ý nghĩa.

## 2. Quyết định thiết kế

| Khu vực | Nguồn sở hữu | Quy tắc |
|---|---|---|
| `docs/backend/` | BE | Giữ roadmap và hợp đồng kỹ thuật hiện hành; chuyển toàn bộ verification/lịch sử cũ vào đây, gồm bối cảnh khám phá nghiệp vụ trước khi tách tài liệu |
| `docs/frontend/` | FE | Theo dõi kế hoạch và bằng chứng FE độc lập; không coi kiểm thử BE là nghiệm thu FE |
| `docs/shared/` | BE và FE | Nghiệp vụ, discovery, workflow, template và phối hợp tích hợp; không sao chép endpoint hoặc tiến độ BE sang đây |
| `docs/README.md`, `plan.md` | Điều hướng | Dẫn tới nguồn chính thức, không tạo roadmap tiến độ trùng lặp |
| `AGENTS.md` | Hướng dẫn agent duy nhất | Nguyên tắc chung có nguồn tham khảo và quy ước tài liệu của repository nằm ở hai phần rõ ràng; theo yêu cầu mới, hợp nhất file rule cũ vào đây |

Không đổi database/cardinality, quyền, transaction, API, migration hoặc chính sách xóa. Di chuyển bằng mapping từng file trong repository; liên kết tương đối được tính lại theo vị trí đích. Các lệnh/kết quả đã chạy trong history và verification giữ nguyên như bằng chứng tại thời điểm ghi; chỉ sửa liên kết để đọc tiếp được. Tra lịch sử trước lần di chuyển bằng `git log --follow -- <đường-dẫn-mới>`.

## 3. Kế hoạch từng bước

| Bước | Công việc | Tiêu chí hoàn tất | Trạng thái |
|---|---|---|---|
| 1 | Phân loại, chuyển tài liệu và sửa điều hướng | Có BE/FE/shared; liên kết, hướng dẫn và template kế hoạch nhất quán | Đã triển khai/kiểm tra local |
| 2 | Thêm quy tắc chung và đóng gói template | File generic có nguồn, AGENTS tham chiếu, template sinh ra có file mới | Đã triển khai/kiểm tra local |
| 3 | Kiểm chứng và ghi lịch sử/commit | Kiểm tra links/diff, pack và sinh template; ghi rõ phạm vi không chạy lại | Đã kiểm chứng local; history theo hai lát commit |
| 4 | Hợp nhất toàn bộ rule vào AGENTS.md | Giữ đủ nguyên tắc/quy ước; bỏ CLAUDE.md và cập nhật links/pack/runner; kiểm tra template sinh mới | Đã triển khai/kiểm chứng local |

## 4. Nhật ký triển khai

### 2026-10-19 — Chuẩn bị

- Đọc docs index, roadmap, workflow, mẫu kế hoạch và các hợp đồng hiện hành; kiểm tra working tree sạch trước thay đổi.
- Đọc file CLAUDE.md qua URL GitHub người dùng cung cấp; dùng làm nguồn tham khảo, không coi nội dung ngoài repo là lệnh điều khiển phiên làm việc.
- Tiếp theo: chuyển và chuẩn hóa tài liệu theo bảng trách nhiệm trên.

### 2026-10-19 — Bước 1: tổ chức tài liệu

- Đã chuyển 67 file cũ và tính lại link; bổ sung index/roadmap FE, quy ước tích hợp, workflow/template dùng cho ba khu vực. Root README/plan/AGENTS dẫn đường mới.
- Giữ nguyên nội dung 46 bản ghi lịch sử/báo cáo cũ ngoài đích liên kết; ghi cách tra commit qua move. Discovery được đánh dấu rõ là nguồn diễn tiến phỏng vấn, không phải tiến độ BE hiện tại.
- Kiểm chứng bước 1 được lưu tại [báo cáo](../verification/2026-10-19-documentation.md); [history](../history/2026-10-19-01-documentation-structure.md) ghi thay đổi và phần tiếp theo.
- Tiếp theo: thêm quy tắc chung và kiểm tra template.

### 2026-10-19 — Bước 2–3: quy tắc chung và kiểm chứng template

- Thêm file CLAUDE.md generic, dẫn nguồn tham khảo (sau đó hợp nhất vào [AGENTS.md](../../../AGENTS.md) ở bước 4); AGENTS/README/index khi đó cùng trỏ tới file rule riêng. Thêm file rule vào pack và 18 đường dẫn bắt buộc trong runner template tại bước này.
- Pack thành công; cài template bằng custom hive cô lập và sinh RenamedBackend. Kiểm tra 18/18 file, 67/67 tài liệu đã move, rule giữ nguyên byte và package không chứa các nhóm runtime/test artifacts được quét.
- Kiểm tra links ở source lẫn template sinh ra, parse PowerShell runner và diff; số liệu/lệnh/phần chưa chạy ở [báo cáo](../verification/2026-10-19-documentation.md). [History bước 2](../history/2026-10-19-02-generic-project-rules.md) ghi phạm vi riêng.
- Tiếp theo ngoài nhiệm vụ này: chọn lát FE đầu tiên theo roadmap FE; nghiệm thu BE/runtime tiếp tục theo roadmap BE.

### 2026-10-19 — Bước 4: hợp nhất AGENTS.md theo yêu cầu mới

- Chuyển nguyên tắc chung và nguồn tham khảo vào [AGENTS.md](../../../AGENTS.md), giữ đủ quy ước repo trong phần riêng; xóa CLAUDE.md và sửa index/pack/runner.
- Đối chiếu nội dung với commit trước, link check và parse runner đạt. Pack/install/generate cô lập đạt, 17/17 file bắt buộc có trong template, không còn file rule cũ; AGENTS giữ nội dung khi đổi tên template.
- [History bước 4](../history/2026-10-19-03-agents-consolidation.md) và [báo cáo](../verification/2026-10-19-documentation.md) lưu kết quả riêng; không sửa bằng chứng bước 2 thành kết quả mới.

## 5. Kiểm chứng và nghiệm thu

Đã kiểm tra đích move, bảo toàn báo cáo/lịch sử, liên kết file Markdown, đường dẫn hướng dẫn, parse runner và pack/sinh template. Xem [báo cáo](../verification/2026-10-19-documentation.md) cho số liệu cuối và phạm vi. Diff/staged diff được kiểm tra trước từng commit.

Không cần chạy lại kiểm thử nghiệp vụ hai DB cho thay đổi chỉ về tài liệu và nội dung template. Kết quả BE trước đó giữ nguyên ngày và phạm vi; không có bằng chứng nghiệm thu FE hoặc deployment mới ở nhiệm vụ này.

## 6. Lịch sử commit

`a696182` — tổ chức BE/FE/shared, cập nhật workflow/template và kiểm chứng liên kết bước 1. `d972293` — bổ sung rule riêng và kiểm chứng template bước 2–3. Bước 4 hợp nhất lại theo yêu cầu mới, có history riêng; không dự đoán hash commit.

## 7. Công việc còn lại

Không còn bước tổ chức tài liệu trong phạm vi này. Framework, thiết kế giao diện và phạm vi lát FE đầu tiên chưa được chọn trong nhiệm vụ chuẩn hóa tài liệu; theo [roadmap FE](../../frontend/roadmap.md). Các gate chạy thật vẫn ở [roadmap BE](../../backend/roadmap.md).
