# Frontend — điểm điều hướng

FE hiện nằm trong repo sibling [Boilerate.Frontend](../../../Boilerate.Frontend/README.md), Angular 22/PrimeNG, có sáu màn Collections, authentication/browser session và authorization theo BE. Source và CI FE độc lập với repo này.

- [Quy chuẩn codebase và cách triển khai FE](../../../Boilerate.Frontend/docs/coding-rules.md): nguồn quy tắc duy nhất, gồm naming, components, form/store/service/utilities và ví dụ trọn luồng.
- [Bản đồ source](../../../Boilerate.Frontend/docs/architecture.md).
- [Collections](../../../Boilerate.Frontend/docs/features/live-admin-collections.md) và [giữ phiên](../../../Boilerate.Frontend/docs/features/persistent-session.md).
- [Lịch sử tính năng và bằng chứng FE](../../../Boilerate.Frontend/docs/README.md).
- [API BE](../backend/commerce-api.md) và [hợp đồng phối hợp](../shared/contracts/frontend-backend.md).

Các hồ sơ `features/history/verification` cũ trong thư mục này phản ánh mốc nền UI trước khi repo FE độc lập, không mô tả source hiện tại. Đọc docs trong repo FE cho công việc tiếp theo; không tạo bản sao tại đây. BE có Catalog và browser session trên main `9e5b410`; kết quả mock/BE tests không thay bằng chứng FE thật.
