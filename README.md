# Boilerate

Backend template .NET 10: Identity, quyền Function–Action từ database, nhiều phiên đăng nhập, audit, Catalog tùy chọn và adapters mail/storage/cache/jobs. Có API quản trị; giao diện Catalog nằm trong repo sibling [Boilerate.Frontend](../Boilerate.Frontend/README.md), có sáu màn Collections và browser session.

## Kế hoạch và lịch sử phát triển

[AGENTS.md](AGENTS.md) là nguồn hướng dẫn agent duy nhất: gồm nguyên tắc làm việc chung và quy ước tài liệu/phát triển riêng của repository.

Code và tài liệu được phát triển song song theo [quy trình chung](docs/shared/development-workflow.md), có quy ước trong [AGENTS.md](AGENTS.md). [Docs index](docs/README.md) tách [BE](docs/backend/README.md), [FE](docs/frontend/README.md) và [nghiệp vụ/quy trình chung](docs/shared/README.md). Tiến độ BE nằm tại [roadmap BE](docs/backend/roadmap.md), [hồ sơ tính năng](docs/backend/features/shop-mvp.md), [history](docs/backend/history/README.md) và [báo cáo kiểm chứng](docs/backend/verification/2026-10-19-aftersales.md). FE giữ [quy chuẩn và tài liệu hiện tại trong repo riêng](../Boilerate.Frontend/docs/README.md). Tính năng lớn dùng [mẫu kế hoạch](docs/shared/templates/feature-plan.md); lịch sử chỉ ghi mốc tính năng, chi tiết code tra Git. Đọc [hướng dẫn phối hợp BE–FE](docs/shared/contracts/frontend-backend.md).

## Cấu trúc và context

Domain giữ entity/sự kiện; Application giữ use case/DTO/validation/interfaces; Infrastructure triển khai persistence/Identity/adapters; Host compose HTTP/SignalR. Migrators.MSSQL và Migrators.PostgreSQL dùng chung model, mỗi provider có tám migrations riêng: InitialFoundation, CommerceCatalog, DigitalInventory, OrderingCheckout, PaymentDigitalDelivery, PhysicalShipping, ManualRenewals và RefundsReturns. Tests giữ regression và integration trên database thật.

`ICurrentUser` đọc actor; initializer internal nằm trong Infrastructure, dùng tại middleware/ranh giới thực thi. Hai interface cùng instance scoped. `SetCurrentUserId` phục vụ attribution, không tạo authentication; job chưa tự truyền actor. Hub đọc `Context.User`. Claims role trong JWT là thông tin phiên; quyền hiện tại lấy từ DB. Async validators dùng chung DbContext chạy tuần tự.

## Khởi tạo installation mới

Cài SDK theo global.json; chuẩn bị SQL Server hoặc PostgreSQL. Không có tài khoản/khóa/connection mặc định dùng chung. Thay các giá trị `<...>` dưới đây; không đưa secret vào source.

```powershell
dotnet tool restore
dotnet restore Boilerate.sln --locked-mode
dotnet build Boilerate.sln --no-restore
$env:DatabaseSettings__DBProvider = "postgresql" # hoặc mssql
$env:DatabaseSettings__ConnectionString = "<connection database mới>"
$env:SecuritySettings__JwtSettings__Key = "<khóa ngẫu nhiên riêng ít nhất 32 byte UTF-8>"
# Chỉ chạy lệnh của provider đã chọn:
dotnet ef database update --project src/Migrators.PostgreSQL --startup-project src/Host -- --provider postgresql
# SQL Server tương ứng:
# dotnet ef database update --project src/Migrators.MSSQL --startup-project src/Host -- --provider mssql
$env:BOILERATE_BOOTSTRAP_EMAIL = "admin@gmail.com" # local Development default
$env:BOILERATE_BOOTSTRAP_PASSWORD = "Abcd@1234" # local Development default
dotnet run --project src/Host --no-launch-profile -- --bootstrap-admin
Remove-Item Env:BOILERATE_BOOTSTRAP_PASSWORD
dotnet run --project src/Host --no-launch-profile
```

