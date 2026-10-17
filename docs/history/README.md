# Lịch sử phát triển

Mỗi bước ghi mục tiêu, thay đổi, quyết định, kiểm chứng và bước tiếp theo. Không đưa raw logs hoặc secrets vào lịch sử. Các mục ngày 2026-10-17 dưới đây là bản ghi hồi cứu cho đợt code đã triển khai trước khi áp dụng quy ước docs song song; số thứ tự là cách phân nhóm commit, không phải giờ thực hiện lịch sử được suy đoán.

| Bước | Bản ghi | Commit |
|---|---|---|
| 00 | [Thiết kế và quy trình tài liệu](2026-10-17-00-design.md) | Tra cứu bằng commit subject trong bản ghi |
| 01 | [Dependency có advisory](2026-10-17-01-dependencies.md) | Tra cứu bằng commit subject trong bản ghi |
| 02 | [Catalog, database, kho và media/outbox](2026-10-17-02-commerce.md) | Tra cứu bằng commit subject trong bản ghi |
| 03 | [Thông báo lỗi nghiệp vụ ở Production](2026-10-17-03-errors.md) | Tra cứu bằng commit subject trong bản ghi |
| 04 | [Runtime và gate Linux/container](2026-10-17-04-runtime.md) | Tra cứu bằng commit subject trong bản ghi |
| 05 | [Đóng gói template và loại test artifacts](2026-10-17-05-template.md) | Tra cứu bằng commit subject trong bản ghi |
| 06 | [Tổng hợp kiểm chứng và bàn giao](2026-10-17-06-verification.md) | Tra cứu bằng commit subject trong bản ghi |

Sau đợt này, tạo bản ghi ngay khi thực hiện từng bước và cập nhật feature plan trong cùng thay đổi. Không sửa mất kết quả cũ khi có lượt test mới; bổ sung kết quả mới cùng phạm vi source của nó.
