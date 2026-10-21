# Phân quyền theo nhóm — hợp đồng BE

Ngày bắt đầu: 2026-10-21. Trạng thái: Đã triển khai; SQL Server đã kiểm chứng cô lập, chưa nghiệm thu deployment.

Phối hợp: [hồ sơ chung](../../shared/features/grouped-authorization.md).

## Invariant

Chỉ grant Function–Action đang có hiệu lực của các vai trò được gán mới cấp quyền. FunctionGroup không tham gia quyết định authorization. Các mutation danh mục/quyền giữ transaction, AuthorizationLock, audit và optimistic version hiện có.

## Schema và tương thích

Identity.FunctionGroups: UUID PK, Code/NormalizedCode/Name bắt buộc tối đa 100; normalized code unique; SortOrder không âm; Version 32 ký tự concurrency. Function.GroupId bắt buộc, index và FK Restrict, mỗi Function thuộc đúng một nhóm. Chỉ xóa nhóm rỗng. Migration hai provider backfill theo mã chức năng, không sửa ID/grant/user/session. Nhóm Hệ thống, Danh mục và sản phẩm, Đơn hàng, Chức năng khác được seed với ID ổn định; bootstrap mới dùng mapping này, replay giữ nhóm operator đã sửa.

## API

- GET personal/authorization: permissions và isCentralAdmin từ database, no-store; personal/permissions giữ nguyên.
- CRUD function-groups: CentralAdmin + Function.View/Create/Update/Delete; mã bất biến, mutation có version.
- Function DTO/request thêm GroupId; omitted khi tạo dùng nhóm Other, khi cập nhật giữ nhóm hiện tại.
- Role permission request giữ version và cặp FunctionId/ActionId. Cho giữ cặp inactive đang được gán, không cho cấp mới; bỏ chọn là thu hồi.

## Kiểm chứng và triển khai

Chuẩn bị migration + SQL review cho hai provider. Không chạy migration trên database đang dùng. Kiểm isolated migration fresh/upgrade, unique/FK/concurrency, bootstrap replay, inactive grants, CentralAdmin, bảo vệ admin cuối cùng và contract endpoint cũ. Trạng thái chạy thực tế cập nhật sau khi hoàn tất.

## Vận hành đã chốt

Nhóm mặc định Other được giữ để client cũ và bootstrap luôn có nơi gán Function chưa phân nhóm; không xóa nhóm này ngay cả khi rỗng. Các nhóm khác chỉ xóa khi không chứa Function. Group mutation cùng khóa ACL và audit, không cascade hay đổi grant.

`FunctionRequest` kế thừa các trường capability, thêm GroupId nullable cho tương thích. FunctionDto và role permission catalogue bổ sung GroupId; ActionDto giữ contract hiện có. Cấu hình action của Function cho giữ cặp đang bật khi Action đã inactive, từ chối thêm mới action inactive. RoleId trong body permission được route cung cấp trước service validation; request chỉ cần version/permissions như contract FE.

- Script review SQL Server: [grouped-authorization.mssql.sql](../../../scripts/migrations/grouped-authorization.mssql.sql).
- Script review PostgreSQL: [grouped-authorization.postgresql.sql](../../../scripts/migrations/grouped-authorization.postgresql.sql).

Script nâng cấp từ migration RefundsReturns; installation mới phải áp dụng toàn bộ migration theo runbook. Chưa áp dụng lên database ứng dụng. Triển khai migration và BE trước FE; endpoint personal/permissions vẫn phục vụ client cũ. Không tự chạy bootstrap/grant khi HTTP startup.

Người dùng chốt chỉ kiểm chứng SQL Server ở đợt này. Tests hai provider được giữ, PostgreSQL chưa chạy runtime. [Bằng chứng source, fixture và API/browser thật](../../../../Boilerate.Frontend/docs/verification/2026-10-21-grouped-authorization.md).