Development có thể dùng `dotnet user-secrets set --project src/Host "DatabaseSettings:ConnectionString" "<connection>"` và tương tự cho JWT key/provider. Ưu tiên: JSON < development secrets < environment. EF factory chỉ đọc connection/provider environment hoặc provider argument, không khởi động Host/adapters và không lấy secret từ JSON.

Host **không migrate/seed** khi phục vụ. Swagger Development ở `/swagger`; health `/health/live` và `/health/ready`. Database provider/cờ module được chốt lúc startup; thay đổi cần restart.

## Baseline mới và lệnh operator

Từ quyết định ngày 2026-10-16, template **chỉ hỗ trợ database tạo mới**. Lịch sử thử nghiệm cũ đã được gộp thành một `InitialFoundation` cho mỗi provider. Không chạy baseline này vào database đã có schema/lịch sử cũ; source không còn đường nâng cấp installation cũ hoặc ETL tự động. Không có database hiện có nào bị xóa trong quá trình dọn source.

Áp dụng migration riêng trong bước triển khai, rồi bootstrap admin đầu tiên theo hướng dẫn trên. Host không tự migrate/seed. Sau bootstrap, có thể kiểm tra tính toàn vẹn hoặc đăng ký/cấp quyền có chủ đích:

```powershell
dotnet run --project src/Host --no-launch-profile -- --preflight
dotnet run --project src/Host --no-launch-profile -- --register-capabilities
dotnet run --project src/Host --no-launch-profile -- --grant-admin-capabilities
```

`--preflight` kiểm tra schema hiện tại sau migrate: Code/NormalizedCode, mã trùng/sai, grant thiếu cặp và số lượng bản ghi. Chạy LINQ trên cả hai provider, không suy Code từ nhãn Name và không sửa dữ liệu. Register chỉ tạo định nghĩa/cặp explicit, không cấp grants. Grant là quyết định operator riêng; restart không sửa quyền. Bootstrap chỉ nhận Identity/ACL rỗng, email/password qua environment.

`--design-only` chỉ cho scaffolding/script offline, connection thay thế cố tình không kết nối được; apply migration phải có connection explicit. Sau mỗi đổi model, tạo migration cho **hai provider**, vì EF chỉ scaffold provider đang chọn ([Microsoft](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers)). Chuyển provider không chuyển dữ liệu hoặc hàng đợi jobs.

## Phân quyền và API quản trị

Role là nhóm quyền; user nhận hợp grants từ nhiều role. Actions/Functions là danh mục, ActionInFunctions là cặp hợp lệ, Permissions là grant cho role. Code ASCII ổn định, NormalizedCode uppercase duy nhất, Name sửa được. Giữ User/Users và Role/Roles riêng. Thêm dòng DB không tự tạo endpoint/nghiệp vụ.

Authorization yêu cầu user active, membership hiện tại, grant chưa xóa mềm, definitions/cặp đều active. Kết quả nhớ trong scoped request, không cache giữa requests. Endpoint khai báo `Permissions.{FunctionCode}.{ActionCode}`. Quản trị yêu cầu membership Admin DB và quyền tương ứng. Sentinel row lock + transaction bảo vệ admin cuối cùng; versions chống ghi đè đồng thời. Grant diff giữ nguyên row không đổi, cấp lại restore soft-delete. Request sai rollback; audit trước/sau. Quyền chức năng vẫn cần kiểm tra sở hữu dữ liệu.

| API | Hợp đồng |
|---|---|
| GET/POST `/api/actions`, GET/PUT/DELETE `/api/actions/{id}` | danh mục thao tác |
| GET/POST `/api/functions`, GET/PUT/DELETE `/api/functions/{id}` | danh mục chức năng |
| PUT `/api/functions/{id}/actions` | `{version,activeActionIds:[guid]}` |
| GET/POST `/api/roles`, GET/PUT/DELETE `/api/roles/{id}` | nhóm quyền |
| GET/PUT `/api/roles/{id}/permissions` | `{version,permissions:[{functionId,actionId}]}` thay tập mong muốn |
| POST `/api/users/search`, GET `/api/users/{id}` | phân trang/chi tiết có version |
| PUT `/api/users/{id}/roles` | `{version,roleIds:[string]}`; không nhận role name |
| POST `/api/users/{id}/toggle-status` | active/inactive có version |

