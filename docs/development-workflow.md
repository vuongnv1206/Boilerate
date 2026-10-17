# Quy trình phát triển cùng tài liệu

Mỗi tính năng được xây dựng song song với tài liệu. Không đợi hoàn tất một module lớn mới viết lại lịch sử. Với sửa lỗi nhỏ, có thể gộp kế hoạch, bước sửa và kiểm chứng trong một mục nhật ký; với thay đổi database, API hoặc nghiệp vụ, tạo hồ sơ tính năng theo [mẫu](templates/feature-plan.md).

## Bước 1 — Chốt mục tiêu và phạm vi

1. Ghi vấn đề người dùng cần giải quyết và kết quả quan sát được sau thay đổi.
2. Ghi phần nằm trong mốc này, phần hoãn lại và các giả định đang dùng.
3. Chọn mốc tại [roadmap](roadmap.md), tạo `docs/features/<ma-tinh-nang>.md` và chia thành các bước có đầu ra kiểm tra được.
4. Ghi câu hỏi chưa rõ cùng ảnh hưởng của nó; không đánh dấu quyết định chưa chốt là thiết kế đã duyệt.

Tiêu chí chấp nhận cần có ví dụ cụ thể. Ví dụ: một Product thuộc hai Category; xóa liên kết Category không làm mất Product; Product Draft không xuất hiện ở API công khai.

## Bước 2 — Thiết kế trước khi đổi persistence và API

1. Ghi đối tượng nghiệp vụ, aggregate bảo vệ quy tắc và phần handler/service điều phối.
2. Với database, ghi rõ từng quan hệ là 1–1, 1–N hay N–N; có bắt buộc hay không; bảng/cột nào giữ FK. N–N phải nêu bảng nối và khóa của bảng nối.
3. Ghi PK, FK ghép nếu cần, unique/check/index, nullable, precision/scale, concurrency version và quy tắc xóa. Phân biệt quy tắc DB bảo vệ được với quy tắc domain cần kiểm tra trong transaction.
4. Ghi transaction boundary, thứ tự khóa, idempotency, lịch sử/audit và ảnh hưởng của rollback/retry. Dịch vụ ngoài không chạy trong transaction nghiệp vụ; chỉ ra cơ chế bù hoặc outbox nếu cần.
5. Ghi hợp đồng API, quyền cần kiểm tra, thay đổi có phá vỡ client không và chính sách migration/dữ liệu hiện có.

Cập nhật tài liệu hợp đồng hiện hành cùng thay đổi được chọn. Nếu đổi quyết định, bổ sung lý do vào lịch sử để giữ dấu vết thay vì chỉ ghi đè kết luận cũ.

## Bước 3 — Triển khai từng lát nghiệp vụ

1. Domain và ràng buộc nghiệp vụ.
2. EF mappings; tạo migration và kiểm tra SQL cho cả SQL Server lẫn PostgreSQL khi model thay đổi. Không chỉnh `InitialFoundation` để giấu thay đổi mới.
3. Use case, validation, transaction, phân quyền và API.
4. Tích hợp media/outbox/adapter khi tính năng cần.
5. Cập nhật API mẫu, cấu hình và hướng dẫn vận hành ngay tại bước có thay đổi tương ứng.

Trong hồ sơ tính năng, ghi sau mỗi bước: phần đã làm, file/endpoint chính, quyết định mới, kiểm chứng đã có và bước tiếp theo. Code đã viết nhưng chưa chạy được phải được ghi rõ là chưa kiểm chứng.

## Bước 4 — Kiểm chứng theo rủi ro

Chạy kiểm tra phù hợp với thay đổi, lưu lệnh, môi trường, thời điểm, số pass/fail/skip và vị trí bằng chứng. Với sửa lỗi, ưu tiên ca tái hiện lỗi trước sửa rồi kiểm tra sau sửa. Không tạo test chỉ lặp lại implementation cho thay đổi tài liệu hoặc thay đổi nhỏ có thể kiểm tra trực tiếp.

| Thay đổi | Bằng chứng cần cân nhắc |
|---|---|
| Domain/API | Hành vi hợp lệ, dữ liệu sai, quyền và điều kiện biên |
| Database/query | Migration thật, constraints, kết quả nhất quán trên hai provider |
| Transaction/kho | Rollback, cạnh tranh cập nhật, retry/idempotency và lịch sử |
| Media/outbox | Crash/restart, lease/retry, worker đồng thời, tham chiếu còn tồn tại |
| Runtime/adapter | Chạy thật trên cấu hình được bật; volume, proxy, health, restart khi liên quan |
| Template | Sinh dự án đổi tên, build, migrations và kiểm tra liên quan |

Ghi riêng các kiểm tra không chạy được, nguyên nhân và cách chạy lại. Không dùng kết quả cũ để mô tả trạng thái sau thay đổi mới nếu thay đổi đó có thể làm kết quả không còn đúng. Không gộp các lần chạy thành một con số như thể đó là một lần chạy đầy đủ.

## Bước 5 — Hoàn thiện tài liệu và chia commit

Trước mỗi commit, xem diff của code lẫn tài liệu. Tài liệu phải phản ánh đúng hành vi tại commit đó, bao gồm giới hạn chưa xử lý.

- Chia theo thay đổi có ý nghĩa: thiết kế; domain/persistence; use case/API; vận hành; regression và tài liệu tổng kết khi có thể tách độc lập.
- Mỗi commit giữ repo nhất quán. Nếu tách migration khỏi domain làm build/model sai, giữ chúng trong cùng commit.
- Dùng thông điệp rõ phạm vi, ví dụ `feat(catalog): support multiple categories per product` hoặc `fix(inventory): prevent duplicate stock adjustments`.
- Ghi commit thực tế vào nhật ký sau khi Git tạo hash. Có thể ghi bảng tổng kết trong commit tài liệu kế tiếp; không điền hash dự đoán hoặc hash của commit chưa tồn tại.
- Không commit secret, dữ liệu upload, database test, log máy hoặc kết quả kiểm thử cũ vào template. Báo cáo tóm tắt có chọn lọc và lệnh tái chạy thuộc tài liệu trong Git.

Không sửa lại lịch sử đã ghi chỉ để khiến quá trình trông tuyến tính. Nếu một bước bị làm lại, ghi quyết định thay thế và commit sửa tương ứng.

## Bước 6 — Cập nhật trạng thái và việc còn lại

1. Cập nhật checklist trong hồ sơ tính năng và trạng thái mốc trong roadmap.
2. Thêm nhật ký theo ngày tại `docs/history/`, liên kết hồ sơ/thiết kế/API và các commit đã có.
3. Nêu rõ đã triển khai, đã kiểm chứng đến đâu, điểm chưa nghiệm thu và điều kiện để hoàn tất chúng.
4. Báo cho người dùng kết quả, các commit đã tạo và liên kết tài liệu.

Một mốc chỉ hoàn tất khi tiêu chí của mốc được đáp ứng. Nếu Docker, CI hoặc adapter ngoài chưa chạy được thì giữ phần đó ở trạng thái chờ nghiệm thu, dù phần code của tính năng đã có.
