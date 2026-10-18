# 2026-10-18 — Bước24: thiết kế gia hạn có claim/xác minh

- Tiếp tục bước4B sau Shipping. Đọc AGENTS, README docs, roadmap, MVP và quyết định digital fulfillment trước code.
- [Feature plan](../features/manual-renewals.md) ghi ownership/cardinality, bảng/cột/constraints/concurrency/xóa/migration, privacy/ACL/API và thứ tự thực hiện.
- Chọn một request/ManualExtension line Paid; khách nhập target sau thanh toán; nhân viên Start đưa PendingVerification trước thao tác ngoài, không tự nhả khi hết phiên. Verify quyền riêng lưu kết quả/hạn thật/note/evidence, không tự cộng ngày.
- Claim cùng canonical target serialize toàn shop; target/evidence bảo vệ bằng Data Protection, history metadata chống replay. Thời hạn gói/số lượng đọc snapshot đã mua.
- Bước này chỉ docs, chưa build/test/migration; không dùng326 test Shipping cũ để khẳng định renewal đã đạt.
- Tiếp theo: domain/persistence rồi service/API/tests, cập nhật history và commit theo lát.