Update definition gửi `{code,name,isActive,version}`; Code giữ nguyên. DELETE definition/role gửi `?version=<current>`. Version role lấy từ GET role, user lấy từ GET user. Tham chiếu cả grant lịch sử không xóa vật lý; trả 409 hoặc disable. Khi 409 phải tải lại rồi quyết định thay đổi, không retry ghi đè mù quáng.

## Account và sessions

POST `/api/tokens/get`: `{email,password}`; POST `/api/tokens/refresh`: `{refreshToken}`. Access 15 phút, refresh 30 ngày mặc định; DB chỉ giữ SHA256. Refresh xoay nguyên tử một lần; reuse revoke family. Mỗi login family độc lập. Access đã phát trong family dùng đến expiry nếu family chưa revoked.

GET `/api/tokens/sessions`, DELETE `/api/tokens/sessions/{id}`, DELETE `/api/tokens/sessions` chỉ thao tác phiên của mình. JWT kiểm tra issuer/audience/sid và account/session DB mỗi request. Disabled/lockout/revoked bị chặn; đổi/reset password revoke all trong transaction. Password mới tối thiểu 9; password hash cũ vẫn dùng được. Lockout mặc định 5 lần/15 phút, cấu hình SecuritySettings:Lockout:MaxFailedAccessAttempts/DurationMinutes; rate limit cấu hình SecuritySettings:AuthenticationRequestsPerMinute theo remote IP. Khi sau proxy, deployment cần trusted forwarded headers, không tin X-Forwarded-For tùy ý.

Self-registration/password recovery mặc định tắt. Nếu bật confirm/recovery, cần PublicBaseUrl, SMTP và durable Hangfire. Google ID token kiểm tra audience; Facebook kiểm tra app/user debug_token. Email trùng không tự link account: login account hiện tại rồi gọi `/api/auth/google/link` hoặc `/api/auth/facebook/link`. Account mới nhận Basic không có grants mặc định. Lỗi production ProblemDetails có code/traceId, không exception internals.

## Module/adapters

| Cấu hình | Mặc định / hành vi |
|---|---|
| DatabaseSettings:DBProvider | mssql hoặc postgresql, Npgsql EF 10.0.3 |
| Modules:Catalog:Enabled | false; bỏ routes/Swagger, giữ EF model/schema/data/grants |
| Modules:Notifications:Enabled | false; bật API, `/hubs/notifications` và dispatcher |
| HangfireStorageSettings:Enabled | false; enqueue báo lỗi rõ ràng khi tắt |
| HangfireStorageSettings:StorageProvider | sqlserver/postgresql connection riêng bắt buộc; memory chỉ dev |
| HangfireStorageSettings:EnableDashboard | false; bật cần auth + quyền DB Hangfire.View, không hostname bypass |
| BlobStorageSettings:Enabled | false; Azure/Aws khi cấu hình; bỏ local chưa triển khai |
| CacheSettings:UseDistributedCache | false; memory; Redis tùy chọn |

Catalog không phụ thuộc jobs/notifications. Notifications snapshot người nhận user/role/broadcast; receipt `(NotificationId,UserId)` giữ read/hide riêng. Trạng thái đọc/ẩn chỉ nằm ở receipt; không có cột đọc chung trên Notification. Dispatcher phát sau commit, retry chưa gửi; client deduplicate ID và lấy history qua HTTP. Query JWT chỉ nhận `/hubs/notifications`; Hub chỉ tham gia group của session trong JWT. Dispatcher truy vấn lại các session còn hiệu lực trước khi gửi, loại phiên revoked/expired và account không hợp lệ. Kết nối đóng khi access hết hạn. Không còn Hub method thao tác dữ liệu nghiệp vụ.

