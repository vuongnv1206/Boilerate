# Gia hạn thủ công có claim và xác minh

Ngày bắt đầu: 2026-10-18. Cập nhật: 2026-10-19, Asia/Saigon. Trạng thái: **đã có code, đang kiểm chứng local**.
Mốc: bước4B [Shop MVP](shop-mvp.md), [roadmap](../roadmap.md).

## 1. Mục tiêu và phạm vi

Sau nhận tiền, mỗi dòng ManualExtension có một yêu cầu gia hạn. Khách bổ sung định danh tài khoản đích; nhân viên nhận việc, lưu ý định bắt đầu trước thao tác ngoài, rồi lưu kết quả đã xác minh. Không tự cộng hạn từ số lượng hoặc coi Paid là gia hạn thành công.

| Trong lát này | Hoãn lại | Giả định triển khai |
|---|---|---|
| Request/claim/start/verification/history; target và bằng chứng mã hóa; owner/admin API; khóa cùng target; hoàn tất đơn khi đủ mọi món | Tự động gọi dịch vụ ngoài, LMS, refund/returns, upload bằng chứng, migration nghiệp vụ đổi target sau khi đã bắt đầu | Một request/dòng, Quantity là số đơn vị gói từ snapshot Name/SKU/options; người bán thể hiện thời hạn gói trong tên/options. Nhân viên xác nhận kết quả thực tế, hệ thống không tính ngày |
| Quyền Process/Verify/Reveal riêng, không gắn cứng vai trò quản lý | Tự suy quyền Verify từ Process; tự retry thao tác bên ngoài | Khách nhập định danh sau Paid, không nhập password; một target/request, qty2 vẫn cùng tài khoản |

Tiêu chí:

- [ ] Một Paid/non-Cancelled ManualExtension line có đúng một request, retry payment/provision không tạo trùng; đơn Unpaid không tạo việc.
- [ ] Hai nhân viên hoặc hai đơn cùng target không có hai quyền bắt đầu đồng thời; lease chỉ áp dụng trước Start.
- [ ] Start lưu PendingVerification trước hành động ngoài. Mất response/restart/hết lease không mở lại thực hiện. Chỉ Verify có bằng chứng/ghi chú mới kết luận Applied hoặc NotApplied.
- [ ] Applied lưu hạn mới UTC thực tế còn hiệu lực và hoàn tất request; NotApplied trở Ready để nhận/làm lần mới rõ ràng, không tự chạy lại; lịch sử các lần giữ nguyên.
- [ ] Qty không tự tăng expiry; không đổi target sau Start; hoàn tất đơn cần đủ physical/digital/renewal.
- [ ] Target/evidence/token không lọt DTO thường/audit/outbox; owner-only và ACL cả controller/service, no-store; rollback/race/replay và hai DB được kiểm chứng.

## 2. Thiết kế

### Domain và database

Renewals sở hữu request/attempt/operation. OrderItem giữ snapshot gói, options và Quantity bất biến, không thêm nguồn giá/thời hạn khác. Không tạo tài khoản hay tự xác minh quyền ở dịch vụ ngoài.

| Quan hệ | Cardinality | Cách bảo vệ |
|---|---|---|
| OrderItem → RenewalRequest | 1–0..1, chỉ ManualExtension4 | UQ OrderItemId; composite FK (OrderItemId,OrderId,VariantId,FulfillmentKind) tới AK hiện hữu, Kind=4; Restrict |
| RenewalRequest → RenewalAttempt | 1–0..N | FK RequestId Restrict; UQ OperationId; filtered UQ RequestId khi Outcome=Pending1 |
| RenewalRequest → RenewalOperation | 1–N | FK RequestId Restrict; UQ OperationId toàn bảng; operation append-only dùng replay và lịch sử metadata |

| Bảng/cột | Kiểu/constraints/index | Ownership/concurrency |
|---|---|---|
| Renewals.RenewalRequests | Id GUID PK; OrderId/OrderItemId/VariantId GUID; FulfillmentKind4; Status AwaitingTarget1/Ready2/Claimed3/PendingVerification4/Completed5; ProtectedTarget? max4000; TargetKey? ASCII64; TargetPreview?100; TargetLocked bool; ClaimedBy?/ClaimToken? GUID; ClaimUntilUtc?; CompletedAtUtc?/VerifiedExpiresAtUtc?; audit/Version32 | UQ OrderItemId; index(Status,CreatedOn,Id); filtered UQ TargetKey khi Status IN(3,4); trạng thái ràng buộc target/lease/claim/completion bằng check |
| Renewals.RenewalAttempts | Id GUID PK; RequestId/OperationId/StartedBy GUID; StartedAtUtc; Outcome Pending1/Applied2/NotApplied3; VerifiedBy?/VerifiedAtUtc?/NewExpiresAtUtc?; ProtectedEvidence? max32000 | Identity/start bất biến; chỉ pending được resolve một lần; Applied cần verified expiry > verified time, NotApplied không có expiry; không delete hoặc sửa kết quả đã chốt |
| Renewals.RenewalOperations | Id GUID PK; RequestId/OperationId/ActorId GUID; Action ASCII32; RequestHash ASCII64; StatusAfter int1..5; OccurredAtUtc | Append-only; không lưu target/evidence/token/payload rõ; replay hash gồm request/action/actor/nội dung chuẩn hóa, bỏ version/token |

