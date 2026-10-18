# Kiểm chứng Ordering/checkout — 2026-10-18

Asia/Saigon. Source đầu đợt `31a0109`; [hồ sơ thiết kế](../features/ordering-checkout.md). Kết quả dưới đây thuộc working tree sau source đó, không phải checkout cũ nguyên trạng.

## Môi trường và phạm vi

Windows/.NET 10, SQL Server LocalDB và PostgreSQL 16 cluster test cô lập localhost:55439. Fixture tạo/xóa database `boilerate_test_<guid>`. Raw logs/TRX/keys không commit.

## Kết quả

Chưa có kết quả kiểm chứng đầy đủ tại thời điểm tạo báo cáo. Lệnh, số ca thực sự chạy và giới hạn sẽ được bổ sung sau khi tích hợp.

## Giới hạn

Chỉ Cart/Order/reservation, xem/hủy đơn và expiry. Chưa nhận tiền, xử lý trả muộn, giao bí mật/email, gia hạn/PendingVerification hoặc Shipment. Không chạy dịch vụ/payment/carrier thật; Linux/Docker/deployment cần gate riêng.