Local upload giữ JPEG/PNG ≤5 MiB theo số byte thực đọc, xử lý stream short-read và xóa file upload dở. `/files/...` kiểm tra owner/quyền rồi mở đường dẫn avatar đã lưu trong DB; có `nosniff`. Chặn traversal, alternate streams và symbolic link/junction. Deployment phải giữ thư mục Files thuộc quyền ứng dụng, không cho tiến trình không tin cậy thay directory đồng thời. Media Product dùng asset ID/gallery và durable outbox cleanup; upload bỏ dở có intent trong DB để sweeper thu gom sau TTL. Xem vòng đời và quy tắc khóa tại [API/vận hành commerce](docs/backend/commerce-api.md).

Blob adapter dùng conditional write khi `Overwrite=false`; lỗi quyền/kết nối không được coi là không tồn tại. S3 container là prefix trong một bucket, không hỗ trợ Create/DeleteContainer hay public ACL riêng; các method đó báo `NotSupportedException`. Listing theo đủ trang. Tên object mới không nhận segment `.`/`..`; object cũ giữ nguyên key, listing trả `Url=null` nếu không biểu diễn an toàn bằng HTTP URL. Azure download ánh xạ lỗi 404 trực tiếp, không kiểm tra tồn tại rồi mới tải. Caller sở hữu stream upload và stream download trả về.

SMTP yêu cầu TLS: `UseSsl=true` dùng TLS ngay khi kết nối (thường port 465); `false` yêu cầu STARTTLS (thường port 587), không gửi credentials qua plaintext. Lỗi mail được đưa lên boundary/job để xử lý; không log thêm recipient/subject ở adapter.

Cache chỉ bỏ qua lỗi kết nối/timeout/I/O; lỗi Redis command/server và lỗi lập trình vẫn được đưa lên. JSON cache hỏng bị loại, chỉ log loại exception để tránh lộ giá trị. Cache này không quyết định authorization. Generic domain events cũ chạy trong process sau commit và dispose transaction. Commerce dùng transactional outbox riêng cho media cleanup, có lease/retry/dead-letter/replay; không serialize generic entity events. Caller mở transaction riêng phải commit/dispose rồi gọi DispatchCommittedEventsAsync; rollback/dispose thì gọi DiscardPendingEvents và bỏ hoặc reset context. Transaction đã dispose không tự chứng minh commit; đây là hợp đồng do caller thực hiện. Không cho batch chưa hoàn tất tự đi theo một lần save khác. Event được chụp trước save nên không mất khi hard-delete detach entity và không thu gom event chưa được save ở thời điểm dispatch. Dùng SaveChangesAsync(ct) hoặc SaveChangesAsync(true, ct) để chạy cùng pipeline audit/soft delete/event; sync save và deferred acceptance bị từ chối rõ ràng. Repository theo lô materialize input một lần và thêm event trên đúng entity gửi xuống persistence, kể cả xóa theo specification.

Search giới hạn 200 ký tự/16 fields, order tối đa 8 fields từ request, filter tối đa 100 nodes/8 levels. Pagination mặc định sắp theo Id, hoặc thêm Id làm tie-breaker khi tiêu chí không duy nhất; filter null vẫn áp dụng trang mặc định. User search áp dụng IsActive cho cả kết quả và tổng số. Field/direction trùng hoặc sai, kiểu giá trị không hợp lệ và audit type không tồn tại trả lỗi validation. Các truy vấn số/null/ngày UTC được kiểm thử trên hai database. Audit export áp dụng cùng BaseFilter/allowlist, từ chối UserId sai/khoảng ngày đảo ngược thay vì bỏ qua điều kiện. Xác nhận email/điện thoại sai là lỗi request400, thông báo không chứa thông tin liên hệ.

## Cách xử lý cảnh báo

