# Quy trình phát triển cùng tài liệu

Tài liệu hợp đồng được cập nhật cùng tính năng. Lịch sử chỉ giữ mốc tính năng hoặc thay đổi hành vi đáng kể; Git giữ chi tiết code. Sửa lỗi nhỏ/refactor/format/test không cần nhật ký riêng; với thay đổi database, API hoặc nghiệp vụ, tạo hồ sơ tính năng theo [mẫu](templates/feature-plan.md).

## Chọn nơi ghi tài liệu

| Công việc | Hồ sơ, lịch sử, bằng chứng | Nguồn tiến độ |
|---|---|---|
| Backend | `docs/backend/features/`, `history/`, `verification/` trong cùng khu vực BE | [Roadmap BE](../backend/roadmap.md) |
| Frontend | Tài liệu hiện hành trong repo sibling `Boilerate.Frontend/docs/` | [Roadmap FE](../frontend/roadmap.md) |
| Nghiệp vụ/quy trình hoặc phối hợp cả hai | `docs/shared/features/`, `history/`, `verification/` trong shared; liên kết hồ sơ BE/FE nếu có | Các mốc BE/FE bị ảnh hưởng; không tạo bản sao trạng thái ở shared |

Chỉ tạo thư mục/index khi có nội dung đầu tiên. Mỗi hợp đồng có một nguồn: nghiệp vụ ở shared, API/database ở BE, thiết kế và hành vi giao diện ở FE. Thay đổi đi qua hai bên dùng một hồ sơ điều phối liên kết phần việc và bằng chứng riêng, theo [hướng dẫn tích hợp](contracts/frontend-backend.md).

## Bước 1 — Chốt mục tiêu và phạm vi

1. Ghi vấn đề người dùng cần giải quyết và kết quả quan sát được sau thay đổi.
2. Ghi phần nằm trong mốc này, phần hoãn lại và các giả định đang dùng.
3. Chọn mốc tại roadmap của khu vực phụ trách, tạo `docs/<area>/features/<ma-tinh-nang>.md` (`backend`, `frontend` hoặc `shared`) và chia thành các bước có đầu ra kiểm tra được.
4. Ghi câu hỏi chưa rõ cùng ảnh hưởng của nó; không đánh dấu quyết định chưa chốt là thiết kế đã duyệt.

Tiêu chí chấp nhận cần có ví dụ cụ thể. Ví dụ BE: một Product thuộc hai Category; xóa liên kết Category không làm mất Product. Ví dụ FE: form đọc đúng schema của loại sản phẩm; khi server trả 409, dữ liệu người dùng đang sửa được giữ để đối chiếu và tải lại.

## Bước 2 — Thiết kế phần bị ảnh hưởng

1. Ghi đối tượng nghiệp vụ, aggregate bảo vệ quy tắc và phần handler/service điều phối.
2. Với database, ghi rõ từng quan hệ là 1–1, 1–N hay N–N; có bắt buộc hay không; bảng/cột nào giữ FK. N–N phải nêu bảng nối và khóa của bảng nối.
3. Ghi PK, FK ghép nếu cần, unique/check/index, nullable, precision/scale, concurrency version và quy tắc xóa. Phân biệt quy tắc DB bảo vệ được với quy tắc domain cần kiểm tra trong transaction.
4. Ghi transaction boundary, thứ tự khóa, idempotency, lịch sử/audit và ảnh hưởng của rollback/retry. Dịch vụ ngoài không chạy trong transaction nghiệp vụ; chỉ ra cơ chế bù hoặc outbox nếu cần.
5. Ghi hợp đồng API, quyền cần kiểm tra, thay đổi có phá vỡ client không và chính sách migration/dữ liệu hiện có.

Cập nhật tài liệu hợp đồng hiện hành cùng thay đổi được chọn. Nếu đổi quyết định, bổ sung lý do vào lịch sử để giữ dấu vết thay vì chỉ ghi đè kết luận cũ.

Với FE không đổi database/API, ghi rõ “không thay đổi” và dẫn tới hợp đồng BE thay vì thiết kế lại schema. Chốt màn hình/luồng, nguồn dữ liệu, trạng thái loading/empty/error/success, phiên/quyền, form validation, responsive và accessibility cần kiểm tra. Không tự suy diễn endpoint chưa có; ghi phần cần BE bổ sung trong hồ sơ điều phối.

## Bước 3 — Triển khai từng lát nghiệp vụ

