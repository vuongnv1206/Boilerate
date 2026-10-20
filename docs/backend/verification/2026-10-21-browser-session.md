# Kiểm tra browser session — 21/10/2026

Môi trường local .NET 10.0.402, SQL Server instance `.`. [Hợp đồng](../features/browser-session.md).

`BrowserSessionTests` gồm 13 ca: cookie flags trong Production/Development HTTP/HTTPS, body không chứa refresh token, header chống CSRF cho cả ba action, cookie thiếu, cookie bị từ chối so với lỗi server, restore/rotate/logout, JSON API cũ, family logout khi access đã hết hạn và phiên khác không bị thu hồi. Hai ca dùng SQL Server thật; các ca HTTP còn lại thay ITokenService để kiểm tra hợp đồng transport.

Đã chạy thêm 5 regression hiện có về issuer/audience, lockout, refresh replay/concurrency, thu hồi và các family độc lập. Lượt build đầu bị khóa DLL do Host local đang chạy; dừng đúng process Host của task rồi build lại thành công. Logs local trong `artifacts/browser-session-tests.log` và `artifacts/browser-session-regression.log`, bị ignore.

Kết quả cuối sau bổ sung userId: **13/13 đạt**, gồm xác nhận userId thật không đổi sau refresh và JSON token cũ không thêm metadata nội bộ. 5/5 regression hiện có đã đạt trước phần metadata; phần metadata không đổi rotation/validation. PostgreSQL và deployment không nằm trong kết quả này. [Kiểm tra FE và API thật](../../../../Frontend/docs/verification/2026-10-21-persistent-session.md) ghi riêng bằng chứng trình duyệt.
