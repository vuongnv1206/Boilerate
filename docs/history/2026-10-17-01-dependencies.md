# 01 — Dependency có advisory

Ngày ghi: 2026-10-17, Asia/Saigon. Bản ghi hồi cứu.

1. Xác định MessagePack transitive của SignalR Redis cần bản vá cho GHSA-qhrr-8q5h-9q3h.
2. Pin 2.5.303 qua Directory.Packages.props và PackageReference trực tiếp ở Infrastructure, không nâng major hàng loạt.
3. Cập nhật cả năm lock files phụ thuộc: Infrastructure, Host, hai Migrators và Tests.

Kiểm chứng đã có: locked restore thành công; audit package lưu ở artifacts/commerce-audit.json ghi 0 packages có advisory tại thời điểm kiểm tra. Đây là kết quả lịch sử, không bảo đảm trạng thái advisory ở một thời điểm tương lai. Xem [bảng kiểm chứng](../verification/2026-10-17-commerce.md).

Bước tiếp theo: Catalog domain, mappings/migrations, API và các kiểm thử đi cùng thiết kế database.

Commit subject: `fix(deps): pin patched MessagePack and refresh lock files`.
