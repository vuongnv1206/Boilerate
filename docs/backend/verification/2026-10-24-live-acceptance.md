# Verification — Live Acceptance Suite & Admin Acceptance — 2026-10-24

## Phạm vi

Kiểm chứng toàn diện Live Acceptance kết nối Kestrel backend và frontend browser trên cơ sở dữ liệu SQL Server cô lập (`boilerate_test_<hash>`), chạy qua runner `verify-admin.ps1`.
Bao gồm: Catalog Editor, Category Membership, Digital/Physical Inventory, Orders/Shipping/Returns, Manual Renewals, Function-Action Permissions và User Administration.

## Kết quả

| Lệnh | Môi trường | Kết quả |
| --- | --- | --- |
| `dotnet build Boilerate/tests/Boilerate.LiveAcceptance/Boilerate.LiveAcceptance.csproj -c Release` | .NET 10, Windows Release | Pass; 0 error, 0 warning. |
| `powershell -ExecutionPolicy Bypass -File Boilerate\scripts\verify-admin.ps1 -FrontendRoot Frontend -SkipBuild` | Windows, LocalDB / SQL Server, 4 browser profiles | Pass: **68 / 68 live acceptance tests (100% green)**. |

## Các điểm đã xác nhận trong mã nguồn BE

1. **Gia hạn và mã hóa ký tự tiếng Việt (`OrderingRenewals.cs`):**
   - Đã cấu hình `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` cho `JsonSerializerOptions` trong quá trình mã hóa/giải mã `ProtectedEvidence`, ngăn việc biến đổi ký tự UTF-8 tiếng Việt thành mã thoát `\uXXXX`.
2. **Khóa đồng quy người dùng (`UserService.cs`):**
   - Ánh xạ tĩnh Mapster cho `UserDto.Version` từ `ApplicationUser.ConcurrencyStamp` khi thực thi endpoint `/api/users/search`, ngăn lỗi `409 Conflict` không mong muốn khi cập nhật hồ sơ người dùng.
3. **Thứ tự biến thể và biến thể mặc định (`CommerceQueries.cs`):**
   - Phản ánh đúng thứ tự `OrderByDescending(v => v.IsDefault).ThenBy(v => v.Id)` cho DTO `Variants`.
4. **Cô lập và dọn dẹp cơ sở dữ liệu kiểm thử (`Boilerate.LiveAcceptance`):**
   - Tạo DB ngẫu nhiên `boilerate_test_<guid>`, tự động áp dụng toàn bộ migration, seed dữ liệu mẫu chấp nhận và dọn dẹp an toàn khi dừng host.
