# Catalog workspace — điều phối BE–FE

Ngày bắt đầu/cập nhật: 2026-10-21 (Asia/Saigon). Trạng thái: Đang triển khai.

## Mục tiêu và phạm vi

Triển khai kế hoạch người dùng đã duyệt: catalog theo luồng quản trị Webflow, giữ nền nghiệp vụ và dữ liệu hiện có. Danh sách gọn, danh sách bản ghi bên editor, một lần lưu content/options/SKU/giá/gallery, bảng variants theo từng option, quản lý ảnh, categories có số sản phẩm, nhân bản và bulk status. CSV thực hiện sau, không chặn nghiệm thu catalog.

Nguồn hợp đồng/triển khai: [Catalog BE](../../backend/features/catalog-v1.md), [API](../../backend/commerce-api.md), [roadmap BE](../../backend/roadmap.md), [Collections FE](../../../../Frontend/docs/features/live-admin-collections.md).

## Invariant và quyết định

- Product/SKU identity, quan hệ đơn/giỏ/reservation, movements và kho số được giữ nguyên. Loại sản phẩm/schema và fulfillment hiện có vẫn độc lập.
- Editor lưu nguyên tử trạng thái cuối content/options/variants/default/gallery với Product.Version và ProductTypeVersion. Giá giữ quyền riêng. Kho và reveal không thuộc transaction editor.
- Giữ Draft/Published/Archived; lưu Published có hiệu lực ngay. Không tạo draft/live revision hoặc lịch publish.
- Sản phẩm đã lưu giữ nguyên axes/code/selections/SKU; thêm values và tổ hợp còn thiếu. Giữ giới hạn cấu hình hiện có.
- Không đổi schema DB cho projections/editor; không reset hoặc ghi đè seed/operator data. Không tạo cơ chế replay cho mutation catalog; sau response không rõ phải tải canonical dữ liệu để kiểm tra.

## Thứ tự và tiêu chí hoàn tất

1. BE contract/projection/save nguyên tử/bulk và kiểm invariant/rollback/quyền trên hai provider.
2. FE shared draft, workspace/list/editor/options/variants/media và hồi quy dirty/conflict/permission.
3. Categories/ProductTypes compact, duplicate/bulk, kiểm check và desktop/tablet hai theme.
4. Cập nhật tài liệu nguồn, ghi kết quả kiểm chứng thực tế và giới hạn; không coi mock/build là nghiệm thu deployment.

## Kiểm chứng

Chưa chạy tại thời điểm khởi tạo hồ sơ. Kết quả cập nhật ở hồ sơ BE/FE; không nhân bản contract hoặc tạo roadmap thứ ba.