Request là aggregate root có backing collection Attempts. TargetLocked set một lần khi Start, chặn đổi target về sau kể cả NotApplied. ProtectedTarget giữ định danh đã trim, bảo toàn hoa/thường thực tế; fingerprint SHA256 của FormKC+uppercase+trim phục vụ serialization thận trọng toàn shop. Hash không phải encryption; không coi hash là secret an toàn trước đoán thử. Mask chỉ hiển thị vài ký tự. Hai định danh khác nhưng cùng canonical bị xử lý tuần tự; alias ngoài dịch vụ không thể tự phát hiện, nhân viên phải đối chiếu tài khoản thực tế. Đây là giới hạn khi chưa tích hợp provider.

Không có Processing có thể tự hết hạn: Start chuyển thẳng Claimed→PendingVerification và tạo attempt Pending trước hành động ngoài; kết quả chưa biết cho tới khi Verify. Nhân viên chỉ được thao tác ngoài sau khi Start thành công hoặc đã đối chiếu operation qua API. Lease mặc định15phút (Ordering.RenewalClaimMinutes,1..120) chỉ trước Start. Sau Start claim token/lease bị xóa, target vẫn khóa vô hạn cho tới xác minh. Process có thể Claim/Release việc mình; claim hết hạn chưa Start được reclaim. PendingVerification không bao giờ được reclaim bằng timeout. Verify riêng có thể xác minh của nhân viên khác, lưu actor/thời gian; không giả định role tên Manager.

Attempt Applied cần NewExpiresAtUtc UTC lớn hơn thời điểm xác minh và note có nghĩa (tối đa1200 ký tự); expiry là kết quả dịch vụ, không tính Quantity×ngày. Evidence tham chiếu/text tùy chọn tối đa1800 ký tự; cả note/evidence được bảo vệ bằng Data Protection purpose riêng theo AttemptId. Dung lượng ciphertext32000 tính cả JSON escaping Unicode và base64, tránh từ chối ghi chú tiếng Việt hợp lệ. Không upload file/LMS/email mới trong lát này. Evidence chỉ qua endpoint có Verify và Read audit. Target chỉ qua owner/reveal endpoint được audit trước trả.

Thứ tự transaction: ordering gate hiện hữu serialize claim/target/start/verify/payment/shipping. Active target unique index bảo vệ thêm ở DB. Khi reclaim target của request khác có lease quá hạn, lưu release/history và flush trong cùng transaction trước Claim mới để tránh thứ tự UPDATE vi phạm unique index; rollback vẫn bao trùm mọi flush. Không I/O ngoài trong transaction. Request.Version bảo vệ thao tác; kết quả Verify chạm Order.Version khi reconcile. Bank/payment retry không tạo lại request hoặc gia hạn lại. Operation replay đúng actor/request/action/payload trả trạng thái hiện tại trước check version; đổi payload hoặc dùng operation người khác conflict.

Provision tự động cùng transaction receipt cho ManualExtension của Paid non-Cancelled, kể cả order NeedsReview (chỉ thu nhận target; Process/Start chờ hết NeedsReview). Retry fulfillment cũng đảm bảo requests. Endpoint admin provision hỗ trợ Paid orders trước migration nếu operator cần; version Order bắt buộc, UQ chống trùng, không ETL ngầm từ GET. Không tự hoàn tất đơn khi chỉ có request: phải Completed toàn bộ các request và đủ shipment/digital snapshot. Reconcile không tự đặt PhysicalDispatchStarted khi chưa có shipment.

Audit loại ProtectedTarget/TargetKey/ClaimToken/ProtectedEvidence; metadata/history vẫn có actor/status/version/timestamps. Không cascade từ Product/User/Order vào lịch sử. SaveChanges chặn hard-delete Requests/Attempts, update/delete Operations, sửa attempt đã resolve. Migration mới `ManualRenewals` cho cả SQL Server/PostgreSQL (thứ7); giữ sáu migrations cũ. Down chặn trước DDL khi có renewal/payment/shipping/delivery/stock history hoặc active holds, tránh gỡ một phần chuỗi rồi mới fail ở migration cũ. Baseline mới không ETL; operator provision rõ ràng cho dữ liệu Paid cũ.

### API, quyền và vận hành

