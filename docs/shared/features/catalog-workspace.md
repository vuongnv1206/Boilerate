# Catalog workspace — điều phối BE–FE

Ngày cập nhật: 2026-10-22 (Asia/Saigon). Trạng thái: Product workspace đã kiểm chứng local; editor danh mục theo Webflow và API membership versioned đã có source/build, chưa kiểm chứng runtime/API/browser. CSV là bước sau, không chặn Catalog.

## Mục tiêu và phạm vi

### Danh mục theo Webflow — source đã triển khai, chờ kiểm chứng tích hợp

Màn sửa danh mục đi theo ảnh Webflow: Basic info (Name, Slug, URL preview, Description), ô tìm kiếm sản phẩm và chip liên kết, Item details (Parent category, thứ tự, trạng thái, ID), Activity và các thao tác cuối trang. Danh mục cha là trường mở rộng riêng của shop. Sitemap indexing/Published/Queued to publish không có trong hợp đồng hiện tại nên không giả lập.

API bổ sung một lệnh versioned batch theo delta; mỗi Product giữ nguyên các Category khác, membership được audit qua Product.Version và batch rollback toàn bộ khi một sản phẩm conflict. Không thay database vì bảng nối ProductCategories đã là nguồn quan hệ N–N. FE tải các membership hiện tại từ Products search, tìm sản phẩm ứng viên, giữ chip đã chọn qua phân trang và gửi delta cùng bước Lưu.

Thao tác cuối trang dùng khả năng hiện có: Archive đặt IsActive=false, Delete gọi xóa có kiểm tra version/tham chiếu, Duplicate tạo danh mục mới cùng thông tin và các product memberships. Kết quả không mô tả trạng thái publish hay indexing vốn chưa có API.

Các bước 1–3 đã có source: versioned command/API, chip selector/hydrate membership, layout Webflow, Parent category và thao tác tương ứng. FE production build và BE Host build đã đạt. Bước tích hợp API HTTP/browser, quyền, conflict rollback và duplicate/archive/delete còn chờ xác nhận runtime; API local đang chạy chưa được reload sang bản mới.

Tiêu chí tích hợp còn lại: tìm/thêm/bỏ nhiều sản phẩm; sản phẩm vẫn giữ các danh mục khác; version conflict rollback cả lô và giữ draft; lưu Parent cùng Name/Slug; archive/delete/duplicate chỉ báo thành công sau phản hồi API. Kiểm quyền Product.View/Product.Update độc lập với Category.View/Update.

### Quyết định triển khai 2026-10-22 — editor Webflow

Giữ nguyên icon rail, nav CMS và danh sách sản phẩm bên trái. Editor dùng nhãn tiếng Việt, control 24px, hàng option 32px, header bảng 34px và hàng variant 53px theo Webflow đã khảo sát trực tiếp. Bỏ mã nội bộ khỏi mọi form catalog; option nhập bằng thẻ với Hủy/Xong, tự tạo tổ hợp, variant mở trong dòng với Hoàn tác/Đóng và một lần Lưu chung. SKU không bắt buộc/không làm định danh. Cấu hình lưu bằng ID, có ảnh riêng, số lượng, theo dõi tồn, kích thước và vòng đời xóa giữ lịch sử. Không thêm thuế tự động, membership định kỳ hay Designer.

Trình tự: (1) cập nhật editor/form và contract full configuration; (2) transaction/domain/index/migration hai provider; (3) nối UI thật và tự tạo dữ liệu thử; (4) kiểm tra Lưu/reload, options/variants, ảnh/tồn/default, duplicate/archive/delete/category; (5) build/lint và hồi quy tập trung. Ưu tiên nghiệm thu bằng giao diện dùng database thật; không chạy lại toàn bộ E2E. Source mới đã nối database SQL Server đang chạy; nghiệm thu trực tiếp trên UI ghi ở dưới.

Triển khai catalog theo luồng quản trị Webflow, giữ nguyên nền nghiệp vụ và dữ liệu hiện có. Danh sách sản phẩm gọn, editor giữ danh sách bên cạnh, options đứng trước variants, bảng variants thể hiện từng option, giá và ảnh. Catalog gồm danh sách, lọc/sắp xếp, editor lưu chung, media, categories, product types, nhân bản và bulk status. CSV làm sau, không chặn nghiệm thu catalog.

