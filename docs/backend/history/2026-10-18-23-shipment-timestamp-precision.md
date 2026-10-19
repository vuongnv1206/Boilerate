# 2026-10-18 — Bước23: ổn định độ chính xác timestamp shipment

- Phát hiện trong regression sau targeted62/62: replay delivery trên PostgreSQL trả timestamp đã truncate xuống microsecond, khác response tạo ban đầu ở phần tick thứ7; assertion so sánh thời điểm thật đã bắt được lỗi không ổn định này.
- Kế hoạch trước sửa: chuẩn hóa DispatchedAtUtc/DeliveredAtUtc về UTC microsecond ngay trong domain trước persist/map; không đổi timezone hoặc migration. Bổ sung test ticks cố định và dùng clock cố định có tick dư trong ca replay provider, không nới tolerance của assertion.
- Phạm vi: Domain/Shipping/Shipment.cs, ShippingDomainTests, helper/replay test ShippingIntegrationTests; các journal/bằng chứng liên quan.
- Kiểm chứng tại lúc ghi: bộ regression source trước fix còn chạy và đã báo1 failure PostgreSQL. Lượt đó kết thúc với324/325 pass,1 failure,0 skip,5m29s; build/test fix đang thực hiện, chưa khẳng định đạt. [Báo cáo từng lượt](../verification/2026-10-18-physical-shipping.md) giữ bằng chứng này.
- Tiếp theo: kết thúc lượt cũ, build lại, chạy regression đầy đủ với clock tái hiện lỗi, cập nhật báo cáo và commit cùng API chưa phát hành. Không cố tình để lỗi trong commit API rồi tạo fix khác.

Đã thực hiện sau đó: build0 lỗi/2806 warnings;3/3 ca kiểm precision/replay đạt trên hai provider với clock ticks cố định. Regression đầy đủ sau sửa đang chạy; chưa khẳng định hoàn tất trước kết quả.

Kết quả cuối: regression326/326 đạt,0 fail/skip,6m10s; có24 domain shipping (thêm ca precision) và22 provider integration, giữ assertion chính xác. Fix được chốt cùng commit API/COD; không có migration bổ sung hoặc sửa các migration lịch sử. Chi tiết tại báo cáo liên kết ở trên.
