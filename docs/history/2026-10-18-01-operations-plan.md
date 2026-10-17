# Bắt đầu nghiệm thu vận hành

Ngày: 2026-10-18, Asia/Saigon. Đây là bản ghi ngay khi thực hiện.

1. Đối chiếu roadmap: Catalog/giá/kho/media-outbox đã triển khai và kiểm thử local; runtime/Linux/container, backup/restore và adapter thật chưa đóng. Ordering/Payment/Shipping chưa triển khai.
2. Chọn [nghiệm thu vận hành](../features/operations-acceptance.md) trước checkout: tăng độ chặt gate container/proxy và native recovery drill qua hai database, giữ nguyên schema/API thương mại.
3. Probe môi trường: Docker CLI có mặt nhưng daemon pipe chưa tồn tại ở lần đầu; khởi Docker Desktop nền và kiểm tra tiếp. Không suy diễn CI hoặc Docker đã đạt.

Tiếp theo: triển khai các ca kiểm chứng, chạy được phần nào ghi đúng phần đó; lưu raw artifacts ngoài Git. Bổ sung kết quả thực tế vào verification và roadmap.
