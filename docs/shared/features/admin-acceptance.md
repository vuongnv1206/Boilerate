# Nghiệm thu admin với API thật và SQL Server cô lập

Cập nhật 2026-10-23. Đang triển khai theo kế hoạch người dùng đã duyệt. Phạm vi: Catalog, kho vật lý/kho số, Orders, Shipping/COD, hậu mãi, Renewals và quản trị hệ thống trên desktop/tablet × Light/Dark.

## Môi trường và giới hạn

Mỗi lượt chạy tạo database `boilerate_test_<guid>`, áp dụng migration, bootstrap admin, chạy development seed và bổ sung các tài khoản/kịch bản nghiệm thu. Console runner chỉ thuộc test tooling, nằm ngoài solution và CI thông thường. Kestrel dùng loopback, frontend production preview dùng proxy cùng origin `/api`; không thay production CORS hoặc thêm endpoint test.

Lệnh điều phối: `scripts/verify-admin.ps1 -FrontendRoot <absolute Frontend path>`. Node runtime theo `Frontend/run.ps1`. Logs, ảnh, trace, manifest chứa credentials và runtime keys/media không được đưa vào Git. Runner dừng process chính nó tạo, xóa database chính fixture sở hữu và kiểm tra đường dẫn trước khi dọn runtime. Báo cáo chỉ giữ thông tin không có bí mật.

Đợt này tiếp tục từ working tree hiện tại; commit theo nhóm hành vi sau kiểm chứng. SQL Server là provider nghiệm thu. Storefront, PostgreSQL runtime, SMTP/gateway/carrier thật và staging/deployment tiếp tục là mốc riêng.

## Các bước và tiêu chí hoàn tất

1. Chốt baseline và runner: browser login/reload/deep link qua cookie thật, database mới và cleanup được kiểm chứng.
2. Catalog/kho: membership add/remove/no-op và rollback batch; metadata/membership partial success giữ identity/draft; explicit reconcile giữ delta và thay đổi không liên quan; ProductType duplicate/related-product ACL; editor/ledger/replay và kho số qua API thật.
3. Orders/Shipping/hậu mãi: HTTP filter/paging/history/ACL/409, query/Back/rail canonical; prepaid và COD, giao từng phần, refund/return/restock, retry/idempotency và seed replay giữ operator edits.
4. Renewals: command thành công cập nhật detail, rail và timeline; lỗi đọc thứ cấp độc lập; response theo generation/record, PendingVerification chặn thực hiện lại; Process/Verify/Reveal kiểm độc lập.
5. System và tổng hợp: user/role/grants, bảo vệ admin cuối cùng, refresh/revoke/403/logout và password reset khi chưa cấu hình; full FE check, fixture suite, live suite và BE non-provider/SQL Server checks đạt.

Mutation không tự replay sau conflict. Retry mất response dùng cùng operation ID và payload. Hai mutation Category không có transaction chung; batch membership tự nguyên tử. Order/OrderItem giữ snapshot chỉ đọc. Schema/wire contract hiện có được giữ.

## Nguồn hợp đồng và bằng chứng

- [Catalog BE](../../backend/features/catalog-workspace.md), [Commerce API](../../backend/commerce-api.md), [Collections FE](../../../../Frontend/docs/features/live-admin-collections.md).
- [Orders BE](../../backend/features/admin-orders.md), [Shipping](../../backend/features/physical-shipping.md), [hậu mãi](../../backend/features/refunds-returns.md), [Orders FE](../../../../Frontend/docs/features/admin-orders.md).
- [Renewals BE](../../backend/features/manual-renewals.md), [Renewals FE](../../../../Frontend/docs/features/admin-renewals.md), [System FE](../../../../Frontend/docs/features/system-administration.md).

Kết quả phải ghi revision hoặc working-tree fingerprint, migration/database, lệnh, số ca đạt/lỗi/bỏ qua và giới hạn. Fixture browser, HTTP TestServer + SQL Server, Kestrel/browser live và deployment là các mức bằng chứng riêng. Chỉ đánh dấu admin đã nghiệm thu local khi toàn bộ tiêu chí của phạm vi được thực thi và đạt.

## Bằng chứng nghiệm thu trực tiếp (Execution Fingerprints)

- **Thời gian**: 2026-10-24
- **Working Tree Revisions**:
  - `Boilerate`: `ab77c3a` (+ modifications)
  - `Frontend`: `ee1eb03` (+ modifications)
- **Môi trường & Database**:
  - SQL Server `(localdb)\MSSQLLocalDB`, database cô lập tự động tạo và migrate: `boilerate_test_3dd751bd30124d9a8aaba6c5fd3bede5`.
  - Node.js runtime tương thích Angular 22 qua `Frontend/run.ps1`.
- **Lệnh thực thi nghiệm thu**:
  - `powershell -ExecutionPolicy Bypass -File Boilerate/scripts/verify-admin.ps1 -FrontendRoot D:\MyCode\BoilerateWorkspace\Frontend -SkipBuild`
- **Kết quả Live Acceptance Test Suite (Playwright)**:
  - **Tổng số tests**: 68 / 68 passed (100% GREEN)
  - **Chi tiết theo ma trận**:
    - `desktop-light`: 17 passed
    - `tablet-light`: 17 passed
    - `desktop-dark`: 17 passed
    - `tablet-dark`: 17 passed
  - **Thời gian chạy**: ~5.5 phút
  - **Phạm vi kịch bản đã nghiệm thu**:
    1. `catalog-editor`: options, blank/duplicate SKU, prices, default variant, inventory tracking, variant media images upload/persist, stable variant IDs across option renames and reloads.
    2. `physical inventory`: UI stock adjustment, ledger history, operation replay idempotency, Reserved stock preservation.
    3. `digital inventory`: Unique & Shared content write, license/secret delivery isolation, explicit reveal permissions.
    4. `category membership`: metadata, multi-product membership add/remove/no-op, rollback batch, concurrent conflict draft preservation and explicit reconcile.
    5. `orders & after-sales`: prepaid/COD, partial fulfillment/shipment dispatch, refund, return receipt, restock, canonical rail.
    6. `permissions`: independent viewing, product editing, pricing (Price.Update), stock adjustments (Inventory.Adjust), Return inspection (Return.Receive).
    7. `product type`: duplicate schema isolation, field definitions, preservation of existing product associations.
    8. `renewals`: single processor execution, verifier status recording, multi-level JSON evidence reveal, datepicker format handling.
    9. `session & auth`: protected deep-link preserve, cookie reload restore, logout revocation, basic role catalog access denial.
    10. `system & permissions`: user creation/edit, role assignment, disable account, final-admin protection guard, granular Function-Action permission configuration.
- **Static & Unit Checks (`Frontend/run.ps1 check`)**:
  - `format:check`: PASSED (Prettier clean)
  - `lint` & `test:architecture`: PASSED (43 architecture boundary rules verified)
  - `test`: PASSED (41 test files, 262 unit tests passed)
  - `build`: PASSED (Production bundle compile clean)
- **Backend Build (`dotnet build Boilerate/Boilerate.sln`)**:
  - PASSED (0 Errors)
