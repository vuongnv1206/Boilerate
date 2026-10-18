# Sản phẩm linh hoạt và đơn mua chung — phạm vi đã chốt

Cập nhật: 2026-10-18, Asia/Saigon. Nguồn quyết định: [phỏng vấn nghiệp vụ](../features/business-discovery.md). Các quyết định ở đây định hướng kế hoạch mới; không xác nhận code hiện có đã đáp ứng tất cả.

## Chủ shop cấu hình được gì

Người dùng đã chọn A: chủ shop tự tạo loại sản phẩm, bộ trường thông tin, options/SKU và chọn cách bán/giao **đã được hệ thống hỗ trợ**, khi thêm mặt hàng mới không cần sửa code chỉ để thêm tên loại hoặc trường dữ liệu.

Quần áo, điện thoại, laptop, PC, khóa học và tài khoản là các ví dụ để kiểm tra độ linh hoạt, chưa phải cam kết triển khai tất cả nghiệp vụ đặc thù vào MVP. Đặc biệt, PC chỉ là ví dụ; câu trả lời ban đầu chọn cấu hình tạo sẵn không còn được dùng để yêu cầu xây nghiệp vụ PC, BOM hoặc tồn linh kiện ở bản đầu.

Người dùng làm rõ mục tiêu mới nhất: **một phần mềm phục vụ một shop, thích ứng khi chủ shop muốn bán mặt hàng khác**, không phải xây nền tảng quản lý nhiều shop hoặc phát triển mô hình nhiều installation. Webflow là mẫu tham khảo cách quản trị sản phẩm. Chưa đưa trình tạo quy tắc/quy trình tùy ý hoặc kéo thả storefront vào phạm vi ưu tiên này.

### Cách phân tách đề xuất cho kế hoạch

| Phần | Vai trò | Ví dụ minh họa, chưa bắt buộc thêm riêng vào MVP |
|---|---|---|
| Loại sản phẩm và bộ trường | Mô tả thông tin shop cần nhập | Laptop có CPU, RAM; quần áo có chất liệu |
| Options và SKU | Các lựa chọn bán và đơn vị giữ giá/tồn | Màu/size, dung lượng; từng tổ hợp được bán có SKU |
| Cách bán/giao đã hỗ trợ | Chọn nghiệp vụ được hệ thống bảo vệ | Giao vật lý, cấp mã/tài khoản Unique, link Shared, yêu cầu gia hạn Extension |

Đây là phân tách thiết kế đề xuất, không phải schema/API mới đã được duyệt chi tiết. ProductType hiện hành quyết định bộ trường, Product–Category vẫn N–N; không suy ra tên ProductType quyết định cách thu tiền hoặc giao hàng.

Một loại mặt hàng mới có thể dùng các khả năng sẵn có; quy tắc nghiệp vụ mới cần được phân tích và bổ sung khả năng tương ứng. Ví dụ tự động trừ linh kiện hoặc kiểm tương thích PC không tự xuất hiện chỉ nhờ thêm trường CPU/GPU.

## Cùng giỏ, cùng đơn và giao riêng từng món

**Đã chọn A:** trong cùng shop, khách có thể mua áo và mã khóa học trong cùng giỏ, cùng một đơn, thanh toán một lần. Từng món được giao theo cách riêng.

Ví dụ: đơn đã nhận tiền, mã có thể được giao trong Đơn hàng của tôi trong khi áo vẫn đang vận chuyển. Tiến độ giao phải biểu diễn được kết quả của từng phần; nhận tiền và hoàn tất giao hàng là các kết quả khác nhau. Các quy tắc [gia hạn có xác minh](digital-fulfillment.md) vẫn áp dụng cho món Extension.

**Chính sách thanh toán đã chọn A:** đơn có bất kỳ hàng số nào, kể cả đơn vật lý + hàng số, phải thanh toán trước toàn bộ. COD chỉ dành cho đơn thuần hàng vật lý. Chưa chọn payment provider, nguồn xác nhận tiền hoặc mặc định tất cả đơn vật lý bắt buộc dùng COD.

Giữ hàng và trả tiền muộn đã chốt ở mục cam kết hàng bên dưới. Việc có giao phần đủ hàng khi phần khác lỗi hay không và cách hủy/hoàn từng phần sẽ xử lý ở bước liên quan. Không tự coi toàn bộ đơn đã giao xong khi một món hoàn tất.

## Những gì đã có và cần thiết kế tiếp

