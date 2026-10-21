# Backend — BE

Khu vực này sở hữu tài liệu kỹ thuật và bằng chứng BE. [FE](../frontend/README.md) có lộ trình riêng; [nghiệp vụ/quy trình chung](../shared/README.md) dùng cho cả hai bên. Khi nối giao diện, bắt đầu từ [hướng dẫn tích hợp BE–FE](../shared/contracts/frontend-backend.md).

Ưu tiên BE tiếp theo: nghiệm thu toàn luồng [MVP gọn cho một shop nhỏ](features/shop-mvp.md) và môi trường triển khai, theo [roadmap](roadmap.md). Các quyết định nghiệp vụ nằm ở [discovery](../shared/features/business-discovery.md); các gate vận hành còn mở không được tự đóng từ kết quả kiểm thử local.

Tài liệu được cập nhật cùng phần code liên quan. Mỗi tính năng có kế hoạch, các bước thực hiện, bằng chứng kiểm chứng và lịch sử thay đổi để người tiếp theo biết dự án đang ở đâu, vì sao chọn thiết kế đó và còn thiếu gì.

## Đọc theo thứ tự

1. [Lộ trình BE](roadmap.md): phạm vi từng mốc, trạng thái và điều kiện nghiệm thu.
2. [Quy trình phát triển](../shared/development-workflow.md): cách đi từ yêu cầu đến code, kiểm thử, tài liệu và commit.
3. [Mẫu kế hoạch tính năng](../shared/templates/feature-plan.md): sao chép khi bắt đầu tính năng mới hoặc thay đổi hành vi đáng kể.
4. [Hồ sơ Catalog v1](features/catalog-v1.md): kế hoạch và trạng thái triển khai theo từng bước.
5. [Thiết kế database commerce](commerce-database.md): cardinality, cột, FK, unique/check/index, transaction và quy tắc xóa.
6. [API và vận hành commerce](commerce-api.md): khởi tạo, quyền, request/response và vận hành.
7. [Review commerce ngày 2026-10-17](commerce-review.md): lỗi đã sửa, cách kiểm chứng và giới hạn còn lại.
8. [Lịch sử phát triển](history/README.md): các phần việc và commit đã thực hiện.
9. [Kiểm chứng Catalog v1](verification/2026-10-17-commerce.md): kết quả từng lượt, phạm vi source và phần chưa nghiệm thu.
10. [Nghiệm thu vận hành Catalog](features/operations-acceptance.md): các gate runtime/adapter và giới hạn còn mở.
11. [Runbook vận hành/phục hồi](operations-runbook.md) và [kiểm chứng ngày 2026-10-18](verification/2026-10-18-operations.md).
12. [Phỏng vấn nghiệp vụ](../shared/features/business-discovery.md) và [quy tắc giao hàng số/gia hạn](../shared/business/digital-fulfillment.md): nguồn quyết định nghiệp vụ; trạng thái code được theo dõi riêng trong từng hồ sơ tính năng.
13. [Sản phẩm linh hoạt và đơn mua chung](../shared/business/product-flexibility.md): mức tự cấu hình của shop, ví dụ tham khảo và chính sách thanh toán vật lý + số.
14. [MVP shop nhỏ](features/shop-mvp.md): phạm vi gọn, thứ tự BE, tiêu chí nghiệm thu và các đề xuất cần xử lý khi tới bước liên quan.
15. [Cách giao và kho hàng số](features/digital-inventory.md): thiết kế, bước triển khai và [kiểm chứng](verification/2026-10-18-digital-inventory.md).
16. [Giỏ hàng, đơn hàng và giữ hàng](features/ordering-checkout.md): cardinality, transaction, API và kiểm chứng bước 2 MVP.
17. [Nhận tiền và tự động giao hàng số](features/payment-digital-delivery.md): receipt chuyển khoản xác nhận thủ công, trạng thái độc lập, giao Unique/Shared, xử lý trả muộn và email outbox; bằng chứng local/deployment theo hồ sơ.
18. [Giao hàng vật lý và ghi nhận COD](features/physical-shipping.md): shipment có số lượng, xuất từ phần đã giữ, giao từng phần, quyền/version/replay và receipt COD độc lập; đã triển khai, trạng thái kiểm chứng theo hồ sơ.
19. [Gia hạn thủ công](features/manual-renewals.md): nhận việc có lease, Start lưu PendingVerification, xác minh kết quả thực tế, tài khoản đích/bằng chứng được bảo vệ và hoàn tất đơn độc lập Paid.
20. [Hoàn tiền và nhận hàng trả](features/refunds-returns.md): ledger tiền đã hoàn, return theo shipment, restock có kiểm chứng, full refund đóng phần giao còn lại và COD trả về; trạng thái local/deployment theo [báo cáo](verification/2026-10-19-aftersales.md).
21. [Admin Orders workspace](features/admin-orders.md): danh sách lọc server-side, detail snapshot read-only, lifecycle commands, safe timeline và seed/replay; trạng thái kiểm chứng tại [báo cáo](verification/2026-10-21-admin-orders.md).

22. [Phân quyền theo nhóm](features/grouped-authorization.md): API nhóm chức năng, ngữ cảnh quyền hiện tại, tương thích và script migration để review.

Hướng dẫn dựng installation mới, cấu hình và các lệnh operator nằm tại [README dự án](../../README.md). [plan.md](../../plan.md) chỉ điều hướng; tiến độ chính thức nằm trong roadmap và từng hồ sơ BE.

## Cách tổ chức

| Vị trí | Nội dung |
|---|---|
| `docs/backend/roadmap.md` | Các mốc BE, phụ thuộc và điều kiện nghiệm thu |
| `docs/backend/features/<ma-tinh-nang>.md` | Kế hoạch và tiến độ từng bước của một tính năng BE |
| `docs/shared/business/` | Quy tắc nghiệp vụ đã xác nhận và quyết định còn mở; trạng thái triển khai được ghi riêng |
| [docs/backend/history/](history/README.md) | Nhật ký triển khai/review theo ngày, quyết định và liên kết commit |
| `docs/backend/*-database.md`, `docs/backend/*-api.md` | Hợp đồng kỹ thuật BE hiện hành để lập trình và tích hợp |
| `docs/backend/verification/` | Báo cáo tóm tắt kiểm chứng BE được version trong Git, kèm lệnh và môi trường |
| `artifacts/` | Log/TRX và đầu ra kiểm chứng tại máy chạy; có thể bị bỏ qua bởi Git |

Một dòng “đã triển khai” chỉ xác nhận có code. “Đã kiểm chứng” phải kèm môi trường, phạm vi, lệnh và kết quả. “Đã nghiệm thu runtime/adapter” cần bằng chứng chạy thật của runtime/adapter đó. Có workflow CI hoặc dùng mock chưa đủ để đánh dấu nghiệm thu.

Không đưa secret, token, dữ liệu khách hàng hoặc connection string thật vào tài liệu và log được commit. Báo cáo trong Git cần đủ thông tin để tái chạy dù các artifact cục bộ không còn.

Lịch sử trước ngày tách tài liệu vẫn giữ bối cảnh discovery của dự án tại `backend/history/`. Các quyết định nghiệp vụ hiện hành được dẫn sang shared; các lệnh đã chạy trong bản ghi cũ có thể dùng đường dẫn trước khi move. Dùng `git log --follow -- <đường-dẫn-mới>` để theo lịch sử qua lần di chuyển.