Với BE: domain/ràng buộc → EF mappings/migration nếu có → use case/validation/transaction/quyền/API → media/outbox/adapter khi cần. Khi model thay đổi, tạo migration và kiểm tra SQL cho cả SQL Server lẫn PostgreSQL; không chỉnh `InitialFoundation` để giấu thay đổi mới.

Với FE: luồng/trạng thái giao diện → component/form → tích hợp API/phiên/quyền → kiểm chứng trên BE thật. Mock dùng để phát triển phải ghi rõ, có bước thay bằng API và không được coi là đã nghiệm thu tích hợp.

Cập nhật API mẫu, thiết kế màn hình, cấu hình và hướng dẫn chạy ngay tại bước có thay đổi tương ứng. Không tạo tầng abstraction hoặc cấu hình thêm chỉ để “chuẩn bị” cho chức năng chưa thuộc phạm vi.

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
| FE | Hành trình màn hình, loading/empty/error, validation, phiên/quyền, conflict/retry, responsive và accessibility liên quan |
| Tích hợp BE–FE | API thật, payload và trạng thái thực tế; quyền/owner, dữ liệu cũ, mất kết nối; ghi phiên bản source/môi trường |
| Tài liệu | Liên kết, đường dẫn hướng dẫn, index/roadmap; pack/sinh template nếu đổi nội dung được phân phối |

Ghi riêng các kiểm tra không chạy được, nguyên nhân và cách chạy lại. Không dùng kết quả cũ để mô tả trạng thái sau thay đổi mới nếu thay đổi đó có thể làm kết quả không còn đúng. Không gộp các lần chạy thành một con số như thể đó là một lần chạy đầy đủ.

## Bước 5 — Hoàn thiện tài liệu và chia commit

Trước mỗi commit, xem diff của code lẫn tài liệu. Tài liệu phải phản ánh đúng hành vi tại commit đó, bao gồm giới hạn chưa xử lý.

- Chia theo thay đổi có ý nghĩa: thiết kế; domain/persistence; use case/API; vận hành; regression và tài liệu tổng kết khi có thể tách độc lập.
- Mỗi commit giữ repo nhất quán. Nếu tách migration khỏi domain làm build/model sai, giữ chúng trong cùng commit.
- Dùng thông điệp rõ phạm vi, ví dụ `feat(catalog): support multiple categories per product` hoặc `fix(inventory): prevent duplicate stock adjustments`.
- Tra chi tiết commit trong Git; history chỉ tóm tắt năng lực đã triển khai, không giữ bảng commit hoặc nhật ký từng lần chạy.
- Không commit secret, dữ liệu upload, database test, log máy hoặc kết quả kiểm thử cũ vào template. Báo cáo tóm tắt có chọn lọc và lệnh tái chạy thuộc tài liệu trong Git.

Không sửa lại lịch sử đã ghi chỉ để khiến quá trình trông tuyến tính. Nếu một bước bị làm lại, ghi quyết định thay thế và commit sửa tương ứng.

## Bước 6 — Cập nhật trạng thái và việc còn lại

1. Cập nhật checklist trong hồ sơ tính năng và trạng thái mốc trong roadmap.
2. Chỉ thêm mốc vào history khi hoàn thành tính năng hoặc thay đổi hành vi đáng kể; không tạo dated file sau mỗi bước. Liên kết tài liệu đang sở hữu hành vi.
3. Nêu rõ đã triển khai, đã kiểm chứng đến đâu, điểm chưa nghiệm thu và điều kiện để hoàn tất chúng.
4. Báo cho người dùng kết quả, các commit đã tạo và liên kết tài liệu.

Một mốc chỉ hoàn tất khi tiêu chí của mốc được đáp ứng. Nếu Docker, CI hoặc adapter ngoài chưa chạy được thì giữ phần đó ở trạng thái chờ nghiệm thu, dù phần code của tính năng đã có.

Kết quả BE, FE và deployment được ghi riêng theo phạm vi chạy; không dùng số kiểm thử BE để đánh dấu giao diện hoặc tích hợp đã hoàn tất.

Ưu tiên quy ước gọn này khi các mẫu/hồ sơ cũ còn yêu cầu nhật ký sau từng bước. Báo routine checks trong handoff; chỉ giữ một báo cáo tích hợp/nghiệm thu cần thiết cho mốc, raw artifacts ngoài Git. FE duy trì rulebook và evidence trong repo riêng.
