# Kiểm tra browser session — 21/10/2026

Môi trường local .NET 10.0.402, SQL Server instance `.`. [Hợp đồng](../features/browser-session.md).

`BrowserSessionTests` gồm 13 ca: cookie flags trong Production/Development HTTP/HTTPS, body không chứa refresh token, header chống CSRF cho cả ba action, cookie thiếu, cookie bị từ chối so với lỗi server, restore/rotate/logout, JSON API cũ, family logout khi access đã hết hạn và phiên khác không bị thu hồi. Hai ca dùng SQL Server thật; các ca HTTP còn lại thay ITokenService để kiểm tra hợp đồng transport.

Đã chạy thêm 5 regression hiện có về issuer/audience, lockout, refresh replay/concurrency, thu hồi và các family độc lập. Lượt build đầu bị khóa DLL do Host local đang chạy; dừng đúng process Host của task rồi build lại thành công. Logs local trong `artifacts/browser-session-tests.log` và `artifacts/browser-session-regression.log`, bị ignore.

Kết quả cuối sau bổ sung userId: **13/13 đạt**, gồm xác nhận userId thật không đổi sau refresh và JSON token cũ không thêm metadata nội bộ. 5/5 regression hiện có đã đạt trước phần metadata; phần metadata không đổi rotation/validation. PostgreSQL và deployment không nằm trong kết quả này. [Kiểm tra FE và API thật](../../../../Frontend/docs/verification/2026-10-21-persistent-session.md) ghi riêng bằng chứng trình duyệt.

## CI trên source đã push

Source `274d2b2e8bf5be94eaab941f57b8185861669b2e` đã push main. [Run 39466158790](https://github.com/vuongnv1206/Boilerate/actions/runs/39466158790): Windows boundaries đạt Release build và non-provider tests. Linux đã qua restore/build và bước integration tests với SQL Server/PostgreSQL của workflow; tại thời điểm ghi báo cáo đang build production container. Container/runtime/audit/template là các gate còn lại, chưa coi toàn workflow đạt.

Kết quả này bổ sung CI integration cho PostgreSQL, tách với lượt local SQL Server ở trên. [CI FE](https://github.com/vuongnv1206/Boilerate.Frontend/actions/runs/39466256951) đã đạt 148 unit tests và 44 browser tests; QA local FE đã kiểm reload sâu/hai tab/logout trên BE thật. Commit báo cáo sau source dùng `[skip ci]`.
