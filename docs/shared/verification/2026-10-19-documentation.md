# 2026-10-19 — Kiểm chứng tổ chức tài liệu và quy tắc chung

Môi trường: Windows, PowerShell, Python 3.11; workspace repository. Phạm vi: Markdown, điều hướng và nội dung template. Không đổi code/API/database của BE; không có UI FE mới.

## Bước 1 — Tách BE, FE và shared

- Chuyển 67 file Markdown cũ theo mapping; kiểm tra đích nằm trong workspace trước khi move, không ghi đè file đã có. Mapping cục bộ: `artifacts/docs-standardization/moves.json`.
- Lần quét đầu sau khi thêm docs mới: 79 file, 462 liên kết file nội bộ; 1 liên kết chưa resolve là báo cáo này khi còn chưa được tạo. Đã tạo báo cáo rồi chạy lại trước commit.
- Kết quả cuối bước 1: **80 file Markdown, 469 liên kết file nội bộ, 0 liên kết hỏng**. `git diff --check` và `git diff --cached --check` đạt trước commit. Summary JSON: `artifacts/docs-standardization/links-step1.json`.
- Chỉ kiểm tra liên kết file Markdown inline bên ngoài code fence tại root và `docs/`; bỏ qua URL ngoài và fragment/heading. Không xác nhận nội dung ở URL ngoài còn truy cập được.
- Đối chiếu history/verification cũ với Git trước move: **46 bản ghi giữ nguyên nội dung ngoài đích liên kết**; **67/67 đích move tồn tại**. Index history có thêm giải thích vị trí cũ/mới, được kiểm riêng thay vì coi là bản ghi nguyên trạng.

## Cách tái kiểm tra liên kết

Chạy tại root repository bằng PowerShell có Python:

```powershell
@'
import re
from pathlib import Path
from urllib.parse import unquote, urlsplit
root = Path.cwd()
files = sorted(set(root.glob('*.md')) | set((root / 'docs').rglob('*.md')))
count, broken = 0, []
for path in files:
    body = path.read_text(encoding='utf-8-sig')
    body = re.sub(r'^```[^\n]*\n.*?^```[^\n]*$', '', body, flags=re.M | re.S)
    for raw in re.findall(r'\[[^\]\n]*\]\(([^\)\n]+)\)', body):
        target = urlsplit(raw.strip().strip('<>'))
        if target.scheme or target.netloc or not target.path:
            continue
        count += 1
        if not (path.parent / unquote(target.path)).resolve().exists():
            broken.append((str(path.relative_to(root)), raw))
print({'files': len(files), 'links': count, 'broken': broken})
raise SystemExit(bool(broken))
'@ | python -
git diff --check
git diff --cached --check
```

Script cục bộ tương đương ở `artifacts/docs-standardization/check_links.py` lưu JSON khi chạy. Raw mapping/JSON/logs/package/project sinh thử không được commit. Dùng đoạn lệnh trên để tái kiểm tra khi artifacts không còn.

## Bước 2 — Rule generic và template

| Kiểm tra đã chạy | Kết quả |
|---|---|
| `dotnet pack Boilerate.Template.csproj --no-restore -o artifacts/docs-standardization/packages --verbosity quiet` | Đạt |
| Cài package bằng `dotnet new install` với custom hive cô lập; sinh `RenamedBackend` | Đạt |
| Kiểm tra các đường dẫn từ `$requiredFiles` trong `scripts/verify-template.ps1` | **18/18 có trong dự án sinh ra** |
| Kiểm tra toàn bộ đích di chuyển tài liệu | **67/67 có trong package và dự án sinh ra** |
| So sánh CLAUDE.md source với dự án đổi tên | **Giữ nguyên byte**; nội dung không phụ thuộc tên dự án/stack/nghiệp vụ |
| Quét package | 495 entries; không có các nhóm `bin/obj/logs/Files/TestResults/artifacts/__pycache__/.git`, hoặc hậu tố `.trx/.pyc/.pfx/.env/.nupkg` |
| Parse `scripts/verify-template.ps1` bằng PowerShell AST | Đạt; không có parse error |
| Kiểm tra liên kết source và template đổi tên | **Mỗi nơi 82 file Markdown, 483 liên kết nội bộ, 0 hỏng**; phạm vi checker giống bước 1 |
| Diff và staged diff trước commit | `git diff --check`, `git diff --cached --check` đạt |

Lệnh tạo môi trường smoke (PowerShell tại root; đường dẫn có GUID để không ghi đè output cũ):

```powershell
$taskDocsSmoke = Join-Path (Get-Location) ('artifacts/docs-standardization/template-' + [guid]::NewGuid().ToString('N'))
$taskDocsHive = Join-Path $taskDocsSmoke 'hive'
$taskDocsGenerated = Join-Path $taskDocsSmoke 'RenamedBackend'
$taskDocsPackage = Join-Path (Get-Location) 'artifacts/docs-standardization/packages/Boilerate.CleanArchitecture.Template.1.0.0.nupkg'
dotnet pack Boilerate.Template.csproj --no-restore -o artifacts/docs-standardization/packages --verbosity quiet
if ($LASTEXITCODE) { throw 'Pack failed' }
dotnet new install $taskDocsPackage --debug:custom-hive $taskDocsHive
if ($LASTEXITCODE) { throw 'Install failed' }
dotnet new boilerate -n RenamedBackend -o $taskDocsGenerated --debug:custom-hive $taskDocsHive
if ($LASTEXITCODE) { throw 'Generate failed' }
```

Đối chiếu danh sách `$requiredFiles` của runner với các file tương ứng trong `$taskDocsGenerated`; so sánh rule bằng byte. Mở package bằng ZIP để kiểm đích tài liệu và nhóm artifact nêu trong bảng. Chạy lại đoạn kiểm liên kết ở trên từ thư mục sinh ra. Không dùng full runner chỉ để kiểm docs, vì full runner còn yêu cầu hai DB và chạy restore/build/test.

Raw evidence cục bộ: `artifacts/docs-standardization/pack.log`, `template-install.log`, `template-generate.log`, `template-paths.json`, `template-check.json`, `links-step2.json`, `links-template.json`. Source trước khi tổ chức lại: `7a480d0`; commit tổ chức docs: `a696182`. Commit rule được tạo cùng bản ghi bước 2, không ghi hash dự đoán.

## Phạm vi chưa chạy

Không chạy lại build/regression hai DB, Linux/Docker/CI, adapter thật hoặc E2E FE trong nhiệm vụ tài liệu này. Các báo cáo BE cũ giữ nguyên ngày/phạm vi, không được gộp thành kết quả kiểm chứng mới.

Phần pack/sinh template ở trên chỉ nghiệm thu nội dung tài liệu được phân phối, không thay thế full template build/test hoặc kiểm chứng nghiệp vụ.
