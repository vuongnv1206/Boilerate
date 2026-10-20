# FE-structure — Khung thư mục Frontend

Ngày bắt đầu: `2026-10-19`
Cập nhật gần nhất: `2026-10-19`
Trạng thái: `Hoàn tất`
Khu vực phụ trách: `FE`
Mốc roadmap: [FE bước 0](../roadmap.md)
Hồ sơ liên quan: [README FE](../README.md), [hợp đồng BE–FE](../../shared/contracts/frontend-backend.md)

## 1. Mục tiêu và phạm vi

**Vấn đề:** Repo hiện tại là Backend .NET ở thư mục gốc, trong khi cần một vị trí riêng để bắt đầu phát triển FE.

**Kết quả mong muốn:** Có workspace cha chứa hai dự án sibling `Boilerate/` (Backend hiện tại) và `Frontend/` (ứng dụng giao diện trong tương lai), không làm thay đổi đường dẫn build hoặc chạy của Backend.

| Trong mốc này | Hoãn lại | Giả định cần giữ |
|---|---|---|
| Tạo workspace `BoilerateWorkspace/` với `Boilerate/` và `Frontend/`; cập nhật tài liệu | Chọn framework, thư viện UI, admin/storefront và cấu hình build/dev server | `src/` và `tests/` trong dự án `Boilerate/` tiếp tục là Backend .NET |

Tiêu chí chấp nhận:

- [x] Workspace cha có hai dự án sibling `Boilerate/` và `Frontend/`, trong đó `Frontend/` có hướng dẫn phạm vi ban đầu.
- [x] Các đường dẫn Backend hiện hành tiếp tục được giữ nguyên.
- [x] Template .NET vẫn chỉ đóng gói nội dung Backend; Frontend là dự án sibling độc lập.
- [x] Tài liệu FE, roadmap và history phản ánh đúng việc mới tạo khung, chưa có ứng dụng FE.

## 2. Quyết định thiết kế

### Domain và database — khi có thay đổi

Không thay đổi domain, database, migration, API hoặc quyền Backend.

### API, quyền và vận hành

Không thay đổi endpoint, cấu hình runtime Backend, Docker image hoặc deployment. Việc thêm thư mục tài liệu FE không tạo dependency Node.js và không yêu cầu lệnh FE.

### Giao diện và tích hợp — khi liên quan FE

`Frontend/README.md` là điểm bắt đầu cho ứng dụng tương lai. Framework, phạm vi admin/storefront, URL môi trường, phiên/quyền và design system vẫn là quyết định mở của FE bước 0.

### Nhật ký quyết định

| Ngày | Quyết định | Lý do và phương án đã cân nhắc | Tác động |
|---|---|---|---|
| 2026-10-19 | Dùng `BoilerateWorkspace/` làm workspace cha với `Boilerate/` và `Frontend/` là sibling | Root hiện tại đã là Backend; workspace riêng biểu diễn đúng hai dự án mà không đổi cấu trúc .NET bên trong | Cần mở workspace cha; Backend template không đóng gói sibling Frontend |
| 2026-10-19 | Chưa scaffold framework | Framework/UI và phạm vi admin/storefront chưa được chốt trong roadmap FE | Chưa có lệnh dev/build FE hoặc kiểm chứng trình duyệt |

## 3. Kế hoạch từng bước

| Bước | Công việc | Đầu ra/tiêu chí hoàn tất | Phụ thuộc | Trạng thái |
|---|---|---|---|---|
| 1 | Chốt phạm vi tổ chức repo | Quyết định dùng workspace cha với Backend `Boilerate/` và sibling `Frontend/` | — | Hoàn tất |
| 2 | Tạo điểm bắt đầu FE | `Frontend/README.md` mô tả phạm vi và các quyết định còn mở | 1 | Hoàn tất |
| 3 | Đồng bộ workspace và tài liệu | Workspace README, roadmap/history/index được cập nhật; template Backend không nhận diện Frontend sibling | 2 | Hoàn tất |
| 4 | Kiểm chứng | Kiểm tra file, diff và đóng gói template | 3 | Hoàn tất |
| 5 | Scaffold ứng dụng FE | Chọn framework, tạo manifest và lệnh dev/build | 1 | Chưa bắt đầu |

## 4. Nhật ký triển khai

### 2026-10-19 — Bước 1–4: Tạo khung thư mục Frontend

- Đã làm: tạo workspace `BoilerateWorkspace/`, đưa `Frontend/README.md` thành sibling của dự án `Boilerate/`; cập nhật README, roadmap và tài liệu lịch sử FE.
- Thiết kế hoặc kế hoạch thay đổi: giữ nguyên toàn bộ đường dẫn Backend trong repo sau khi di chuyển vật lý; hoãn chọn framework.
- Kiểm chứng tại bước này: kiểm tra cấu trúc/diff; chạy đóng gói template sau khi cập nhật.
- Tiếp theo: chốt framework và phạm vi ứng dụng trước khi scaffold mã FE.

## 5. Kiểm chứng và nghiệm thu

| Ngày/môi trường | Lệnh hoặc thao tác | Phạm vi | Kết quả thực tế | Bằng chứng |
|---|---|---|---|---|
| 2026-10-19 / local | Kiểm tra cấu trúc và diff | sibling `Frontend/README.md`, docs, template include | Pass; file và các liên kết chính đã có, diff không có lỗi whitespace | Bản ghi này |
| 2026-10-19 / local | Kiểm tra workspace paths và `dotnet pack Boilerate.Template.csproj --no-restore` | Workspace sibling layout; đóng gói template Backend | Pass; `BoilerateWorkspace/Boilerate` là repo vật lý, `Frontend/README.md` ở sibling và package Backend tạo thành công | Bản ghi này; package output ignored |

- Đã triển khai: khung thư mục và tài liệu hướng dẫn ban đầu.
- Đã kiểm chứng: workspace paths, cấu trúc/diff và template package Backend local đạt.
- Chưa chạy/chưa nghiệm thu: chưa có framework, dev server hoặc kiểm thử trình duyệt FE.
- Giới hạn/rủi ro còn lại: việc chọn framework và phạm vi admin/storefront có thể làm thay đổi cấu trúc con bên trong `Frontend/`.

## 6. Lịch sử commit

| Commit thực tế | Nội dung | Tài liệu/bước liên quan |
|---|---|---|
| Chưa commit | Tạo workspace sibling Frontend | Bước 1–4 |

## 7. Công việc còn lại

| Việc | Lý do chưa hoàn tất | Điều kiện/kiểm chứng để đóng | Mốc tiếp theo |
|---|---|---|---|
| Chọn framework/UI và phạm vi app | Chưa có quyết định trong roadmap FE | Có quyết định, manifest, lệnh dev/build và kiểm tra tích hợp BE | FE bước 0 |
