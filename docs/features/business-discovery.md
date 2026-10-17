# Kế hoạch nghiệp vụ shop — phỏng vấn và chốt phạm vi

Ngày bắt đầu/cập nhật: 2026-10-18, Asia/Saigon. Trạng thái: **kết thúc vòng hỏi sâu hiện tại theo chỉ dẫn shop nhỏ; chuyển sang [kế hoạch MVP gọn](shop-mvp.md)**. Chưa bắt đầu code nghiệp vụ mới.

## 1. Mục tiêu và phạm vi

Theo yêu cầu mới, ưu tiên lập kế hoạch nghiệp vụ trước. Mục tiêu là quản trị sản phẩm linh hoạt như trải nghiệm Webflow: shop tự cấu hình loại/trường/options/SKU và chọn cách bán/giao đã hỗ trợ. Dùng ví dụ sản phẩm và đơn mua để làm rõ giới hạn/ngoại lệ rồi mới chia lát triển khai; không biến mỗi ví dụ thành tính năng bắt buộc. Các điều kiện nghiệm thu runtime trong [roadmap](../roadmap.md) vẫn được giữ, chưa được đánh dấu hoàn tất.

Đợt này chỉ ghi quyết định và lập kế hoạch; vòng hỏi sâu đã dừng theo chỉ dẫn mới nhất. Chưa sửa code, database, migration hoặc API thương mại; chưa chọn provider thanh toán/vận chuyển.

Đầu ra cần có:

- Mô hình bán hàng, các loại khách và kịch bản mua chính có ví dụ thật.
- Quy tắc giá, nhận/xác nhận đơn, giữ/nhả hàng, thu/hoàn tiền, giao hàng và hậu mãi; có người chịu trách nhiệm và ngoại lệ.
- Phạm vi MVP/phần hoãn, luồng trạng thái và tiêu chí chấp nhận theo tình huống.
- Sau khi chốt nghiệp vụ: thiết kế cardinality/ownership/constraints/API của từng lát, thứ tự triển khai và kiểm chứng. Không coi câu hỏi hoặc đề xuất là yêu cầu đã được duyệt.

## 2. Những gì đã biết và điều cần xác nhận

