# Tạo khung thư mục Frontend

Ngày: 2026-10-19
Khu vực: FE
Hồ sơ: [FE-structure — Khung thư mục Frontend](../features/frontend-root-structure.md)

## Đã làm

- Tạo workspace cha `BoilerateWorkspace/` với `Boilerate/` và `Frontend/` là hai dự án sibling.
- Di chuyển vật lý repo Backend cùng `.git` và các thay đổi hiện tại vào `BoilerateWorkspace/Boilerate`.
- Đưa `Frontend/README.md` ra ngoài repo Backend để làm điểm bắt đầu cho ứng dụng giao diện.
- Giữ nguyên `src/`, `tests/` và các lệnh Backend hiện hành.
- Giữ template .NET chỉ đóng gói nội dung Backend; Frontend là dự án độc lập.
- Cập nhật README và roadmap FE để phân biệt khung repo với ứng dụng FE đã scaffold.

## Lý do và tác động

Root hiện tại đã là Backend. Workspace cha giúp làm việc với Backend và Frontend song song mà không tạo thay đổi đường dẫn cho solution, Docker, CI hoặc migration Backend. Framework, thư viện UI, phạm vi admin/storefront và cấu hình tích hợp vẫn để mở.

## Kiểm chứng

- Đã kiểm tra các đường dẫn Backend hiện tại trong solution, Dockerfile, CI và script trước khi chỉnh sửa.
- Kiểm tra workspace xác nhận hai dự án sibling; `dotnet pack Boilerate.Template.csproj --no-restore` đạt cho template Backend. Code ở đường dẫn cũ đã được xóa; thư mục cũ còn lại đang chờ handle của tiến trình hiện tại được giải phóng.
- Chưa có kiểm thử trình duyệt, dev server hoặc API integration vì chưa scaffold framework FE.

## Bước tiếp theo

Chốt framework/UI và phạm vi ứng dụng trong FE bước 0, sau đó tạo manifest và lệnh dev/build trong `Frontend/`.
