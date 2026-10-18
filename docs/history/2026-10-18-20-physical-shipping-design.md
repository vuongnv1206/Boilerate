# 2026-10-18 — Bước20: thiết kế giao vật lý và thu COD

- Yêu cầu: tiếp tục plan sau nhận tiền/giao số. Chọn lát4A giao vật lý; gia hạn/refund vẫn các lát tiếp nối.
- [Feature plan](../features/physical-shipping.md): Order1–NShipment; Shipment–OrderItem N–N có quantity; một movement/lần xuất/dòng; constraints/ownership/locks/xóa/migration và API/quyền. COD đã giao độc lập đã thu.
- Đọc docs README/roadmap/MVP/payment, mẫu feature và domain/persistence hiện hành trước sửa.
- File tại bước này: feature plan, history index, roadmap và MVP navigation.
- Kiểm chứng: chưa có code/test/migration mới; không dùng regression276 cũ để khẳng định shipping đạt.
- Tiếp theo: domain/persistence và migrations, sau đó API/ACL/COD và tests; mỗi commit tính năng kèm docs/history.
