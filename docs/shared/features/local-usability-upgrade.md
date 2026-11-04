# Hoàn thiện Admin và storefront local — 04/11/2026

## Mục tiêu và phạm vi

Nâng UX Admin desktop/tablet và storefront gồm mobile 390×844, 360×800; giữ palette hiện hành. Dùng API có sẵn, không endpoint/schema/migration mới. Không deploy, push hoặc thay database cửa hàng.

## Trách nhiệm và kế hoạch

- FE sở hữu triển khai/tiêu chí: [hồ sơ FE](../../../../Frontend/docs/features/local-usability-upgrade.md).
- BE giữ hợp đồng [Ordering](../../backend/features/ordering-checkout.md), [guest storefront](../../backend/features/guest-storefront.md), [discounts](discount-codes.md). Không sao chép contract vào FE.
- Thứ tự: đồng bộ docs → sửa catalog/giỏ → đơn/checkout → UI/mobile → checks/browser/live SQL Server cô lập trên cùng artifact.
- Tiêu chí: không SKU cũ hoặc thành công giả, không đơn lặp, giữ draft/giỏ sau lỗi; filter/Back đúng, đơn cũ truy cập được; keyboard/touch và hai theme.

## Baseline

FE HEAD 083c78d43bf3e2ddcd737ed72ec17ef913ae922d; BE HEAD d5d684d9f6b06aed7eff9be49b17a20955434d40 cộng working tree docs/skills chưa commit và phần sửa FE ban đầu. Không coi HEAD là toàn bộ source đang kiểm tra. Fixture live bổ sung 8 mã Unique để hỗ trợ đủ desktop/tablet/mobile hai theme; không đổi API hoặc tồn kho ứng dụng. Giữ lại thay đổi chuẩn hóa; không reset hoặc commit/push.

Trạng thái: đã triển khai và kiểm chứng local theo báo cáo FE, chưa nghiệm thu deployment. Báo cáo FE cuối giữ revision/diff và hash artifact; PostgreSQL runtime, SMTP thật và staging ngoài kết quả local này.
