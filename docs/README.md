# Tài liệu dự án

Chọn khu vực theo công việc. BE và FE có kế hoạch, lịch sử và bằng chứng riêng; nghiệp vụ và quy trình dùng chung chỉ có một nguồn.

| Khu vực | Bắt đầu ở đâu | Nội dung |
|---|---|---|
| Backend — BE | [Index BE](backend/README.md), [roadmap BE](backend/roadmap.md) | Database/ERD, API, domain, feature plans, vận hành, lịch sử và kiểm chứng BE |
| Frontend — FE | [Index FE](frontend/README.md), [roadmap FE](frontend/roadmap.md) | Điều hướng tới repo FE độc lập, có Collections, session và quyền theo BE |
| Dùng chung | [Index shared](shared/README.md) | Nghiệp vụ đã chốt, nguồn quyết định, phối hợp BE–FE, workflow và mẫu kế hoạch |

## Luồng đọc

1. Đọc [phạm vi sản phẩm linh hoạt](shared/business/product-flexibility.md) và [giao hàng số/gia hạn](shared/business/digital-fulfillment.md) để hiểu shop cần gì.
2. Đọc roadmap của phần sắp làm và hồ sơ tính năng liên quan; trạng thái triển khai nằm ở đó.
3. Khi tích hợp, đọc [hướng dẫn BE–FE](shared/contracts/frontend-backend.md) rồi dùng [API BE hiện hành](backend/commerce-api.md). Không lấy ERD làm hợp đồng request/response.
4. Phát triển theo [quy trình chung](shared/development-workflow.md), cập nhật contract tại nguồn; history chỉ ghi mốc tính năng, verification chỉ giữ bằng chứng tích hợp cần thiết.

## Quy ước tài liệu

- BE và shared giữ tài liệu ở repository này; tài liệu FE hiện hành ở [repo FE](../../Boilerate.Frontend/docs/README.md). Không nhân bản quy chuẩn, tiến độ hay evidence FE tại hai repo.
- `shared/business/` lưu quy tắc nghiệp vụ; `shared/contracts/` lưu cách phối hợp và dẫn tới hợp đồng kỹ thuật chính thức. Không sao chép danh sách endpoint hay bảng tiến độ BE vào tài liệu FE.
- Thay đổi dùng chung có hồ sơ trong `shared/features/`, history và verification trong `shared/`. Một tính năng chạm cả BE và FE có một hồ sơ điều phối dùng chung, liên kết các bước của hai bên.
- [Mẫu kế hoạch](shared/templates/feature-plan.md) dùng khi tính năng lớn cần chốt contract/thiết kế. Không bắt tạo plan, dated history hoặc report cho từng bước refactor/test.
- [AGENTS.md](../AGENTS.md) chứa toàn bộ hướng dẫn agent: phần nguyên tắc chung và phần quy ước riêng của repository. Không duy trì file rule riêng theo công cụ.
- `plan.md` là [điểm điều hướng](../plan.md), không phải một roadmap khác.

“Đã triển khai”, “đã kiểm chứng local” và “đã nghiệm thu deployment/adapter” là ba trạng thái khác nhau. Kết quả BE không nghiệm thu FE. Mock hoặc cấu hình CI không chứng minh chạy thật.

## Tìm tài liệu trước lần tổ chức lại

Ngày 2026-10-19, tài liệu BE cũ ở `docs/` được chuyển vào `docs/backend/`; nghiệp vụ, discovery, workflow và template được chuyển sang `docs/shared/`. Xem [hồ sơ tổ chức tài liệu](shared/features/documentation-structure.md) và [lịch sử dùng chung](shared/history/README.md).

Tên file của các feature plans, history và verification cũ được giữ. Các lệnh/kết quả đã chạy trong báo cáo cũ giữ đường dẫn tại thời điểm chạy; liên kết đọc tài liệu đã được cập nhật. Dùng `git log --follow -- <đường-dẫn-mới>` để tra lịch sử trước khi di chuyển.
