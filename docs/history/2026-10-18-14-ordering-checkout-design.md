# 2026-10-18 — Thiết kế giỏ, đơn và giữ hàng trước triển khai

User yêu cầu tiếp tục bước 2 của [MVP](../features/shop-mvp.md), source đầu đợt `31a0109`. Đã đọc rules/index/roadmap/MVP và model/UoW/ACL hiện có.

## Quyết định và file liên quan

- [Hồ sơ Ordering](../features/ordering-checkout.md) ghi cardinality/ownership, bảng/cột/PK/FK/unique/check/index, concurrency, xóa, migration hai provider, API và thứ tự khóa trước substantive implementation.
- Một cart/customer đăng nhập; chung đơn vật lý+số; giá snapshot do server tính; expectedTotal chống thay giá âm thầm và OperationId chống checkout trùng.
- Giữ vật lý bằng Reserved, Unique bằng trạng thái/owner đơn; hết hạn/hủy nhả nguyên tử. TTL trả trước mặc định 30 phút; COD chỉ vật lý và không dùng TTL trả trước.
- Dùng khóa ordering để serialize thao tác trong shop nhỏ; worker bounded batch; không gọi dịch vụ ngoài trong transaction. Chưa có nhận tiền/giao mã/hoàn tiền/carrier/UI.
- [Docs index](../README.md), [MVP](../features/shop-mvp.md), [roadmap](../roadmap.md), [history index](README.md) và [verification](../verification/2026-10-18-ordering-checkout.md) theo dõi đợt này.

## Kiểm chứng và tiếp theo

Chỉ đối chiếu thiết kế và source tại thời điểm ghi kế hoạch; chưa có kết quả kiểm thử Ordering. Tiếp theo domain/mapping/service/worker/controller/migrations và kiểm domain/provider/concurrency/expiry/quyền. Không coi code đang viết là nghiệm thu production.
