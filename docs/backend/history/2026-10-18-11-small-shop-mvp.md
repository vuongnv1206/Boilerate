# 2026-10-18 — Giữ hàng và kế hoạch MVP cho một shop nhỏ

## Mục tiêu và quyết định

Người dùng xác nhận giữ hàng khi tạo đơn thành công; đơn trả trước mặc định 30 phút và shop có thể cấu hình. Nếu thực sự trả tiền sau hạn, tự kiểm tra giữ lại toàn bộ: đủ hàng tiếp tục, thiếu thì chờ nhân viên xử lý/hoàn tiền. Chưa gán quy tắc này cho COD hoặc thông báo về muộn của lần trả đúng hạn.

Chỉ dẫn mới nhất: e-commerce nhỏ, đủ nghiệp vụ cho một shop bình thường, không cần quá chi tiết. Dừng phỏng vấn sâu ngoại lệ; gom thành kế hoạch từng lát thay vì yêu cầu trả lời hết mọi trường hợp trước.

## Thay đổi

- Tạo [kế hoạch MVP](../features/shop-mvp.md): cách giao/kho số → Cart/Order/reservation → nhận tiền/giao số → admin/giao vật lý/hủy/hoàn → kiểm chứng. Chọn cách giao sẵn có, không phát triển rule builder/page builder/PC BOM.
- Cập nhật [phạm vi/quy tắc giữ hàng](../../shared/business/product-flexibility.md), [hàng số](../../shared/business/digital-fulfillment.md), [feature discovery](../../shared/features/business-discovery.md), [roadmap](../roadmap.md), [docs index](../README.md), [history index](README.md) và [plan navigation](../../../plan.md).
- Giữ rõ quyết định đã xác nhận với đề xuất đơn giản cho shipping/refund/khách hàng; chưa tự chọn provider hoặc chính sách thay người dùng.

## Kiểm chứng

Đã đối chiếu câu trả lời trực tiếp và review độc lập tài liệu; sửa các câu hiện trạng còn coi giữ hàng là chưa chốt, làm rõ thời hạn 30 phút chỉ cho đơn trả trước. `git diff --check` đạt; lượt cuối kiểm tra 38 file Markdown/168 relative links đạt. Chỉ sửa docs, chưa code/migration/API mới; không chạy build/test ứng dụng. Các tiêu chí trong MVP là mục tiêu, chưa phải bằng chứng thực thi hoặc nghiệm thu.

## Còn lại và bước tiếp

Bước BE tiếp theo được đề xuất là thiết kế cách giao và kho số bằng một feature plan ngắn, có ownership/cardinality/constraints/API; sau đó mới code. Chi tiết Order/Payment/Shipping làm ở bước liên quan; các gate runtime/adapter vẫn cần hoàn tất trước vận hành thật.

Các commit tài liệu trước đã tồn tại: `2cd1120` (cấp phát/gia hạn), `8836fc9` (sản phẩm linh hoạt và đơn chung).
