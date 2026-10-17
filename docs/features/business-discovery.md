# Kế hoạch nghiệp vụ shop — phỏng vấn và chốt phạm vi

Ngày bắt đầu/cập nhật: 2026-10-18, Asia/Saigon. Trạng thái: đang làm rõ yêu cầu; **chưa chốt kế hoạch triển khai nghiệp vụ mới**.

## 1. Mục tiêu và phạm vi

Theo yêu cầu mới, ưu tiên lập kế hoạch nghiệp vụ trước. Bắt đầu từ sản phẩm và đơn mua thực tế, làm rõ quyết định/ngoại lệ rồi mới chia lát triển khai. Các điều kiện nghiệm thu runtime trong [roadmap](../roadmap.md) vẫn được giữ, chưa được đánh dấu hoàn tất.

Đợt này chỉ phỏng vấn, ghi quyết định và lập kế hoạch. Chưa sửa code, database, migration hoặc API thương mại; chưa chọn provider thanh toán/vận chuyển.

Đầu ra cần có:

- Mô hình bán hàng, các loại khách và kịch bản mua chính có ví dụ thật.
- Quy tắc giá, nhận/xác nhận đơn, giữ/nhả hàng, thu/hoàn tiền, giao hàng và hậu mãi; có người chịu trách nhiệm và ngoại lệ.
- Phạm vi MVP/phần hoãn, luồng trạng thái và tiêu chí chấp nhận theo tình huống.
- Sau khi chốt nghiệp vụ: thiết kế cardinality/ownership/constraints/API của từng lát, thứ tự triển khai và kiểm chứng. Không coi câu hỏi hoặc đề xuất là yêu cầu đã được duyệt.

## 2. Những gì đã biết và điều cần xác nhận

| Nguồn | Trạng thái | Nội dung |
|---|---|---|
| Phạm vi đã duyệt trước đây | Baseline hiện hành | Một shop, hàng vật lý, VND nguyên đồng, kho MAIN không âm |
| Catalog đã triển khai | Hợp đồng hiện hành | ProductType → Product 1–N; Product ↔ Category N–N; SKU giữ giá/tồn, custom field khác option |
| Hướng mốc sau đã duyệt | Chưa triển khai | OrderItem snapshot; Order/Payment/Shipment có trạng thái riêng; quan hệ tương lai trong database docs |
| Yêu cầu ngày 2026-10-18 | Đã rõ | Tạm ưu tiên kế hoạch nghiệp vụ; hỏi sâu theo tình huống, phát triển tài liệu song song |
| Ví dụ sản phẩm, khách, luồng mua và chính sách ngoại lệ | **Chưa có câu trả lời trong vòng này** | Chưa tự chọn COD/online, tài khoản bắt buộc, thời điểm giữ hàng hoặc tự xác nhận đơn |

Baseline không cần hỏi lại nếu tình huống thực tế vẫn phù hợp. Nếu ví dụ mới cho thấy cần thay đổi, ghi rõ tác động và xin quyết định về phạm vi trước khi sửa thiết kế. Không tự nới phạm vi chỉ vì có thể triển khai được.

## 3. Các vòng dự kiến

Hỏi từng vòng, chọn câu tiếp theo dựa trên trả lời; bảng này là lộ trình phỏng vấn, không phải danh sách yêu cầu đã được chốt.

| Vòng | Trọng tâm | Đầu ra cần chốt | Trạng thái |
|---|---|---|---|
| 1 | Ba sản phẩm thực tế: đơn vị bán, lựa chọn của khách, có sẵn/chuẩn bị theo yêu cầu | Mô hình mặt hàng và tình huống khó nhất | Đã hỏi; chờ trả lời |
| 2 | Khách lẻ/sỉ, tài khoản, cách mua, địa bàn/kênh bán và nhu cầu shop xác nhận | Một hành trình mua chính và người tham gia | Chưa hỏi |
| 3 | Giá cam kết, giỏ hàng/checkout, phí và khuyến mãi | Giá nào áp dụng khi có thay đổi; tổng tiền được xác nhận lúc nào | Chưa hỏi |
| 4 | Nhận đơn, tồn/giữ hàng, thời hạn, hủy/sửa và cạnh tranh mua món cuối | Khi nào shop cam kết hàng; khi nào nhả; xử lý hết giữ hàng | Chưa hỏi |
| 5 | Thu tiền/xác nhận tiền, COD/chuyển khoản/online, trả thiếu/thừa/trễ và hoàn tiền | Quyền xác nhận, ngoại lệ và đối soát | Chưa hỏi |
| 6 | Chuẩn bị/giao/nhận tại shop, phí, giao lỗi và hậu mãi | Fulfillment, đổi/trả và phạm vi giao từng phần ở MVP | Chưa hỏi |
| 7 | Vận hành/quyền/phê duyệt, ưu tiên và lát cắt phát hành | MVP hoàn chỉnh, phần hoãn và tiêu chí nghiệm thu | Chưa hỏi |

Các tình huống dùng để kiểm tra giả định khi phù hợp: số lượng theo cân có phần lẻ; đặt làm/preorder; khắc tên theo dòng đơn; combo trừ thành phần; giá đổi khi đang mua; thanh toán đến sau khi hết giữ hàng. Đây là câu hỏi khám phá, **chưa phải các tính năng được thêm vào scope**.

## 4. Nhật ký phỏng vấn

### 2026-10-18 — Vòng 1 mở

Đã hỏi: chọn 3 sản phẩm muốn bán ở đợt đầu, gồm món khó quản lý nhất; mỗi món bán theo đơn vị nào, khách phải chọn gì và shop có sẵn hay cần chuẩn bị/làm theo yêu cầu.

Chưa nhận câu trả lời tại thời điểm tạo bản ghi. Mỗi vòng sau sẽ ghi riêng: **đã xác nhận / còn mở / đề xuất** cùng tình huống làm căn cứ; không tự điền chính sách thay người dùng.

## 5. Quy tắc chuyển sang kế hoạch triển khai

Chốt được hành trình mua chính và ngoại lệ quan trọng mới lập use cases/state transitions, database cardinality/ownership/FK/constraints, API/quyền, transaction/idempotency và acceptance scenarios. Nếu còn câu trả lời có thể làm thay đổi cấu trúc nghiệp vụ, giữ mục đó mở thay vì mặc định hóa trong code.

Kiểm chứng hiện tại là đối chiếu docs/phạm vi; chưa có code/test nghiệp vụ mới. Các kết quả kiểm thử Catalog và vận hành trước đây không xác nhận những chính sách đang được hỏi ở đây.