| Nguồn | Trạng thái | Nội dung |
|---|---|---|
| Phạm vi đã duyệt trước đây | Baseline của code Catalog v1 hiện có | Một shop, hàng vật lý, VND nguyên đồng, kho MAIN không âm. Hướng mới là dùng lại phần mềm cho các installation riêng và cấu hình sản phẩm linh hoạt; baseline chưa đáp ứng toàn bộ mục tiêu đó |
| Catalog đã triển khai | Hợp đồng hiện hành | ProductType → Product 1–N; Product ↔ Category N–N; SKU giữ giá/tồn, custom field khác option |
| Hướng mốc sau đã duyệt | Chưa triển khai | OrderItem snapshot; Order/Payment/Shipment có trạng thái riêng; quan hệ tương lai trong database docs |
| Yêu cầu ngày 2026-10-18 | Đã rõ | Tạm ưu tiên kế hoạch nghiệp vụ; hỏi sâu theo tình huống, phát triển tài liệu song song |
| Trả lời vòng 1 và bổ sung ngày 2026-10-18 | Ví dụ kiểm tra độ linh hoạt | Quần áo, điện thoại, laptop, PC, khóa học và tài khoản; không mặc định tất cả nghiệp vụ đặc thù bắt buộc ở MVP. Người dùng đã làm rõ PC chỉ là ý tưởng tham khảo |
| Trả lời vòng 1A ngày 2026-10-18 | **Đã chọn A** | Một shop tương ứng một phần mềm được triển khai/vận hành riêng. Không yêu cầu multi-tenant; không đưa SaaS/marketplace vào kế hoạch hiện tại |
| Trả lời vòng 1B ngày 2026-10-18 | **Đã chốt luồng giao hàng số** | Sau thanh toán thành công tự lấy mã/link/thông tin tài khoản từ kho hàng số, gán cho đơn, hiển thị trong Đơn hàng của tôi và gửi email thông báo |
| Trả lời vòng 1B ngày 2026-10-18 | Phần loại khỏi scope | Không cần trình học trực tuyến/LMS hoặc API cấp quyền bên thứ ba cho luồng giao hàng số. Payment provider/cách xác nhận thanh toán vẫn chưa chốt |
| Trả lời vòng 1C ngày 2026-10-18 | Đã chốt kịch bản cấp phát | Mã và tài khoản có sẵn là Unique, mua 2 nhận hai bản ghi khác nhau; link là Shared, mua 2 vẫn hiển thị cùng link; gói gia hạn là Extension cho cùng tài khoản |
| Trả lời vòng 1D ngày 2026-10-18 | Đã chốt cách thực hiện gia hạn | Nhân viên thực hiện ở dịch vụ ngoài; thanh toán độc lập với kết quả gia hạn. Lưu hạn thực tế đã xác minh, lịch sử/thời điểm hoàn tất/bằng chứng nếu có; chống xử lý đồng thời và thực hiện trùng |
| Trả lời vòng 1E ngày 2026-10-18 | Đã chốt ngoại lệ kết quả chưa rõ | `PendingVerification`: không được gia hạn lại cho đến khi xác định được kết quả lần trước. Người được quyền xác minh/bàn giao vẫn còn mở |
| Làm rõ PC và chọn mức flex ngày 2026-10-18 | **Đã chọn A: cấu hình quản trị** | Shop tự tạo loại/trường/options/SKU và chọn cách bán/giao sẵn có. Chưa yêu cầu rule/workflow builder hoặc kéo thả storefront. Không tiếp tục ép chốt tồn PC/linh kiện |
| Trả lời giỏ/đơn chung ngày 2026-10-18 | **Đã chọn A: cùng đơn** | Hàng vật lý và hàng số dùng chung giỏ/đơn, thanh toán một lần; từng món giao theo cách riêng |
| Trả lời chính sách COD ngày 2026-10-18 | **Đã chọn A: có hàng số phải trả trước** | Đơn có hàng số thanh toán trước toàn bộ; COD chỉ dành cho đơn thuần hàng vật lý. Chưa chọn provider hoặc cách xác nhận đã thu tiền |
| Trả lời giữ hàng ngày 2026-10-18 | **Đã chọn A** | Tạo đơn thành công thì giữ hàng trong một thời hạn; người mua sau chờ nhả hàng |
| Trả lời thời hạn ngày 2026-10-18 | **Đã chọn B: 30 phút** | Đơn trả trước giữ 30 phút kể từ tạo thành công; shop cấu hình được |
| Trả lời trả muộn ngày 2026-10-18 | **Đã chọn A** | Khách thực sự trả sau hạn: tự thử giữ lại toàn bộ; đủ thì tiếp tục, thiếu thì chờ nhân viên xử lý/hoàn tiền. Chưa quyết định thông báo về muộn cho lần trả đúng hạn |
| Chỉ dẫn mới nhất ngày 2026-10-18 | **Shop nhỏ, đủ nghiệp vụ thông thường** | Dừng hỏi quá sâu; gom kế hoạch thành các lát bán hàng/quản trị hoàn chỉnh; chi tiết còn mở xử lý khi đến bước liên quan |
| Chi tiết khách và vận hành đơn | **Còn mở, theo bước MVP** | Tài khoản bắt buộc, giữ hàng COD, thông báo thanh toán về muộn, xác nhận đơn và hủy/hoàn. Giữ từ lúc tạo đơn/thời hạn trả trước/trả tiền sau hạn đã chốt ở trên |

Baseline không cần hỏi lại nếu tình huống thực tế vẫn phù hợp. Nếu ví dụ mới cho thấy cần thay đổi, ghi rõ tác động và xin quyết định về phạm vi trước khi sửa thiết kế. Không tự nới phạm vi chỉ vì có thể triển khai được.

## 3. Các vòng dự kiến

Bảng dưới giữ lịch sử và chủ đề khám phá, không phải yêu cầu phải phỏng vấn hết. Theo chỉ dẫn mới nhất, dừng vòng hỏi sâu tại đây; các chủ đề chưa hỏi chuyển sang từng bước trong [MVP](shop-mvp.md).

