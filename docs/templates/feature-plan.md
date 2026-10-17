# <Mã tính năng> — <Tên tính năng>

> Sao chép sang `docs/features/<ma-tinh-nang>.md`, thay toàn bộ phần trong `<...>`. Mẫu này chưa phải một tính năng đã được triển khai. Liên kết bên dưới tính từ file trong `docs/features/`.

Ngày bắt đầu: `<YYYY-MM-DD>`
Cập nhật gần nhất: `<YYYY-MM-DD>`
Trạng thái: `<Đang thiết kế | Đang triển khai | Chờ kiểm chứng | Chờ nghiệm thu runtime | Hoàn tất>`
Mốc roadmap: `<liên kết tới ../roadmap.md#...>`

## 1. Mục tiêu và phạm vi

**Vấn đề:** `<Người dùng gặp gì, trong thao tác nào?>`

**Kết quả mong muốn:** `<Hành vi có thể quan sát và kiểm tra.>`

| Trong mốc này | Hoãn lại | Giả định cần giữ |
|---|---|---|
| `<...>` | `<...>` | `<...>` |

Tiêu chí chấp nhận:

- [ ] `<Tình huống hợp lệ và kết quả.>`
- [ ] `<Tình huống không hợp lệ/quyền thiếu và kết quả.>`
- [ ] `<Concurrency/rollback/idempotency nếu liên quan.>`

## 2. Quyết định thiết kế

### Domain và database

| Quan hệ | Cardinality và tính bắt buộc | Bảng/cột lưu quan hệ | FK/unique/check/quy tắc xóa |
|---|---|---|---|
| `<A → B>` | `<1–N; B bắt buộc có A>` | `<B.AId>` | `<FK Restrict; ...>` |
| `<A ↔ C nếu có>` | `<N–N; A có 0–N C>` | `<AC, PK(AId,CId)>` | `<Hai FK; không trùng liên kết; ...>` |

| Bảng/cột thay đổi | Kiểu/nullable/default | Index/precision/concurrency | Lý do |
|---|---|---|---|
| `<...>` | `<...>` | `<...>` | `<...>` |

- Aggregate và quy tắc domain: `<...>`.
- Quy tắc DB bảo vệ; quy tắc cần domain + transaction: `<...>`.
- Transaction/thứ tự khóa/idempotency: `<...>`.
- Audit/lịch sử/outbox và tác động rollback: `<...>`.
- Migration SQL Server/PostgreSQL; xử lý dữ liệu cũ: `<... hoặc không đổi DB>`.
- Tài liệu database chi tiết: `<liên kết nếu tách riêng>`.

### API, quyền và vận hành

| Endpoint/use case | Request/response thay đổi | Quyền và phạm vi dữ liệu | Lỗi/ảnh hưởng client |
|---|---|---|---|
| `<...>` | `<...>` | `<...>` | `<...>` |

Cấu hình, adapter, triển khai/khôi phục: `<... hoặc không thay đổi>`.

### Nhật ký quyết định

| Ngày | Quyết định | Lý do và phương án đã cân nhắc | Tác động |
|---|---|---|---|
| `<...>` | `<...>` | `<...>` | `<...>` |

## 3. Kế hoạch từng bước

| Bước | Công việc | Đầu ra/tiêu chí hoàn tất | Phụ thuộc | Trạng thái |
|---|---|---|---|---|
| 1 | Chốt phạm vi/thiết kế | `<ERD/quan hệ/API/quy tắc rõ ràng>` | `<...>` | Chưa bắt đầu |
| 2 | Domain và persistence | `<Model/mappings/migrations tương ứng>` | 1 | Chưa bắt đầu |
| 3 | Use case/API/quyền | `<Lát nghiệp vụ chạy được>` | 2 | Chưa bắt đầu |
| 4 | Kiểm chứng | `<Các ca nghiệm thu và bằng chứng>` | 3 | Chưa bắt đầu |
| 5 | Tài liệu/vận hành/tổng kết | `<Hợp đồng hiện hành, history, roadmap>` | 4 | Chưa bắt đầu |

Điều chỉnh bước theo tính năng. Ghi thay đổi kế hoạch vào nhật ký thay vì đánh dấu hoàn tất các bước đã bỏ.

## 4. Nhật ký triển khai

### <YYYY-MM-DD> — Bước <số>: <tên>

- Đã làm: `<Hành vi, file/endpoint liên quan.>`
- Thiết kế hoặc kế hoạch thay đổi: `<Lý do; liên kết quyết định.>`
- Kiểm chứng tại bước này: `<Lệnh/kết quả hoặc chưa chạy và lý do.>`
- Tiếp theo: `<Công việc cụ thể.>`

## 5. Kiểm chứng và nghiệm thu

| Ngày/môi trường | Lệnh hoặc thao tác | Phạm vi | Kết quả thực tế | Bằng chứng |
|---|---|---|---|---|
| `<...>` | `<...>` | `<Provider/runtime/case>` | `<Pass/fail/skip; không điền trước>` | `<Log/TRX/run CI hoặc mô tả tái chạy>` |

- Đã triển khai: `<...>`.
- Đã kiểm chứng: `<...; ghi riêng từng lần chạy>`.
- Chưa chạy/chưa nghiệm thu: `<...; lý do và cách tiếp tục>`.
- Giới hạn/rủi ro còn lại: `<...>`.

Nếu bằng chứng nằm trong `artifacts/` không được commit, ghi đủ lệnh và cấu hình không chứa secret để tái chạy. CI YAML, test bị skip hoặc mock adapter không xác nhận runtime/adapter thật.

## 6. Lịch sử commit

| Commit thực tế | Nội dung | Tài liệu/bước liên quan |
|---|---|---|
| `<hash sau khi commit>` | `<...>` | `<...>` |

Chỉ điền hash sau khi commit đã tồn tại; bảng có thể được hoàn thiện ở commit tài liệu tiếp theo.

## 7. Công việc còn lại

| Việc | Lý do chưa hoàn tất | Điều kiện/kiểm chứng để đóng | Mốc tiếp theo |
|---|---|---|---|
| `<...>` | `<...>` | `<...>` | `<...>` |

Hoàn tất khi các tiêu chí chấp nhận được đáp ứng và trạng thái trong [roadmap](../roadmap.md) phản ánh đúng bằng chứng.
