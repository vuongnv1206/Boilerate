# Development seeding qua ICustomSeeder — 2026-10-20

Mục tiêu: phục hồi extension point seed tái sử dụng từ template 1.0.1, phù hợp Domain/ACL hiện tại. Theo [hồ sơ điều phối](../../shared/features/local-admin-integration.md).

Phạm vi: `ICustomSeeder.InitializeAsync` và runner scoped DI chạy lần lượt theo thứ tự đăng ký; lệnh `--seed-dev` chỉ chạy Development. Theo yêu cầu người dùng, toàn bộ dữ liệu Catalog nằm trong một file `SeedData/catalog.json`; code chỉ đọc file và thêm type/category/product/variant/kho qua use cases hiện có. Seed có định danh ổn định, chạy lại không ghi đè dữ liệu đã chỉnh. Không tự cấp quyền hoặc tạo credential mặc định; foundation bootstrap riêng.

Không thay schema/quan hệ/migration; dùng model và transaction/version/audit hiện có. Runner không bọc nhiều use cases vào transaction ngoài nếu chúng sở hữu transaction. Lưu local config riêng, SQL Server mặc định cho môi trường integration; provider PostgreSQL tiếp tục được hỗ trợ trong source.

Các bước: contract/runner/CLI → Catalog seeder → unit/provider tests → chạy SQL Server local hai lần → history/bằng chứng. Hoàn tất khi seed thực chạy, lặp không trùng và server HTTP startup không tự seed.

## Kết quả hiện tại

- Đã có `ICustomSeeder`, runner, lệnh `--seed-dev` và bộ dữ liệu JSON. Thứ tự chạy theo thứ tự đăng ký DI; không thêm Name/Order.
- Đã kiểm tra file JSON nguồn và bản copy trong output của Host/tests có cùng SHA-256.
- Chạy lại trên source JSON hiện tại: **9/9 test đạt**, không bỏ qua test nào; gồm 7 ca kiểm runner/DI/JSON và 2 ca SQL Server `.`. Chưa chạy PostgreSQL trong lượt này.
- Host CLI đã đọc JSON trên database development: **0 sản phẩm mới, giữ nguyên 14 sản phẩm đã có**. Kết quả này chứng minh lần chạy lại, không thay cho kiểm thử ghi từ database trống; hai ca SQL Server đã kiểm phần đó.
- Xem [bằng chứng](../verification/2026-10-20-development-seeding.md) và [lịch sử](../history/2026-10-20-development-seeding.md). Nghiệm thu FE gọi API nằm trong hồ sơ phối hợp; không thuộc kết quả test seed.