Ưu tiên lỗi bảo mật, giao dịch và tài nguyên; không tắt analyzer hoặc đổi API/migration chỉ để đạt số 0. `scripts/review-warnings.py` tạo danh sách theo file/dòng/rule và lý do giữ hoặc hoãn từ log full build:

```powershell
dotnet build Boilerate.sln -c Release --no-restore --no-incremental *> artifacts/analyzers.log
python scripts/review-warnings.py artifacts/analyzers.log --output artifacts/analyzers
```

| Nhóm giữ có chủ đích | Lý do |
|---|---|
| `ICurrentUser` / initializer, marker DI/event | Cô lập reader/khởi tạo và đăng ký DI; attribution không tạo authentication |
| DTO collections/string URL, exception nghiệp vụ | Phục vụ binding/mapping, relative paths và hợp đồng lỗi hiện có |
| Normalized keys, EF string expressions | Giữ quy ước mã ổn định và khả năng dịch SQL; không thay máy móc bằng `StringComparison` |
| Persisted enum và baseline provider | Giữ ý nghĩa giá trị nghiệp vụ; migration mới do EF sinh theo model, không sửa generated code để đạt style |
| Retry boundary và cleanup guard trong test | Giữ retry notifications và từ chối xóa thư mục test nếu path không an toàn |
| JSON cache logging | Không log toàn bộ exception vì message có thể chứa dữ liệu cache |

Style/docs/member ordering và gợi ý allocation/logging chưa có bằng chứng bottleneck là debt hoãn xử lý, không được tính là lỗi đã sửa. Cần kiểm tra từng call site khi mở rộng hệ thống.

## PDF và packages

Excel dùng schema property có getter công khai, bỏ indexer, hỗ trợ enum và giữ số thập phân hiển thị. Số nguyên/decimal vượt độ chính xác Excel được ghi dạng text để bảo toàn giá trị; export theo dõi cancellation giữa các dòng/sheet. Không có hợp đồng ExportOptions/ExportColumn giả chưa được adapter hỗ trợ.