Nguồn contract/triển khai: [Catalog BE](../../backend/features/catalog-workspace.md), [API](../../backend/commerce-api.md), [roadmap BE](../../backend/roadmap.md), [Collections FE](../../../../Frontend/docs/features/live-admin-collections.md), [kế hoạch catalog](../../backend/features/catalog-v1.md).

## Invariant và quyết định

- Giữ Product/SKU ID, quan hệ đơn/giỏ/reservation, lịch sử kho, ProductType, trường động và mọi cách giao hiện có.
- Editor lưu content/options/SKU/giá/default/gallery nguyên tử với Product.Version và ProductTypeVersion. Giá cần quyền riêng; số lượng đích và theo dõi tồn được lưu cùng cấu hình với Inventory.Adjust; reveal và nghiệp vụ kho số giữ endpoint riêng.
- Giữ Draft/Published/Archived; lưu Published có hiệu lực ngay. Không thêm bản nháp song song, hàng đợi hoặc lịch publish.
- Nhãn và SKU được sửa; ID/mã nội bộ không đổi khi đổi nhãn. Thêm trục giữ các biến thể cũ ở giá trị đầu tiên; biến thể mới kế thừa dữ liệu mặc định và có tồn 0. Xóa giá trị loại tổ hợp liên quan khỏi catalog; xóa trục tái tạo phần còn lại từ mặc định, bỏ hết trục trở về sản phẩm đơn. Các bản ghi cũ được retire để giữ đơn/reservation/lịch sử.
- Migration WebflowCatalogEditor cho SQL Server/PostgreSQL bỏ unique SKU, thêm ảnh riêng, retirement, tracking/kích thước và snapshot chính sách tồn. Không reset hoặc ghi đè dữ liệu operator. Conflict giữ nội dung nhập để đối chiếu; không tự gửi lại mutation.

## Trạng thái và kiểm chứng hiện tại

- Đã áp dụng migration SQL Server lên database local đang dùng, giữ cấu hình và dữ liệu hiện có; backend chạy tại cổng 5000, frontend tại 4200.
- Thao tác UI thật: tạo sản phẩm SKU trống, Màu sắc Đỏ/Xanh × Kích cỡ S/M, tự sinh 4 dòng; sửa giá/giá so sánh, SKU trùng, ảnh chính/ảnh bổ sung, tồn 12/không giới hạn, đổi mặc định bằng ID. Lưu rồi reload xác nhận dữ liệu. Đổi tên giá trị giữ nguyên các chỉnh sửa.
- Trên bản sao thử: tồn khởi tạo 0; xóa giá trị đã lưu còn đúng 2 dòng; xóa trục và bỏ hết trục trở về sản phẩm đơn; lưu/reload từng bước, lưu trữ rồi xóa khỏi catalog.
- Chạy build/lint và hồi quy tập trung vào cấu hình/ID/tồn/checkout-shipping không theo dõi tồn. Không dùng lại số lượng hàng trăm test của phiên bản cũ làm bằng chứng cho editor này.
- PostgreSQL có migration source; chưa chạy migration/integration mới trên PostgreSQL do thiếu BOILERATE_TEST_POSTGRES. Chưa nghiệm thu deployment ngoài local. CSV tiếp tục hoãn.

## Hợp đồng triển khai

POST create và PUT editor nhận configuration đầy đủ gồm options/values/variants/defaultVariantId bằng ID; dòng mới dùng client UUID ổn định và response trả ID thật. Mã được server tự sinh. SKU có thể trống hoặc trùng. Legacy delta endpoints giữ tương thích, nhưng client cần dùng configuration để xác định default khi SKU không duy nhất.

Giới hạn ba nhóm/50 tổ hợp mới. Sản phẩm cũ trên 50 biến thể được giữ và sửa nhãn/nội dung, không tự cắt dữ liệu. Một transaction kiểm Product/Type version, giá, ảnh, tồn đích và số đang reserved; operation ID không tạo adjustment lặp. Product.Delete gỡ sản phẩm khỏi catalog, giữ lịch sử; snapshot đơn nhận diện variant bằng ID và giữ chính sách tracking tại thời điểm checkout.

Kết quả 200 E2E/242 unit/31 provider trong các bản ghi trước thuộc mốc cũ, không phải nghiệm thu editor Webflow hiện tại.
