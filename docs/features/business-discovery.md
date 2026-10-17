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
| Phạm vi đã duyệt trước đây | Baseline của code Catalog v1 hiện có | Một shop, hàng vật lý, VND nguyên đồng, kho MAIN không âm. Mục tiêu nhiều shop vừa được nêu cần làm rõ; chưa suy ra baseline này là giới hạn của nền tảng tương lai |
| Catalog đã triển khai | Hợp đồng hiện hành | ProductType → Product 1–N; Product ↔ Category N–N; SKU giữ giá/tồn, custom field khác option |
| Hướng mốc sau đã duyệt | Chưa triển khai | OrderItem snapshot; Order/Payment/Shipment có trạng thái riêng; quan hệ tương lai trong database docs |
| Yêu cầu ngày 2026-10-18 | Đã rõ | Tạm ưu tiên kế hoạch nghiệp vụ; hỏi sâu theo tình huống, phát triển tài liệu song song |
| Trả lời vòng 1 và bổ sung ngày 2026-10-18 | Đã xác nhận hướng sản phẩm | Quần áo, điện thoại, laptop, PC tự build/cấu hình phức tạp; bổ sung khóa học và tài khoản. Mục tiêu gồm hàng vật lý và hàng số, còn cách bán/giao chi tiết chưa chốt |
| Trả lời vòng 1A ngày 2026-10-18 | **Đã chọn A** | Một shop tương ứng một phần mềm được triển khai/vận hành riêng. Không yêu cầu multi-tenant; không đưa SaaS/marketplace vào kế hoạch hiện tại |
| Đơn vị bán, lựa chọn cụ thể, nguồn hàng, khách, luồng mua và chính sách ngoại lệ | **Còn mở** | Chưa tự chọn COD/online, tài khoản bắt buộc, thời điểm giữ hàng hoặc tự xác nhận đơn; chưa coi PC build chỉ là một tổ hợp SKU |

Baseline không cần hỏi lại nếu tình huống thực tế vẫn phù hợp. Nếu ví dụ mới cho thấy cần thay đổi, ghi rõ tác động và xin quyết định về phạm vi trước khi sửa thiết kế. Không tự nới phạm vi chỉ vì có thể triển khai được.

## 3. Các vòng dự kiến

Hỏi từng vòng, chọn câu tiếp theo dựa trên trả lời; bảng này là lộ trình phỏng vấn, không phải danh sách yêu cầu đã được chốt.

| Vòng | Trọng tâm | Đầu ra cần chốt | Trạng thái |
|---|---|---|---|
| 1 | Ba sản phẩm thực tế: đơn vị bán, lựa chọn của khách, có sẵn/chuẩn bị theo yêu cầu | Mô hình mặt hàng và tình huống khó nhất | Đã nhận 4 nhóm sản phẩm và mục tiêu nhiều shop; chi tiết cách bán còn mở |
| 1A | Ý nghĩa nhiều shop: cài đặt riêng, SaaS cửa hàng độc lập hay sàn chung | Chốt mô hình sử dụng bắt buộc ở bản đầu trước khi thiết kế ownership | Đã chọn A: một shop/một phần mềm, không multi-tenant |
| 1B | Khóa học và tài khoản: sản phẩm thực tế, cách bàn giao/cấp quyền và khả năng học trên site | Xác định nghiệp vụ hàng số và giới hạn mốc đầu | Đã hỏi; chờ trả lời |
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

### 2026-10-18 — Vòng 1: nhận câu trả lời, mở nhánh nhiều shop

**Đã xác nhận:** người dùng muốn hỗ trợ quần áo, điện thoại, laptop và PC tự build/cấu hình phức tạp; mục tiêu là nền tảng e-commerce mở rộng cho nhiều shop.

**Còn mở:** "nhiều shop" là một bộ phần mềm cài riêng từng shop, dịch vụ SaaS có cửa hàng độc lập, hay marketplace chung cho nhiều người bán; mô hình nào phải có ở MVP. Đơn vị bán/chi tiết lựa chọn/có sẵn hay lắp theo đơn chưa được trả lời cụ thể. Chưa chốt khách hàng dùng tài khoản chung, giỏ xuyên shop, chủ sở hữu catalog, thu tiền hoặc trách nhiệm bảo hành.

**Đề xuất thứ tự làm rõ:** chốt mô hình shop trước; tiếp đó hỏi PC build là cấu hình cố định, chọn linh kiện mua ngay hay shop kiểm tra/báo giá; sau đó làm rõ ownership sản phẩm/giá/kho và luồng mua chính. Các nhánh này là câu hỏi, chưa được duyệt thành tính năng.

Vòng 1A đã hỏi ba cách sử dụng A/B/C và yêu cầu chọn mô hình cần ở bản đầu. Chưa chọn tenant/database/marketplace architecture thay người dùng. Catalog v1 hiện có vẫn là implementation cho một shop, chưa được coi là đã hỗ trợ nền tảng nhiều shop hoặc nghiệp vụ build PC.

### 2026-10-18 — Vòng 1A: chốt mô hình và bổ sung hàng số

**Đã xác nhận:** chọn A; một shop tương ứng một phần mềm, không cần multi-tenant. Mục tiêu nhiều shop là dùng lại nền tảng cho các installation riêng, không phải mặc định nhiều người bán dùng chung dữ liệu/giỏ/checkout. Bổ sung hai nhóm sản phẩm: khóa học và tài khoản.

**Còn mở:** mỗi installation có cần bán đồng thời nhiều nhóm hay cấu hình theo ngành; mốc đầu chọn nhóm nào. Khóa học được học ở site này hay giao link/quyền ở nơi khác; tài khoản là hàng chuẩn bị sẵn, cấp theo yêu cầu hay quyền có thời hạn/gia hạn. Chưa chốt tự động hay nhân viên xử lý, tồn/cấp phát hàng số, hoặc nghiệp vụ build PC.

**Đề xuất thứ tự tiếp theo:** hỏi một ví dụ khóa học và một ví dụ tài khoản, mô tả đầu ra khách nhận sau thanh toán. Phân biệt giao hàng số/cấp quyền với tổ chức việc học; chưa tự đưa LMS hoặc tích hợp cấp tài khoản vào scope.

Vòng 1B đã hỏi các trường hợp trên, chờ câu trả lời. Thiết kế hiện hành vẫn mô tả code đã làm; phạm vi chỉ hàng vật lý của Catalog v1 không được dùng để bỏ qua mục tiêu hàng số mới.

## 5. Quy tắc chuyển sang kế hoạch triển khai

Chốt được hành trình mua chính và ngoại lệ quan trọng mới lập use cases/state transitions, database cardinality/ownership/FK/constraints, API/quyền, transaction/idempotency và acceptance scenarios. Nếu còn câu trả lời có thể làm thay đổi cấu trúc nghiệp vụ, giữ mục đó mở thay vì mặc định hóa trong code.

Kiểm chứng hiện tại là đối chiếu docs/phạm vi; chưa có code/test nghiệp vụ mới. Các kết quả kiểm thử Catalog và vận hành trước đây không xác nhận những chính sách đang được hỏi ở đây.
