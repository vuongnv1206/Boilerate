# Tài liệu phát triển

Tài liệu được cập nhật cùng phần code liên quan. Mỗi tính năng có kế hoạch, các bước thực hiện, bằng chứng kiểm chứng và lịch sử thay đổi để người tiếp theo biết dự án đang ở đâu, vì sao chọn thiết kế đó và còn thiếu gì.

## Đọc theo thứ tự

1. [Lộ trình dự án](roadmap.md): phạm vi từng mốc, trạng thái và điều kiện nghiệm thu.
2. [Quy trình phát triển](development-workflow.md): cách đi từ yêu cầu đến code, kiểm thử, tài liệu và commit.
3. [Mẫu kế hoạch tính năng](templates/feature-plan.md): sao chép khi bắt đầu tính năng mới hoặc thay đổi hành vi đáng kể.
4. [Hồ sơ Catalog v1](features/catalog-v1.md): kế hoạch và trạng thái triển khai theo từng bước.
5. [Thiết kế database commerce](commerce-database.md): cardinality, cột, FK, unique/check/index, transaction và quy tắc xóa.
6. [API và vận hành commerce](commerce-api.md): khởi tạo, quyền, request/response và vận hành.
7. [Review commerce ngày 2026-10-17](commerce-review.md): lỗi đã sửa, cách kiểm chứng và giới hạn còn lại.
8. [Lịch sử phát triển](history/README.md): các phần việc và commit đã thực hiện.
9. [Kiểm chứng Catalog v1](verification/2026-10-17-commerce.md): kết quả từng lượt, phạm vi source và phần chưa nghiệm thu.
10. [Nghiệm thu vận hành Catalog](features/operations-acceptance.md): bước đang triển khai trước checkout.
11. [Runbook vận hành/phục hồi](operations-runbook.md) và [kiểm chứng ngày 2026-10-18](verification/2026-10-18-operations.md).

Hướng dẫn dựng installation mới, cấu hình và các lệnh operator nằm tại [README dự án](../README.md). [plan.md](../plan.md) là bản tóm tắt phạm vi hiện tại; lộ trình chi tiết được theo dõi tại đây.

## Cách tổ chức

| Vị trí | Nội dung |
|---|---|
| `docs/roadmap.md` | Các mốc xuyên suốt dự án, phụ thuộc và điều kiện nghiệm thu |
| `docs/features/<ma-tinh-nang>.md` | Kế hoạch và tiến độ từng bước của một tính năng, theo mẫu |
| [docs/history/](history/README.md) | Nhật ký triển khai/review theo ngày, quyết định và liên kết commit |
| `docs/*-database.md`, `docs/*-api.md` | Hợp đồng hiện hành để lập trình và tích hợp |
| `docs/verification/` | Báo cáo tóm tắt kiểm chứng được version trong Git, kèm lệnh và môi trường |
| `artifacts/` | Log/TRX và đầu ra kiểm chứng tại máy chạy; có thể bị bỏ qua bởi Git |

Một dòng “đã triển khai” chỉ xác nhận có code. “Đã kiểm chứng” phải kèm môi trường, phạm vi, lệnh và kết quả. “Đã nghiệm thu runtime/adapter” cần bằng chứng chạy thật của runtime/adapter đó. Có workflow CI hoặc dùng mock chưa đủ để đánh dấu nghiệm thu.

Không đưa secret, token, dữ liệu khách hàng hoặc connection string thật vào tài liệu và log được commit. Báo cáo trong Git cần đủ thông tin để tái chạy dù các artifact cục bộ không còn.
