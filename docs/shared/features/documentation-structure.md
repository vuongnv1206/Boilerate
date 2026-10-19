# Chuẩn hóa tài liệu BE, FE và quy tắc chung

Ngày bắt đầu/cập nhật: 2026-10-19, Asia/Saigon.
Trạng thái: đang triển khai tài liệu; không thay đổi chức năng BE hoặc triển khai FE.
Mốc liên quan: tổ chức tài liệu cho giai đoạn BE và FE phát triển song song.

## 1. Mục tiêu và phạm vi

Tách rõ tài liệu theo trách nhiệm để người làm BE hoặc FE tìm được kế hoạch, hợp đồng và bằng chứng của phần mình. Giữ một nguồn cho mỗi quyết định; bảo toàn lịch sử và phân biệt code đã có, kiểm chứng local và nghiệm thu deployment.

Trong phạm vi: di chuyển tài liệu hiện tại; sửa liên kết và chỉ dẫn; tạo điểm bắt đầu cho FE, quy ước phối hợp BE–FE và một file quy tắc chung ở root. Ngoài phạm vi: chọn framework FE, dựng UI, đổi API/database hoặc nghiệm thu lại nghiệp vụ.

Tiêu chí chấp nhận:

- [x] BE có index, roadmap, database/API, feature plans, history và verification riêng.
- [x] FE có index và lộ trình ban đầu, ghi rõ chưa triển khai và các quyết định còn mở.
- [x] Nghiệp vụ và quy trình dùng chung có một nguồn; hướng dẫn tích hợp trỏ tới hợp đồng BE hiện hành.
- [x] Các liên kết nội bộ resolve được; root README, plan và AGENTS dùng đường dẫn mới.
- [ ] Một file quy tắc chung không phụ thuộc stack/nghiệp vụ, có dẫn nguồn tham khảo, được đưa vào template.
- [ ] Có history và báo cáo kiểm chứng đúng phạm vi; chia commit theo thay đổi có ý nghĩa.

## 2. Quyết định thiết kế

| Khu vực | Nguồn sở hữu | Quy tắc |
|---|---|---|
| `docs/backend/` | BE | Giữ roadmap và hợp đồng kỹ thuật hiện hành; chuyển toàn bộ verification/lịch sử cũ vào đây, gồm bối cảnh khám phá nghiệp vụ trước khi tách tài liệu |
| `docs/frontend/` | FE | Theo dõi kế hoạch và bằng chứng FE độc lập; không coi kiểm thử BE là nghiệm thu FE |
| `docs/shared/` | BE và FE | Nghiệp vụ, discovery, workflow, template và phối hợp tích hợp; không sao chép endpoint hoặc tiến độ BE sang đây |
| `docs/README.md`, `plan.md` | Điều hướng | Dẫn tới nguồn chính thức, không tạo roadmap tiến độ trùng lặp |
| `CLAUDE.md` | Quy tắc dùng lại | Nội dung diễn đạt riêng, tham khảo bốn nguyên tắc ở file người dùng gửi; AGENTS bổ sung quy ước riêng của repository |

Không đổi database/cardinality, quyền, transaction, API, migration hoặc chính sách xóa. Di chuyển bằng mapping từng file trong repository; liên kết tương đối được tính lại theo vị trí đích. Các lệnh/kết quả đã chạy trong history và verification giữ nguyên như bằng chứng tại thời điểm ghi; chỉ sửa liên kết để đọc tiếp được. Tra lịch sử trước lần di chuyển bằng `git log --follow -- <đường-dẫn-mới>`.

## 3. Kế hoạch từng bước

| Bước | Công việc | Tiêu chí hoàn tất | Trạng thái |
|---|---|---|---|
| 1 | Phân loại, chuyển tài liệu và sửa điều hướng | Có BE/FE/shared; liên kết, hướng dẫn và template kế hoạch nhất quán | Đã triển khai/kiểm tra local |
| 2 | Thêm quy tắc chung và đóng gói template | File generic có nguồn, AGENTS tham chiếu, template sinh ra có file mới | Chưa bắt đầu |
| 3 | Kiểm chứng và ghi lịch sử/commit | Kiểm tra links/diff, pack và sinh template; ghi rõ phạm vi không chạy lại | Chưa bắt đầu |

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

## 5. Kiểm chứng và nghiệm thu

Đã kiểm tra đích move, bảo toàn báo cáo/lịch sử và liên kết file Markdown bước 1; xem [báo cáo](../verification/2026-10-19-documentation.md) cho số liệu cuối và phạm vi. Kiểm tra đường dẫn hướng dẫn còn dùng, `git diff --check` và staged diff trước commit. Pack/sinh template thực hiện ở bước 2 khi thêm file rule.

Không cần chạy lại kiểm thử nghiệp vụ hai DB cho thay đổi chỉ về tài liệu và nội dung template. Kết quả BE trước đó giữ nguyên ngày và phạm vi; không có bằng chứng nghiệm thu FE hoặc deployment mới ở nhiệm vụ này.

## 6. Lịch sử commit

Chỉ ghi hash sau khi commit tồn tại; mỗi commit có history tương ứng.

## 7. Công việc còn lại

Hoàn tất các bước trên. Framework, thiết kế giao diện và phạm vi lát FE đầu tiên chưa được chọn trong nhiệm vụ chuẩn hóa tài liệu.
