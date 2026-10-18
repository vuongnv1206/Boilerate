# 2026-10-18 — Bước22: API dispatch/delivered và nhận COD

- Đã viết service/controller/ACL: Shipping.View/Dispatch/Deliver, API owner và admin no-store; operation replay trước version để trả đúng shipment hiện có; tổng quantity dưới ordering gate và khóa product/balance.
- Dispatch cập nhật balance/reservation, movement, shipment, Order và audit cùng UoW. Giao thành công không tự ghi nhận tiền. ManualCOD chỉ full-total của COD đã giao hết; receipt bằng ngân hàng sau partial COD chỉ reconcile fulfillment, không tái giữ hàng đã xuất.
- Chặn Cancel khi PhysicalDispatchStarted. Chặn retry giữ lại toàn bộ nếu một đơn đã xuất bị chuyển NeedsReview trong các workflow tương lai; trả conflict để xử lý shipment/return riêng.
- Mixed order giữ nguyên snapshots/mã số và email đã có; ManualExtension không tự hoàn tất. Timestamp DTO chuẩn hóa UTC cho hai provider.
- Kiểm chứng tại bước ghi: Host build0 lỗi/1690 warnings; integration tests22 ca đã viết nhưng chưa chạy; migrations đang scaffold. Không có carrier/gateway/SMTP thật mới được nghiệm thu.
- File: Application/Ordering ShippingContracts/PaymentContracts, Commerce OrderingShipping/OrderingPayments/OrderingService/startup, Host ShipmentsController, ACL/bootstrap, ShippingIntegrationTests.
- Tiếp theo: build/test hai DB, review kết quả, cập nhật feature plan/hợp đồng/runbook và commit API riêng sau phần domain/database.
