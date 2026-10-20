# Admin local: seed và API thật — 2026-10-20

Trạng thái: đang triển khai theo yêu cầu người dùng. Mục tiêu là chạy BE SQL Server local có dữ liệu mẫu và FE localhost:4200 dùng API thật, sau đó route/folder chia theo chức năng sản phẩm, danh mục, loại sản phẩm.

1. BE: `ICustomSeeder`, runner DI có thứ tự và lệnh `--seed-dev`; Catalog seeder dùng use cases hiện có, chạy lại không tạo trùng/ghi đè dữ liệu đã chỉnh. Giữ migration/bootstrap riêng, chỉ cho seed ở Development, không auto-seed khi HTTP khởi động.
2. Môi trường: dùng SQL Server instance local, database development riêng; không reset DB có sẵn. Local configuration/credentials bị ignore. Chạy migration, bootstrap, seed hai lần và xác nhận số lượng ổn định.
3. FE: đăng nhập/phiên/quyền qua API; base HTTP/interceptor có kiểu. Quyết định cuối: `features/collections/{products,categories,product-types}` chỉ giữ 2 màn mỗi phần (danh sách và `-io`), gộp các component nhỏ vào màn. Constants/enums/models/services/stores/forms/guards ở `src/app`, ngang cấp `features`, dùng alias import. Không còn ContentItem demo hoặc CRUD gom bằng switch collection.
4. Tích hợp: product list/detail/content edit/status và category/type list/create/edit theo hợp đồng BE; loading/error/validation/conflict rõ. API source: docs/backend/commerce-api.md và code Controllers/CommerceContracts.
5. Kiểm chứng: BE targeted tests + SQL Server seed/replay; FE format/lint/unit/build, browser web desktop/tablet hai theme; login và đọc/ghi BE thật. Mobile hoãn. Commit từng phần code + history; bảo toàn thay đổi BE có sẵn.

Không đổi schema DB nếu use cases hiện có đủ; không tạo quyền hoặc endpoint giả. Tiến độ BE tại hồ sơ backend, FE tại hồ sơ trong repo Frontend. Đây là mốc mới tiếp sau codebase UI đã hoàn tất.