| Vòng | Trọng tâm | Đầu ra cần chốt | Trạng thái |
|---|---|---|---|
| 1 | Ba sản phẩm thực tế: đơn vị bán, lựa chọn của khách, có sẵn/chuẩn bị theo yêu cầu | Mô hình mặt hàng và tình huống khó nhất | Đã nhận 4 nhóm sản phẩm và mục tiêu nhiều shop; chi tiết cách bán còn mở |
| 1A | Ý nghĩa nhiều shop: cài đặt riêng, SaaS cửa hàng độc lập hay sàn chung | Chốt mô hình sử dụng bắt buộc ở bản đầu trước khi thiết kế ownership | Đã chọn A: một shop/một phần mềm, không multi-tenant |
| 1B | Khóa học và tài khoản: sản phẩm thực tế, cách bàn giao/cấp quyền và khả năng học trên site | Xác định nghiệp vụ hàng số và giới hạn mốc đầu | Đã chốt giao từ kho sau thanh toán, My Orders/email; không LMS/API cấp quyền |
| 1C | Bản ghi kho hàng số dùng một lần hay dùng chung; ý nghĩa số lượng mua | Quy tắc tiêu thụ/cấp phát hàng số | Đã chốt Unique/Shared/Extension; giá và quyền của Shared còn mở |
| 1D | Ai thực hiện gia hạn và nguồn hạn thực tế | Tách nhận tiền với hoàn tất; lịch sử và chống xử lý trùng | Đã chốt nhân viên thực hiện, chỉ lưu kết quả đã xác minh |
| 1E | Mất kết nối sau khi đã thao tác gia hạn | Cách xử lý kết quả không xác định | Đã chốt PendingVerification và cấm làm lại khi chưa biết kết quả; quyền xác minh còn mở |
| 1F | PC cấu hình sẵn hay khách tự build; tồn máy hay linh kiện | Kiểm tra ví dụ cấu hình phức tạp | Ban đầu chọn cấu hình sẵn, sau làm rõ PC chỉ là ví dụ; không yêu cầu chốt nghiệp vụ PC ở MVP |
| 1G | Mức linh hoạt như Webflow | Ranh giới cấu hình được bởi chủ shop | Đã chọn A: loại/trường/options/SKU và chọn cách bán/giao đã hỗ trợ |
| 2 | Khách lẻ/sỉ, tài khoản, cách mua, địa bàn/kênh bán và nhu cầu shop xác nhận | Một hành trình mua chính và người tham gia | Chưa hỏi |
| 2A | Giỏ/đơn vật lý và hàng số | Mua chung hay tách đơn | Đã chọn cùng giỏ/đơn, trả một lần và giao từng món riêng |
| 2B | COD cho đơn có hàng số | Khi nào bắt buộc trả trước toàn bộ | Đã chọn có hàng số phải trả trước; COD chỉ thuần vật lý |
| 3 | Giá cam kết, giỏ hàng/checkout, phí và khuyến mãi | Giá nào áp dụng khi có thay đổi; tổng tiền được xác nhận lúc nào | Chưa hỏi |
| 4 | Nhận đơn, tồn/giữ hàng, thời hạn, hủy/sửa và cạnh tranh mua món cuối | Khi nào shop cam kết hàng; khi nào nhả; xử lý hết giữ hàng | Đã chốt phần 4A; các chi tiết khác chuyển sang MVP |
| 4A | Món cuối, thời hạn trả trước và trả sau hạn | Cam kết hàng và xử lý trả muộn | Đã chốt giữ lúc tạo đơn, mặc định 30 phút/cấu hình, trả muộn thử giữ lại toàn bộ hoặc chờ nhân viên |
| 5 | Thu tiền/xác nhận tiền, COD/chuyển khoản/online, trả thiếu/thừa/trễ và hoàn tiền | Quyền xác nhận, ngoại lệ và đối soát | Đã chốt trả trước/COD và trả sau hạn; chi tiết còn lại theo bước MVP |
| 6 | Chuẩn bị/giao/nhận tại shop, phí, giao lỗi và hậu mãi | Fulfillment, đổi/trả và phạm vi giao từng phần ở MVP | Chưa hỏi |
| 7 | Vận hành/quyền/phê duyệt, ưu tiên và lát cắt phát hành | MVP hoàn chỉnh, phần hoãn và tiêu chí nghiệm thu | Đã có chỉ dẫn shop nhỏ và kế hoạch MVP đề xuất; quyền chi tiết theo bước triển khai |

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

### 2026-10-18 — Vòng 1B: chốt đầu ra sau thanh toán

**Đã xác nhận:** sau thanh toán thành công, hệ thống tự lấy mã, link hoặc thông tin tài khoản từ kho hàng số, gán cho đơn hàng, hiển thị ở "Đơn hàng của tôi" và gửi email thông báo. Không xây trình học trực tuyến và không tích hợp API cấp quyền bên ngoài cho nghiệp vụ giao hàng số này.

**Còn mở:** nguồn xác nhận thanh toán thành công; nội dung email; khách có bắt buộc đăng nhập không; mỗi bản ghi là độc quyền một đơn hay tái dùng; mua số lượng 2 nghĩa là hai nội dung hay gia hạn; giữ hàng trước thanh toán, trường hợp đã thu tiền nhưng chưa cấp được và gửi lại thông báo. Không suy diễn email phải chứa toàn bộ thông tin tài khoản, hoặc yêu cầu này đã loại bỏ payment gateway.

**Đề xuất thứ tự tiếp theo:** chốt cách tiêu thụ bản ghi hàng số và ý nghĩa số lượng trước, sau đó dùng tình huống món cuối/hai người mua để hỏi giữ hàng và ngoại lệ thanh toán. Vòng 1C đã hỏi một lần/dùng chung và số lượng 2; chờ trả lời. Chưa thiết kế schema kho bí mật hoặc code cấp phát.

### 2026-10-18 — Vòng 1C–1E: cấp phát và gia hạn

