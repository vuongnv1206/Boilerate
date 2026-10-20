# Phiên đăng nhập trình duyệt — 21/10/2026

Theo yêu cầu giữ đăng nhập khi reload. Không đổi database, refresh rotation hoặc JWT validation hiện có. FE tiếp tục gọi cùng origin `/api` qua proxy; không mở rộng CORS credentials.

1. Bổ sung ba action trong TokensController: `POST /api/tokens/session` nhận TokenRequest; `POST /api/tokens/session/refresh` không cần body; `DELETE /api/tokens/session` đăng xuất. Các route JSON `tokens/get` và `tokens/refresh` giữ hợp đồng cũ.
2. Browser response gồm `userId`, `token`, `sessionId`, `accessTokenExpiresAt`. UserId lấy từ người dùng đã xác thực trong TokenService, giúp FE không gửi lại thao tác của tài khoản cũ bằng tài khoản mới khi tab khác đổi đăng nhập. Metadata UserId trên TokenResponse dùng JsonIgnore để không đổi JSON cũ.
3. Refresh token chỉ ở cookie `boilerate.refresh`: host-only, HttpOnly, SameSite Strict, path `/api/tokens/session`, hết hạn theo phiên BE. Cookie Secure ngoài Development HTTP. Tất cả browser action yêu cầu `X-Requested-With: XMLHttpRequest` và trả `Cache-Control: no-store`; header kết hợp proxy cùng origin và CORS hiện tại để chặn form POST từ website khác.
4. Refresh dùng rotation/reuse detection hiện có. Không có cookie hoặc refresh bị từ chối trả 401 và xóa cookie; lỗi 5xx không xóa cookie. Logout tìm family qua hash cookie, thu hồi family kể cả access token hết hạn hoặc cookie là phiên đã được thay thế; token lạ không làm lộ trạng thái và logout có thể chạy lại.
5. Kiểm tra HTTP flags/body/CSRF/restore/logout/legacy, SQL Server family và access bị thu hồi, FE reload và nhiều tab. Raw token/cookie/credentials không đưa vào log, Git hoặc tài liệu.

Không thêm bảng, cột hoặc migration. Cookie có hạn trượt theo chính sách refresh BE hiện có; không thêm chính sách remember-me riêng. Production cần HTTPS và proxy đúng origin; cấu hình không phải bằng chứng đã nghiệm thu deployment. PostgreSQL chưa được kiểm tra trong mốc này.

Nguồn: [OWASP API CSRF headers](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html#employing-custom-request-headers-for-ajaxapi), [ASP.NET Core SameSite](https://learn.microsoft.com/en-us/aspnet/core/security/samesite?view=aspnetcore-10.0). [Hồ sơ FE](../../../../Frontend/docs/features/persistent-session.md).