| Endpoint | Request/kết quả | Quyền |
|---|---|---|
| GET /api/orders/{orderId}/renewals | List RenewalDto: status/version/mask/quantity/gói, hạn thực tế/thời điểm hoàn tất | Authenticated owner; đơn khác404 |
| PUT /api/orders/{orderId}/renewals/{id}/target | Version,OperationId,AccountIdentifier → RenewalDto | Owner Paid, chỉ trước Start/chưa Claim |
| GET /api/orders/{orderId}/renewals/{id}/target | Nội dung đích của mình + Read audit | Owner |
| GET /api/admin/renewals?page=1&size=20&status=...; GET /{id} | Metadata và attempt metadata; token chỉ khi đúng holder trước Start | Renewal.View |
| POST /api/admin/orders/{orderId}/renewals/provision | OrderVersion → metadata list; stale409 | Renewal.Process; Paid non-Cancelled |
| POST /api/admin/renewals/{id}/claim; /release | Version,OperationId; release thêm ClaimToken | Renewal.Process, holder/token cho release |
| POST /api/admin/renewals/{id}/start | Version,OperationId,ClaimToken → PendingVerification+attempt metadata | Renewal.Process, đúng holder/token/lease |
| POST /api/admin/renewals/{id}/verify | Version,OperationId,AttemptId,Applied,NewExpiresAtUtc?,Note,Evidence? | Renewal.Verify, không bắt buộc cùng người Start |
| GET /api/admin/renewals/{id}/target; GET /{id}/attempts/{attemptId}/evidence | Nội dung bảo vệ, có Read audit | Renewal.Reveal cho target; Renewal.Verify cho evidence |

Mọi response no-store. API thường không mang ciphertext/full target/evidence; chuyên biệt reveal không nằm trong list/search. Capabilities được đăng ký/cấp rõ qua operator CLI, không cấp ngầm khi restart. Backup DB + key ring cùng recovery point; đối chiếu thao tác ngoài sau snapshot trước mở lại xử lý, vì restore không đảo gia hạn đã thực hiện.

### Quyết định

| Ngày | Quyết định | Lý do |
|---|---|---|
| 2026-10-18 | PendingVerification ngay trước thao tác ngoài; không tự mở bằng lease | Thỏa điều kiện đã chốt khi mất kết nối, giảm số trạng thái có thể hiểu nhầm |
| 2026-10-18 | Quyền Verify riêng, khóa target toàn shop, thu định danh sau Paid | Tránh hardcode role/provider; bảo vệ hai đơn cùng định danh, giữ mốc shop nhỏ |

## 3. Kế hoạch từng bước

| Bước | Đầu ra | Trạng thái |
|---|---|---|
| 1 | Thiết kế domain/DB/API/history trước code | Đã ghi |
| 2 | Domain/mappings/migrations/domain tests | Đạt targeted52/52 local (gồm32 domain và6 renewal migration); deployment chưa nghiệm thu |
| 3 | Payment provisioning, owner/admin claim/start/verify và order reconcile | Đạt34/34 API integration local trên hai DB |
| 4 | Hai DB: replay/race/lease/restart/rollback/ACL/privacy/fulfillment/migration | Targeted đạt; regression chung đang chạy |
| 5 | Docs/runbook/roadmap/bằng chứng và commit nhỏ | Cập nhật song song |

## 4. Nhật ký

2026-10-18: đọc hồ sơ nghiệp vụ và các module hiện hữu; ghi [history24](../history/2026-10-18-24-manual-renewals-design.md) trước code. Tiếp đó thêm domain/mappings/guards và migration thứ7 `ManualRenewals` trên hai DB, owner/admin API và payment provisioning. Build Host và test project thành công; các tests nghiệp vụ mới chưa chạy ở thời điểm ghi dòng này. Review trước test phát hiện giới hạn ciphertext8000 không đủ cho Note/Evidence tiếng Việt tối đa; tăng lên32000 trong migration mới chưa commit, không thêm migration sửa nối tiếp hoặc sửa sáu migrations cũ. Request có thêm index OrderId phục vụ My Orders/reconcile.

## 5. Kiểm chứng

2026-10-19: tiếp tục kiểm chứng source hoàn thiện; sửa biên domain32000/32001 cho khớp dung lượng Unicode. [History nền](../history/2026-10-19-01-renewal-domain-database.md), [history API](../history/2026-10-19-02-renewal-api-verification.md), [báo cáo từng lượt](../verification/2026-10-19-manual-renewals.md). Targeted domain/migration52/52 và API34/34 đạt; regression chung đang chạy. Không coi kiểm thử local là nghiệm thu gia hạn ngoài hoặc deployment.

Targeted domain/migration52/52 và API34/34 đạt; regression chung đang chạy. Build và từng lượt được ghi riêng trong báo cáo, không cộng trùng.326/326 thuộc source Shipping trước đó, không chứng minh renewal. Chưa có provider/SMTP/deployment được nghiệm thu từ thay đổi này. Quy trình thủ công không chứng minh exactly-once trên dịch vụ ngoài nếu nhân viên bỏ qua Start/verification hoặc dùng alias khác; phải đối chiếu thực tế.

## 6. Commit

`e06fafc`: thiết kế trước code. `3d2f152`: domain, persistence, migrations hai DB và kiểm chứng nền. Các commit tiếp nối chỉ ghi hash sau khi tồn tại.

## 7. Còn lại

Refund/returns và các gate runtime/adapter thật là mốc riêng; bản này không tự đổi chính sách bồi hoàn hoặc tự thực hiện gia hạn ngoài hệ thống.
