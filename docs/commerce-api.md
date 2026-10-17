# Vận hành và API commerce v1

Thiết kế quan hệ, khóa và kiểu cột: [commerce-database.md](commerce-database.md). Catalog mặc định tắt. Bật `Modules__Catalog__Enabled=true` rồi restart; schema luôn tồn tại dù module tắt.

## Khởi tạo

1. Cấu hình provider/connection/JWT secret của installation mới theo README.
2. Apply cả InitialFoundation và CommerceCatalog bằng EF CLI của provider đã chọn; Host không tự migrate.
3. Bootstrap admin, hoặc chạy `--register-capabilities` rồi `--grant-admin-capabilities` có chủ đích với Catalog bật. Restart không tự cấp quyền mới.
4. Login, gọi POST `/api/inventory/initialize` một lần; gọi lại trả cùng kho MAIN. Quyền `Inventory.Initialize`.
5. Tạo ProductType/schema, Category, upload media; sau đó tạo Product/SKU. Tồn ban đầu bằng 0, nhập kho bằng adjustment riêng.

Giá chỉ nhận VND nguyên đồng. Mọi mã SKU được giữ duy nhất toàn shop, kể cả SKU inactive và Product archived. ProductTypeId không đổi. Product names được phép trùng; slug duy nhất trong từng namespace Product/Category.

## Hợp đồng API

| Endpoint | Dữ liệu/chức năng | Quyền |
|---|---|---|
| GET/POST `/api/product-types`, GET/PUT `/{id}` | List, create, schema detail, edit schema | ProductType.View/Create/Update |
| GET/POST `/api/categories`, PUT/DELETE `/{id}` | Cây danh mục; DELETE có query version | Category.View/Create/Update/Delete |
| POST `/api/products/search`, GET `/api/products/{id}` | Đọc quản trị gồm mọi trạng thái | Product.View |
| POST `/api/products` | Tạo Draft + SKU + balance MAIN=0 nguyên tử | Product.Create; thêm Price.Update khi gửi giá |
| PUT `/api/products/{id}` | Thay nội dung/categoryIds/fields/gallery | Product.Update |
| POST `/api/products/{id}/status` | Draft=1, Published=2, Archived=3 | Product.Publish |
| POST `/api/products/variants/preview` | Preview tổ hợp option, chưa ghi DB | Product.View |
| POST `/api/products/{id}/variants` | Thêm tổ hợp SKU được chọn | Product.Update; Price.Update nếu gửi giá |
| PUT `/api/products/{id}/variants/{variantId}` | Giá, trạng thái active, ảnh SKU | Product.Update; Price.Update nếu đổi giá |
| PUT `/api/products/{id}/default-variant` | Chọn SKU default đang active | Product.Update |
| PUT `/api/products/{id}/options/{optionId}` | Sửa nhãn/thứ tự, thêm value | Product.Update |
| GET `/api/inventory?page=1&size=20` | Balance, version theo SKU/kho | Inventory.View |
| POST `/api/inventory/{id}/adjustments` | Điều chỉnh có operationId | Inventory.Adjust |
| GET `/api/inventory/{id}/movements` | Lịch sử bất biến, phân trang | Inventory.View |
| POST `/api/media` | Multipart `file`, JPEG/PNG <=5MiB | Product.Update |
| GET `/api/media/{id}` | Xem media quản trị | Product.View |
| GET `/api/outbox?state=4&page=1&size=20` | Vận hành pending/processing/completed/dead-letter | Outbox.View |
| POST `/api/outbox/{id}/replay` | Replay dead-letter, có audit | Outbox.Replay |
| POST `/api/catalog/products/search` | Tìm Published Products | Công khai |
| GET `/api/catalog/products/{slug}` | Chi tiết Published Product | Công khai |
| GET `/api/catalog/categories` | Category active | Công khai |
| GET `/api/catalog/media/{id}` | Ảnh có liên kết Product Published | Công khai |

Các request có ID trong body phải khớp route. Đọc lại version sau mỗi write. Stale version hoặc constraint conflict trả 409; client tải lại và quyết định thay đổi, không retry ghi đè mù quáng. Chỉ adjustment dùng operationId để replay kết quả thành công; request khác cần đọc trạng thái nếu mất response sau commit.

### Tạo schema và Product

POST `/api/product-types`:

```json
{"code":"CLOTHES","name":"Quần áo","fields":[{"code":"MATERIAL","dataType":1,"label":"Chất liệu","helpText":null,"isRequired":true,"isFilterable":true,"choices":[]}]}
```

Dùng id/version/field id từ response, sau đó POST `/api/products`:

```json
{
  "productTypeId":"<type-id>",
  "productTypeVersion":"<type-version>",
  "content":{
    "name":"Áo thun cổ tròn","slug":"ao-thun-co-tron","description":"Cotton",
    "categoryIds":["<ao-nam-id>","<hang-moi-id>"],
    "fields":[{"fieldDefinitionId":"<material-field-id>","fieldType":1,"textValue":"Cotton"}],
    "media":[]
  },
  "options":[
    {"code":"COLOR","name":"Màu","values":[{"code":"BLACK","label":"Đen"}]},
    {"code":"SIZE","name":"Size","values":[{"code":"M","label":"M"},{"code":"L","label":"L"}]}
  ],
  "variants":[
    {"sku":"SHIRT-B-M","price":150000,"compareAtPrice":180000,"options":{"COLOR":"BLACK","SIZE":"M"}},
    {"sku":"SHIRT-B-L","price":160000,"compareAtPrice":null,"options":{"COLOR":"BLACK","SIZE":"L"}}
  ]
}
```

