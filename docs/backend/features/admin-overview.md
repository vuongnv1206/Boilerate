# Tổng quan vận hành — backend

- Ngày bắt đầu: 2026-10-25
- Trạng thái: Đã triển khai; đã kiểm chứng SQL Server cô lập và rollout local. PostgreSQL runtime và staging chưa nghiệm thu.
- Điều phối: [Tổng quan Admin](../../shared/features/admin-overview.md)

## Invariant và API

Ba GET authenticated, no-store, tổng hợp server-side và không mutation. Work chỉ trả nhóm theo Order.View/Renewal.View; stock theo Inventory.View/DigitalInventory.View. Không có quyền phù hợp trả 403. Finance yêu cầu FinancialSummary.View riêng, không suy từ Payment.Confirm.

| Endpoint | Request | Response |
| --- | --- | --- |
| GET /api/admin/overview/work | Không có | generatedAtUtc; groups[] gồm key, kind (order/renewal), total, items[] |
| GET /api/admin/overview/finance | from/to ngày YYYY-MM-DD inclusive; thiếu cả hai mặc định 7 ngày; thiếu một, tương lai, đảo ngày hoặc quá 90 ngày trả 400 | generatedAtUtc, from, to, timeZone, currency, received, refunded, net, days[] (date, received, refunded, net) |
| GET /api/admin/overview/stock | threshold 0–999 mặc định 5; page=1, size=10 (10/20/50) | generatedAtUtc, threshold, pageNumber, pageSize, total, items[] |

Work item: id, orderId, orderNumber, name nullable, sku nullable, status (wire enum hiện có), createdAtUtc. Với đơn id=orderId; renewal có id riêng. Stock item: productId, variantId, productName, sku, fulfillmentKind, available. Không có customer contact, digital content, renewal target/evidence hoặc payment reference.

## Nhóm việc và deep link

- Orders queue=awaiting-payment: PendingPayment, Unpaid, ExpiresAt null hoặc > now.
- Orders queue=awaiting-processing: AwaitingProcessing và Fulfillment Pending/InProgress.
- Orders queue=needs-review: Paid, Fulfillment NeedsReview và chưa Closed; gồm đơn đã hủy nhưng nhận tiền muộn.
- Renewals queue=awaiting-work: Ready/Claimed, parent Paid, không Cancelled/Expired/Closed và không NeedsReview.
- Renewals queue=pending-verification: tất cả PendingVerification, kể cả parent Closed.
- Work keys: awaiting-payment, awaiting-processing, needs-review, renewal-awaiting-work, renewal-pending-verification. kind phân biệt order/renewal. Mỗi nhóm tổng toàn bộ và 5 bản ghi cũ nhất theo CreatedOn/Id.
- Bổ sung queue optional vào list API; unknown queue trả 400. Search/status/date vẫn kết hợp bằng AND. Predicates dùng chung để list/count khớp.

## Tiền và kho

Received=sum PaymentReceipt.Amount theo ReceivedAtUtc; refunded=sum Refund.Amount theo RefundedAtUtc; net=received-refunded, có thể âm. Chuyển ngày VN thành [from 00:00 +07, ngày sau to 00:00 +07). Daily buckets có đủ ngày, số 0 khi không phát sinh. Không dùng Order.Total/PaidAt làm receipts. VND nguyên đồng.

Wire received/refunded/net (totals và days) là chuỗi số nguyên decimal invariant để không mất chính xác trên JavaScript khi vượt Number.MAX_SAFE_INTEGER; dấu âm chỉ xuất hiện ở net. Backend vẫn sum/group bằng decimal tại database.

Kho chỉ active variants của Published products. Physical tracked MAIN: OnHand-Reserved, thiếu balance=0; Unique: đếm State.Available; Shared: chỉ thiếu State.Available mới cảnh báo. Không cảnh báo Physical untracked hoặc ManualExtension. Sort available/productName/variantId, phân trang ở DB.

## Quyền, database và rollout

FinancialSummary/View thuộc nhóm Orders, vào capability manifest. --function CODE optional lọc registration/grant hiện có, reject unknown/missing/duplicate value và không dùng riêng hoặc với bootstrap/preflight/seed. Host loại operator flags cùng giá trị function khỏi ASP.NET configuration arguments; cú pháp không hợp lệ dừng trước khi khởi chạy HTTP. Omit filter giữ behavior cũ. Rollout chỉ scope FinancialSummary.

Migration additive index PaymentReceipts(ReceivedAtUtc,Id), Refunds(RefundedAtUtc,Id) trên MSSQL/PostgreSQL; không thay cardinality, cột, FK hoặc dữ liệu nghiệp vụ. Backup copy-only/checksum/verify trước apply DB local.

## Kiểm thử và trạng thái

Các ca: finance midnight +07/COD/backdate/refund khác kỳ/net âm/zero; queue eligibility và count/list; ACL từng phần/401/403; stock reserved/disabled/thiếu balance/publication; GET không ghi DB; operator scope giữ nguyên grants khác và idempotent. SQL Server cô lập là môi trường chính; PostgreSQL chỉ nghiệm thu khi chạy thật.

Ngày 2026-10-26, Release non-provider regression đạt 235/235 và `AdminOverviewTests` SQL Server đạt 4/4, không skip. Integration chạy migration và dữ liệu thật trên database `boilerate_test_*` cô lập; không dùng Development DB. Finance kiểm tra biên ngày UTC+07, COD, nhận tiền lùi ngày, refund khác kỳ, thu ròng âm và VND vượt `Number.MAX_SAFE_INTEGER`. Work đối chiếu queue/list và phiên bản DB, gồm nhận tiền sau hủy và PendingVerification sau đóng. Stock kiểm tra MAIN, reservation, Shared/Unique, publication, phân trang và các quyền độc lập. Scoped operator kiểm tra idempotency, giữ grants hiện có của Admin/Staff, filter không hợp lệ và module bị tắt; non-provider suite kiểm tra operator argument parsing.

Development đã backup/verify rồi áp index migration và đăng ký/cấp riêng `FinancialSummary.View` cho Admin; các grants cũ được giữ nguyên. PostgreSQL có model/migration và test cases nhưng chưa có `BOILERATE_TEST_POSTGRES`; chưa xác nhận runtime. Bằng chứng tổng hợp, browser/live acceptance và giới hạn còn lại được ghi tại [báo cáo tích hợp](../../../../Frontend/docs/verification/2026-10-26-admin-overview.md).