| Nội dung | Trạng thái |
|---|---|
| ProductType–Product 1–N; Product–Category N–N; typed fields, options/SKU, giá SKU | Đã triển khai trong Catalog v1; xem [database hiện hành](../commerce-database.md) |
| Tự cấu hình loại/trường/options qua các khả năng Catalog hiện có | Có nền tảng backend; trải nghiệm quản trị tổng thể và độ đầy đủ theo yêu cầu mới chưa được nghiệm thu |
| Chọn cách bán/giao vật lý/Unique/Shared/Extension | Đã có [cấu hình và kho số](../features/digital-inventory.md); giao theo đơn/thanh toán/gia hạn còn ở mốc sau |
| Giỏ/đơn chung vật lý + số; trả trước toàn bộ khi có hàng số | Đã có [Cart/Order/giữ hàng](../features/ordering-checkout.md); chưa có nhận tiền/giao theo đơn |
| Rule/workflow builder tùy ý, page builder, PC build/trừ tồn linh kiện | Chưa đưa vào MVP |

## Cam kết hàng đã chốt

- Khi tạo đơn thành công, giữ hàng cho khách trong một thời hạn. Khách đến sau chờ hàng được nhả nếu người trước không trả tiền.
- Đơn trả trước mặc định giữ **30 phút kể từ khi tạo thành công**, shop có thể cấu hình thời hạn.
- Nếu khách thực sự trả sau hạn, tự kiểm tra và giữ lại **toàn bộ hàng**. Đủ hàng thì tiếp tục đơn; thiếu thì chờ nhân viên xử lý/hoàn tiền. Không lấy phần đã dành cho đơn khác để bù.
- Đây là quyết định về trả tiền muộn. Thông báo về muộn cho lần trả đúng hạn cần được đối chiếu với nguồn xác nhận tiền và tình trạng hàng; chưa gán một kết quả tài chính tự động cho trường hợp này.
- Thời hạn 30 phút là cho đơn chờ trả trước. Chưa áp thời hạn đó cho COD, hàng đã trả tiền hoặc yêu cầu gia hạn đang chờ nhân viên.

Reservation đã được bổ sung cùng [Cart/Order](../features/ordering-checkout.md): Reserved vật lý và Unique owner theo dòng đơn. Xác nhận tiền/trả muộn vẫn thuộc mốc Payment tiếp theo, chưa có code nhận tiền hoặc hoàn tiền.

## Các bước và quyết định còn mở

Theo chỉ dẫn mới nhất, đây là **shop nhỏ, đủ luồng bán hàng thông thường**. Dừng mở rộng phỏng vấn ngoại lệ; dùng [kế hoạch MVP gọn](../features/shop-mvp.md) để triển khai từng lát. Các chi tiết còn mở được xử lý khi tới bước liên quan, với đề xuất đơn giản được ghi rõ; không tự coi đề xuất là quyết định đã xác nhận.

Các chi tiết còn mở: xác nhận tiền/provider; thông tin khách và phí ship; hủy/hoàn từng phần; COD; mức cấu hình cách giao ở Product/SKU. Trước mỗi bước đổi database vẫn phải ghi cardinality/ownership, cột/PK/FK/unique/check/index, concurrency, xóa và migration hai DB.

Các ca nghiệm thu dự kiến cho quy tắc đã chốt, **chưa thực thi**:

- Shop thêm một loại sản phẩm và trường thông tin bằng cấu hình, dùng các khả năng bán/giao sẵn có.
- Áo và mã cùng một đơn, trả một lần; có thể theo dõi tiến độ giao riêng.
- Đơn có hàng số không được chọn COD; đơn thuần vật lý mới đủ điều kiện xét COD.
- Đơn trả trước giữ hàng 30 phút mặc định; trả muộn kiểm tra giữ lại toàn bộ, thiếu hàng chờ nhân viên xử lý.
- Mã/tài khoản cấp đúng số lượng Unique; gia hạn chờ xác minh không được thực hiện lại trước khi có kết quả đối chiếu.

Chưa thay đổi database/API/code trong đợt phỏng vấn. Kiểm chứng docs không thay thế kiểm thử hoặc nghiệm thu vận hành.

## Tham khảo Webflow

Đã đối chiếu tài liệu chính thức ngày 2026-10-18: [Ecommerce Collections](https://help.webflow.com/hc/en-us/articles/33961314889875-Ecommerce-Collections-overview) cho thêm custom fields vào Products/Categories; [options và variants](https://help.webflow.com/hc/en-us/articles/33961334531347-Create-product-options-and-variants) mô tả các lựa chọn và tổ hợp bán hàng. Đây là nguồn tham khảo trải nghiệm quản trị, không phải yêu cầu sao chép mọi giới hạn/hành vi Webflow hoặc thiết kế database của họ.