Sản phẩm đơn giản dùng `options:[]` và một variant với `options:{}`. SKU đầu tiên là default. Nháp được thiếu giá hoặc trường required, nhưng phải có SKU default hợp lệ. Publish kiểm tra toàn bộ Product; sửa Product Published cũng phải giữ toàn bộ trạng thái hợp lệ.

PUT Product nhận `{id,version,productTypeVersion,content}`. `categoryIds`, `fields`, `media` là tập mong muốn đầy đủ; mảng rỗng tháo toàn bộ tập tương ứng. Không có giá/SKU trong endpoint này. Tháo ảnh đang được SKU tham chiếu bị từ chối; gỡ ảnh SKU trước qua endpoint variant.

POST status nhận `{id,version,status}`. Không có hard delete/trash/restore cho Product/SKU.

### Tồn kho

```json
{"inventoryItemId":"<balance-id>","version":"<balance-version>","operationId":"<new-guid>","delta":10,"reason":"Nhập lô đầu"}
```

Gửi lại đúng operationId/target/delta/reason trả movement cũ kể cả version đã cũ; khác nội dung trả 409. Giảm kho dùng delta âm; DB balance không âm. Không chỉnh quantity qua Product form, không sửa/xóa movement.

### Trường tùy chỉnh và search

FieldType 1=text, 2=number, 3=boolean, 4=date, 5=single-select. Mỗi value chỉ gửi một cột tương ứng: textValue/numberValue/booleanValue/dateValue/choiceId. Date ở dạng `YYYY-MM-DD`.

```json
{"pageNumber":1,"pageSize":20,"categoryId":"<category-id>","minimumPrice":100000,"maximumPrice":200000,"inStock":true,"fields":[{"fieldId":"<material-field-id>","operator":"eq","textValue":"Cotton"}],"optionValueIds":["<black-value-id>"]}
```

Field operators: text/boolean eq; number/date eq hoặc range với numberTo/dateTo; choice in với choiceIds. Các field kết hợp AND, mỗi field choice dùng OR trong tập lựa chọn. Giá/options/stock phải khớp cùng SKU; paging/count cùng predicate. ProductType/schema quản trị cung cấp thông tin dựng form; frontend storefront có thể lấy cấu hình bộ lọc đã chọn từ phần quản trị. Product detail trả label của field và choice để hiển thị, không yêu cầu client suy nhãn từ ID.

Giá lọc tuân theo VND nguyên đồng, từ 0 đến 9999999999999999. Giá trị lọc number phải nằm trong miền `decimal(18,4)` (trị tuyệt đối nhỏ hơn 100000000000000, tối đa 4 chữ số thập phân); API trả 400 nếu vượt miền hoặc cần làm tròn, để hai database có cùng kết quả. Lỗi quy tắc nghiệp vụ trả lý do trong `ProblemDetails.detail`; lỗi máy chủ/database không công khai chi tiết nội bộ ở Production.

## Deployment, backup và worker

Docker chạy non-root, mount `/data` bền vững; image dùng `Storage__Root=/data/files`, `DataProtection__KeysPath=/data/keys`, log stdout. Không bỏ volume khi thay container. Một replica trong v1; nhiều replica cần shared media volume hoặc một adapter storage đã nghiệm thu. Worker trong Host cần ứng dụng chạy liên tục.

Sau proxy, đặt `ReverseProxy__Enabled=true` và `ReverseProxy__KnownProxies__0=<trusted-ip>`. Header từ địa chỉ khác không được tin. CORS khác origin cấu hình `Cors__Origins__0=https://store.example`; không dùng wildcard credentials. Giữ TLS tại reverse proxy và bảo vệ quyền đọc volume chứa key.

`Commerce`: MaximumOptions=3, MaximumVariants=200, WorkerEnabled=true, WorkerIntervalSeconds=5, LeaseSeconds=120, MaximumAttempts=8. Không dùng Hangfire memory ở Production. Module Catalog tắt sẽ dừng worker và routes, giữ nguyên model/data.

Worker log định kỳ backlog, oldestAgeSeconds, deadLetters; cảnh báo nếu backlog/tuổi tăng kéo dài hoặc deadLetters >0. API outbox trả trạng thái để operator điều tra/replay sau khi sửa nguyên nhân. Replay giữ nguyên message identity và có audit. File missing được coi là đã xóa; permission denied/offline phải retry/dead-letter.

Backup database, media và Data Protection key ring cùng cửa sổ vận hành. Tạm dừng write/worker khi lấy snapshot nhất quán; kiểm thử restore trên installation cô lập, chạy migration/preflight/health, thử đọc media và session/token. Không dùng cache làm nguồn giá, kho hoặc authorization. Chỉ bật adapter SMTP/blob/OAuth/Redis sau smoke với cấu hình thực của môi trường.

## Kiểm chứng

- `dotnet test Boilerate.sln --filter "Category!=ProviderIntegration"`: domain/boundaries.
- Set `BOILERATE_TEST_MSSQL`, `BOILERATE_TEST_POSTGRES` tới server test; `dotnet test Boilerate.sln`: cả hai provider, migration và commerce integration.
- Linux: `docker build -t boilerate-ci .`, đặt `BOILERATE_CONTAINER_IMAGE=boilerate-ci`, chạy tests filter `Runtime=Container`; test dùng database/volume/container ngẫu nhiên rồi cleanup.
- `pwsh scripts/verify-template.ps1`: pack, rename, restore/build/tests và CLI migrate/bootstrap/module matrix.

Lịch sử nghiệm thu mới ở `artifacts/commerce-review/report.md`; CI được cấu hình không đồng nghĩa CI đã chạy thành công.