**Đã xác nhận:** bảng Unique/Shared/Extension và số lượng 2 được lưu tại [quy tắc giao hàng số](../business/digital-fulfillment.md). Mã kích hoạt/tài khoản cấp một lần; link có thể dùng chung; gia hạn cùng tài khoản theo kết quả thực tế do dịch vụ ngoài quyết định.

Gia hạn do nhân viên xử lý, phải lưu lịch sử, thời điểm hoàn tất, hạn mới và bằng chứng nếu có. Thanh toán thành công không đồng nghĩa gia hạn thành công. Cần ngăn hai nhân viên cùng làm một yêu cầu và thực hiện trùng khi thử lại.

**Đã xác nhận ngoại lệ:** nếu không biết thao tác bên ngoài đã thành công chưa, giữ `PendingVerification`, không được gia hạn lại trước khi đối chiếu. Câu hỏi về ai được xác minh/bàn giao chưa được trả lời; giữ mở cho vòng vận hành/quyền, không mặc định chỉ quản lý.

**Còn mở:** giữ hàng Unique, giá/quyền của Shared, tài khoản đích, kết quả gia hạn không khớp hoặc thất bại, hoàn tiền, quyền truy cập nội dung. Chưa chọn công thức tự tính hạn hoặc thiết kế schema thay cho quy tắc đã được xác nhận.

### 2026-10-18 — Làm rõ flex, ví dụ PC và đơn mua chung

Người dùng ban đầu chọn cấu hình PC tạo sẵn, sau đó làm rõ PC chỉ là một đề xuất và mục tiêu thực sự là sản phẩm linh hoạt như Webflow. **Chỉ dẫn mới thay thế cách hiểu PC là nghiệp vụ bắt buộc MVP**; dừng câu hỏi tồn máy/linh kiện và giữ nó như một khả năng tương lai chưa yêu cầu triển khai.

**Đã chọn A về flex:** shop tự tạo loại sản phẩm, trường thông tin, options/SKU và chọn cách bán/giao hệ thống đã hỗ trợ. Đây là ưu tiên quản trị dữ liệu; chưa mở rộng sang rule/workflow builder tùy ý hoặc page builder. Phân tách bộ trường, lựa chọn bán và cách giao là đề xuất thiết kế cần cụ thể hóa, không đồng nhất loại sản phẩm với nghiệp vụ giao.

**Đã chọn A về hành trình mua:** hàng vật lý và hàng số chung giỏ/đơn, thanh toán một lần, từng món giao riêng. **Đã chọn A về nhận tiền:** có hàng số phải trả trước toàn bộ; COD chỉ dành cho đơn thuần vật lý. Chi tiết tại [phạm vi sản phẩm linh hoạt](../business/product-flexibility.md).

**Còn mở:** thời điểm cam kết/giữ hàng, thanh toán muộn, giao lỗi từng phần, hủy/hoàn, tài khoản khách và mức cấu hình ở Product/SKU. Chưa chọn provider, state machine, schema hoặc đưa toàn bộ ví dụ sản phẩm vào MVP.

### 2026-10-18 — Giữ hàng và chuyển sang MVP cho shop nhỏ

**Đã xác nhận:** giữ hàng từ khi tạo đơn thành công; đơn trả trước mặc định 30 phút, shop cấu hình được. Nếu khách thực sự trả sau hạn, kiểm tra giữ lại toàn bộ hàng: đủ thì tiếp tục, thiếu thì chờ nhân viên xử lý/hoàn tiền. Chưa dùng quyết định này để suy ra chính sách COD hoặc thông báo trả đúng hạn nhưng về muộn.

**Chỉ dẫn mới nhất:** e-commerce nhỏ, đủ nghiệp vụ cho một shop bình thường, không cần hỏi quá kỹ. Dừng mở thêm vòng ngoại lệ. Đã lập [MVP](shop-mvp.md) từ cấu hình cách giao/kho số → đơn/giữ hàng → nhận tiền/giao số → admin/giao vật lý/hủy/hoàn → kiểm chứng. Giao vật lý và hoàn tiền do admin xử lý đơn giản là đề xuất cho MVP, chưa tự chọn adapter hoặc chính sách thay người dùng.

## 5. Quy tắc chuyển sang kế hoạch triển khai

Hành trình chính đủ để lập kế hoạch MVP. Thiết kế use cases/state transitions, database cardinality/ownership/FK/constraints, API/quyền, transaction/idempotency và acceptance scenarios theo từng lát. Các câu trả lời còn mở được giữ trong bước liên quan, không buộc phải phỏng vấn toàn bộ trước khi thiết kế lát đầu. Không tự coi đề xuất chính sách là quyết định đã xác nhận.

Kiểm chứng hiện tại là đối chiếu docs/phạm vi; chưa có code/test nghiệp vụ mới. Các kết quả kiểm thử Catalog và vận hành trước đây không xác nhận những chính sách đang được hỏi ở đây.
