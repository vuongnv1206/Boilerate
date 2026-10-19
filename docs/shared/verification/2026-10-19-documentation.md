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

## Phạm vi chưa chạy

Không chạy lại build/regression hai DB, Linux/Docker/CI, adapter thật hoặc E2E FE trong nhiệm vụ tài liệu này. Các báo cáo BE cũ giữ nguyên ngày/phạm vi, không được gộp thành kết quả kiểm chứng mới.

Kiểm tra pack/sinh template và file quy tắc generic được ghi ở bước 2 sau khi thực hiện.