Invoice/report: PDFsharp + PDFsharp-MigraDoc **6.2.4**, Noto Sans regular/bold embedded có tiếng Việt/OFL. MediatR 12.5.0, RazorEngineCore email, ClosedXML Excel giữ lại. 6.2.4 MIT ([NuGet](https://www.nuget.org/packages/PDFsharp-MigraDoc/6.2.4)); 6.5+ AGPL/commercial ([upstream](https://docs.pdfsharp.net/General/License/License.html)), nâng version cần review. Xem THIRD_PARTY_NOTICES.md/license font.

MessagePack pin 2.5.303 vá [GHSA-qhrr-8q5h-9q3h](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h) ở dependency SignalR Redis. Central versions/lock files giữ reproducible restore. Roslyn transitive cần cho Razor; server SignalR không chứng minh cần Client. QuestPDF/Skia/demo endpoints đã gỡ với regression PDF/email/Excel. Pkcs pin 10.0.13 xử lý advisories cũ; không nâng major hàng loạt.

Mục tiêu là **một phần mềm cho một shop, thích ứng với mặt hàng muốn bán**, tham khảo trải nghiệm quản trị sản phẩm Webflow. Chủ shop tự cấu hình loại/bộ trường/options/SKU và chọn cách giao đã hỗ trợ; không xây nền tảng quản lý nhiều shop.

Catalog mặc định tắt và đã thay contract mẫu bằng ProductType/schema, Product–Category N–N, SKU/options, giá VND, tồn kho MAIN, media và transactional outbox. [Cách giao/kho số](docs/backend/features/digital-inventory.md) bổ sung Physical/Unique/Shared/ManualExtension, nội dung mã hóa và quyền xem riêng. [Ordering](docs/backend/features/ordering-checkout.md) bổ sung giỏ, snapshot đơn, giữ hàng, hủy/hết hạn cho khách đăng nhập. [Nhận tiền/giao số](docs/backend/features/payment-digital-delivery.md) có xác nhận chuyển khoản do nhân viên kiểm tra, tự giao Unique/Shared và email outbox. [Shipping/COD](docs/backend/features/physical-shipping.md) có shipment giao từng phần, xuất tồn từ phần đã giữ, xác nhận Delivered và ghi đủ tiền COD riêng. [Gia hạn thủ công](docs/backend/features/manual-renewals.md) tạo yêu cầu sau Paid, có claim/Start/PendingVerification và quyền xác minh kết quả thực tế; không tự gia hạn ở dịch vụ ngoài. Trạng thái kiểm chứng local và deployment nằm trong từng hồ sơ. Delivered không tự Paid; đơn đã xuất không được hủy/nhả tồn qua API cũ, kể cả COD chưa trả.

Xem [database design/ERD](docs/backend/commerce-database.md) và [API/vận hành](docs/backend/commerce-api.md). CommerceCatalog chỉ áp dụng khi Catalog cũ rỗng; các migration commerce tiếp nối bổ sung trên baseline đó, không có ETL tự động. [Hậu mãi](docs/backend/features/refunds-returns.md) ghi nhận khoản đã hoàn và hàng trả thực tế; full refund đóng giao mới, restock cần xác nhận nhận hàng. Đăng ký/cấp rõ Shipping.View/Dispatch/Deliver, Payment.Confirm, Renewal.View/Process/Verify/Reveal và AfterSales.View/Refund.Confirm/Return.Receive qua operator CLI; restock cần thêm Inventory.Adjust. Gateway/webhook, carrier tự động, RMA/đổi hàng còn ở lát sau. SMTP/gateway/carrier/deployment thật cần nghiệm thu riêng; source hoặc build đạt không thay thế bằng chứng này.

[Rà soát commerce ngày 2026-10-17](docs/backend/commerce-review.md) ghi lại các bản sửa transaction/event, lọc số và phản hồi lỗi. CommerceUnitOfWork tự hoàn tất batch event sau commit/dispose hoặc hủy sau rollback/dispose; từ chối khởi chạy khi context còn thay đổi/event chưa lưu.

## Kiểm thử và template

Tests chỉ tạo/drop DB ngẫu nhiên `boilerate_test_*`. Connection phải là server test có quyền create/drop; tuyệt đối không production. MSSQL mặc định LocalDB Windows; PG cần env.

```powershell
$env:BOILERATE_TEST_MSSQL = "<SQL test connection, database master>"
$env:BOILERATE_TEST_POSTGRES = "<PG test connection, database postgres>"
dotnet test Boilerate.sln --no-restore
# Không cần DB:
dotnet test Boilerate.sln --no-restore --filter "Category!=ProviderIntegration"
dotnet list Boilerate.sln package --vulnerable --include-transitive --no-restore
dotnet pack Boilerate.Template.csproj -o artifacts/packages
dotnet new install artifacts/packages/Boilerate.CleanArchitecture.Template.1.0.0.nupkg
dotnet new boilerate -n MyBackend -o MyBackend
pwsh scripts/verify-template.ps1
```

CI Linux dùng SQL/PG services + template tên mới + build/run container non-root qua hai provider; Windows kiểm tra runtime/PDF/email/Excel. Compose cung cấp DB dev với passwords từ env. Template exclude artifacts/logs/Files/bin/obj/secrets, chỉ đóng gói local không publish. SMTP/cloud/Redis/OAuth credentials thật và Linux deployment vẫn cần smoke riêng; build thành công không phải bằng chứng end-to-end adapters.

[Runbook vận hành](docs/backend/operations-runbook.md) có runner `scripts/verify-operations.ps1` theo stage Local/Recovery/Container, lưu summary/TRX và từ chối pass khi ca bị skip hoặc thiếu provider. Local key/proxy 5/5 và native backup/restore SQL Server/PostgreSQL 2/2 đã đạt; xem [bằng chứng 2026-10-18](docs/backend/verification/2026-10-18-operations.md). Container/CI thực tế vẫn chưa nghiệm thu.
