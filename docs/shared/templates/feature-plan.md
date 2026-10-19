# <Mã tính năng> — <Tên tính năng>

> Sao chép sang `docs/<area>/features/<ma-tinh-nang>.md`, với `<area>` là `backend`, `frontend` hoặc `shared`, rồi thay toàn bộ phần trong `<...>`. Mẫu này chưa phải một tính năng đã được triển khai. Điều chỉnh liên kết theo vị trí đích; các đường dẫn trong backtick là hướng dẫn, không phải báo cáo đã có.

Ngày bắt đầu: `<YYYY-MM-DD>`
Cập nhật gần nhất: `<YYYY-MM-DD>`
Trạng thái: `<Đang thiết kế | Đang triển khai | Chờ kiểm chứng | Chờ nghiệm thu runtime | Hoàn tất>`
Khu vực phụ trách: `<BE | FE | Shared>`
Mốc roadmap: `<BE/FE: ../roadmap.md#...; shared: liên kết mốc BE/FE liên quan>`
Hồ sơ liên quan: `<Hồ sơ BE/FE hoặc điều phối chung; tránh sao chép trạng thái>`

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

### Domain và database — khi có thay đổi

Nếu không đổi domain/database, ghi rõ và dẫn tới hợp đồng hiện hành; không điền bảng giả định chỉ để đủ mẫu. Với database thay đổi, ghi đủ bảng sở hữu, quan hệ, cột và các ràng buộc dưới đây.

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
- Migration cho các provider được hỗ trợ; xử lý dữ liệu cũ: `<... hoặc không đổi DB>`.
- Tài liệu database chi tiết: `<liên kết nếu tách riêng>`.

### API, quyền và vận hành

| Endpoint/use case | Request/response thay đổi | Quyền và phạm vi dữ liệu | Lỗi/ảnh hưởng client |
|---|---|---|---|
| `<...>` | `<...>` | `<...>` | `<...>` |

Cấu hình, adapter, triển khai/khôi phục: `<... hoặc không thay đổi>`.

### Giao diện và tích hợp — khi liên quan FE

| Màn hình/luồng | Nguồn dữ liệu/API chuẩn | Trạng thái và quyền | Tiêu chí kiểm tra |
|---|---|---|---|
| `<...>` | `<Link hợp đồng BE; phần chưa có API>` | `<Loading/empty/error/success, phiên/quyền>` | `<Validation, conflict/retry, responsive/accessibility liên quan>` |

- Thiết kế, điều hướng, form/component và khả năng tái sử dụng cần thiết: `<...>`.
- Môi trường/API thật, version nguồn; mock đang dùng và bước thay thế: `<...>`.
- Tác động tới client/BE khác và thứ tự tích hợp: `<...>`.

Thay đổi thuần BE hoặc tài liệu ghi “không thay đổi FE” nếu phù hợp; không tạo màn hình chỉ để hoàn tất bảng.

### Nhật ký quyết định

| Ngày | Quyết định | Lý do và phương án đã cân nhắc | Tác động |
|---|---|---|---|
| `<...>` | `<...>` | `<...>` | `<...>` |

## 3. Kế hoạch từng bước

| Bước | Công việc | Đầu ra/tiêu chí hoàn tất | Phụ thuộc | Trạng thái |
|---|---|---|---|---|
| 1 | Chốt phạm vi/thiết kế | `<Quan hệ/API/quy tắc hoặc luồng giao diện rõ ràng>` | `<...>` | Chưa bắt đầu |
| 2 | Triển khai phần nền cần thiết | `<BE: model/persistence; FE: component/form; hoặc bước tài liệu>` | 1 | Chưa bắt đầu |
| 3 | Hoàn chỉnh lát tính năng | `<Use case/API/quyền hoặc tích hợp UI với BE>` | 2 | Chưa bắt đầu |
| 4 | Kiểm chứng | `<Các ca nghiệm thu và bằng chứng>` | 3 | Chưa bắt đầu |
| 5 | Tài liệu/vận hành/tổng kết | `<Hợp đồng hiện hành, history, roadmap>` | 4 | Chưa bắt đầu |

Điều chỉnh bước theo tính năng và khu vực. Khi chia BE–FE, dẫn tới phần việc liên quan, không đánh dấu tích hợp đạt chỉ vì một bên đã xong. Ghi thay đổi kế hoạch vào nhật ký thay vì đánh dấu hoàn tất các bước đã bỏ.

## 4. Nhật ký triển khai

### <YYYY-MM-DD> — Bước <số>: <tên>

- Đã làm: `<Hành vi, file/endpoint liên quan.>`
- Thiết kế hoặc kế hoạch thay đổi: `<Lý do; liên kết quyết định.>`
- Kiểm chứng tại bước này: `<Lệnh/kết quả hoặc chưa chạy và lý do.>`
- Tiếp theo: `<Công việc cụ thể.>`

## 5. Kiểm chứng và nghiệm thu

| Ngày/môi trường | Lệnh hoặc thao tác | Phạm vi | Kết quả thực tế | Bằng chứng |
|---|---|---|---|---|
| `<...>` | `<...>` | `<BE: provider/runtime/case; FE: browser/API/viewport; tài liệu: links/template>` | `<Pass/fail/skip; không điền trước>` | `<Báo cáo/log/run CI hoặc mô tả tái chạy>` |

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

Hoàn tất khi các tiêu chí chấp nhận được đáp ứng và trạng thái trong roadmap của khu vực phụ trách phản ánh đúng bằng chứng. Báo cáo vào `docs/<area>/verification/`; history vào `docs/<area>/history/` và liên kết từ index tương ứng. Bắt đầu từ [quy trình chung](../../shared/development-workflow.md) trước khi sao chép mẫu này.
