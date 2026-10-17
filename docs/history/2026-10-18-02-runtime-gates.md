# Gate runtime và trusted proxy

Ngày: 2026-10-18, Asia/Saigon.

1. Bổ sung 3 ca qua ForwardedHeadersMiddleware thật: trusted peer áp dụng header, peer không tin cậy không giả client/protocol và chỉ áp dụng một hop.
2. Tăng độ chặt ContainerRuntimeTests: image production chạy bootstrap/preflight riêng; HTTP không mang bootstrap env; kiểm live/ready rồi remove/recreate container với cùng volume, đối chiếu media/key checksum/session. Cleanup xử lý cả operator/container/volume/env files, env Linux quyền 0600; không in provider secrets vào lỗi Docker.
3. Build Release: 0 errors (còn analyzer/style warnings). Chạy DeploymentRuntime qua runner Local: **5 passed, 0 failed, 0 skipped**; raw evidence `artifacts/operations/20261017-180846-1c2be7e0f6254c26a04825c0acabc1e9/operations-local.trx`. Tên thư mục dùng UTC; thời gian local là ngày 2026-10-18.
4. Docker engine timeout sau khởi Docker Desktop. Container gate còn yêu cầu Linux host networking, chưa chạy image thực tế ở môi trường Windows này; không đánh dấu đã nghiệm thu.

Source: tests/Boilerate.Tests/DeploymentRuntimeTests.cs, ContainerRuntimeTests.cs. [Kế hoạch](../features/operations-acceptance.md). Runner và native recovery được hoàn thiện ở bước kế tiếp; kết quả Local phía trên chạy source working tree đầy đủ, không phải một lượt test checkout riêng của commit này.

Tiếp theo: native recovery qua hai DB, runner/CI/runbook và ghi bằng chứng tách khỏi runtime production.

Commit subject: `test(runtime): verify proxy trust and production container replacement`.
