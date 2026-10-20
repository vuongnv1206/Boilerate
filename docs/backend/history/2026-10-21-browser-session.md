# 21/10/2026 — Phiên trình duyệt qua HttpOnly cookie

[Hồ sơ](../features/browser-session.md) ghi hợp đồng và giới hạn. Thêm browser login/refresh/logout trong TokensController, dùng TokenService và UserSessions hiện có. Không đổi JSON token API cũ hoặc schema.

Cookie refresh được server đặt/xóa, access token chỉ trả trong response. Browser response có userId ổn định để FE ràng buộc refresh với đúng tài khoản. Logout thu hồi theo refresh cookie ngay cả khi access token không dùng được. Header riêng và CORS cùng origin bảo vệ browser session mutations.

[Kiểm chứng](../verification/2026-10-21-browser-session.md) tách HTTP contract, SQL Server và QA trình duyệt. FE chịu trách nhiệm chờ khôi phục trong guard, giữ access token trong bộ nhớ và điều phối các tab; deployment chưa nghiệm thu.
