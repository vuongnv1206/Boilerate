"""Inventory MSBuild diagnostics without changing source or analyzer configuration."""

import argparse
import collections
import json
from pathlib import Path
import re


DECISIONS = {
    "CA2100": ("reviewed-sql-identifiers", "SQL identifier hiện chỉ dùng trong fixture database tạm: prefix + Guid do fixture sinh, quote/kiểm tra trước cleanup. SQL DDL không nhận parameter cho identifier. Preflight production đã chuyển sang LINQ."),
    "DAP241": ("reviewed-sql-identifiers", "CREATE/DROP DATABASE cần identifier; tên get-only được sinh trong fixture, quote và kiểm tra prefix trước cleanup. Không có input HTTP."),
    "S6781": ("reviewed-test-boundary", "Test lấy khóa ngẫu nhiên của fixture để ký JWT sai issuer/audience/sid; không có secret production hardcode hoặc trả về client."),
    "CA2000": ("reviewed-resource-owner", "Hai cảnh báo ở smoke test: using declaration sở hữu HttpClient/Process; finally kill và await tiến trình trước Dispose. Các stream production đã được rà soát riêng."),
    "CA1031": ("reviewed-retry-boundary", "Notification BackgroundService cần giữ vòng retry khi một lượt dispatch lỗi bất kỳ; log exception và giữ IsSent=false. Cancellation của host được xử lý riêng."),
    "CA1040": ("keep-architecture", "Marker lifetime phục vụ DI scan; IEvent là type boundary cho event publisher. Thêm method giả hoặc xóa marker sẽ làm mất ý nghĩa hiện tại."),
    "CA2227": ("keep-contract", "DTO/request collections dùng model binding, Mapster và replacement semantics. Entity navigation còn cần EF materialization. Không đổi public setter hàng loạt vì warning."),
    "CA1819": ("keep-contract", "Arrays thuộc request/export DTO được caller sở hữu, không làm lộ mảng private dùng chung. Thay kiểu không mang lại lợi ích bảo mật cho hợp đồng này."),
    "CA1032": ("keep-contract", "Exception nghiệp vụ yêu cầu message và giữ HTTP status/error list. Không thêm constructor rỗng chỉ để đạt quy ước thư viện. InnerException đã được hỗ trợ khi wrap lỗi hạ tầng."),
    "CA1034": ("keep-test-models", "TemplateModel phải public để Razor runtime compiler truy cập. ExportRow là model cho reflection trong test; giữ chúng cạnh test sử dụng, không phải API production của template."),
    "CA1028": ("keep-persistence", "TrailType có underlying byte và số đã được lưu. Không đổi kiểu persisted enum để giảm warning; thay đổi cần một quyết định schema riêng."),
    "CA1008": ("keep-domain-values", "Zero chưa là trạng thái nghiệp vụ hợp lệ của TrailType/NotificationType; giữ nguyên số đã lưu, không thêm None rồi mặc nhiên cho phép trạng thái chưa định nghĩa."),
    "CA1054": ("keep-contract", "ActionUrl có thể là đường dẫn tương đối; đổi string thành Uri làm đổi DTO và mapping mà chưa có yêu cầu nghiệp vụ."),
    "CA1055": ("keep-contract", "ImageUrl hỗ trợ cả local relative path và URL OAuth; giữ string tương thích claims hiện có."),
    "S101": ("reviewed-naming", "SMTPEmailSettings giữ tên section config hiện có; ClosedXMLWriter theo casing thư viện trong tên adapter và có thể đổi tên sau. Hai trường hợp được phân biệt trong diagnostics.json, không coi mọi tên adapter là hợp đồng cấu hình."),
    "S3246": ("defer-public-api", "Variance có thể hợp lệ nhưng không cần cho use case hiện tại. Tránh thay metadata public generic contracts trong một lượt sửa lỗi hạ tầng."),
    "S2325": ("defer-helper-api", "Pure validator helpers có thể chuyển static; chưa cần thay protected API của template chỉ để giảm warning."),
    "CA1862": ("keep-normalized-keys", "Các call site so sánh NormalizedName/NormalizedCode với mã ToUpperInvariant, gồm EF predicates và tra cứu Local khi bootstrap. Giữ quy ước khóa chuẩn hóa; StringComparison không thay thế trực tiếp quy ước này và không phải overload nào cũng dịch được sang SQL."),
    "S1075": ("keep-provider-boundary", "Google/Facebook endpoint cố định là boundary của adapter. Không mở URL tùy ý từ request/cấu hình chỉ để xóa cảnh báo hardcoded URL."),
    "CA1812": ("keep-di-registration", "LifetimeStub/TestAuthenticationHandler được tạo qua generic DI/auth scheme registration; không phải dead code. Không xóa hoặc đổi registration chỉ vì phân tích tĩnh không theo được."),
    "CA1303": ("keep-operator-output", "CLI operator output không phải UI đa ngôn ngữ; ưu tiên thông báo vận hành ổn định."),
    "CA1869": ("defer-cold-path-allocation", "Preflight tạo options một lần cho một lệnh operator; không cần thêm mutable/global state để tối ưu allocation nhỏ này."),
    "S4144": ("keep-endpoint-boundaries", "Các endpoint delegation tương tự có authorization và mục đích khác nhau (admin create/self-register). Giữ boundary rõ thay vì gộp route để giống analyzer."),
    "S6931": ("keep-route-contract", "Health và local-file routes cố ý là absolute infrastructure routes; không đổi URL đang được client/deployment sử dụng chỉ để đạt convention."),
    "S1118": ("keep-host-entrypoint", "Program là public partial entrypoint của top-level Host và tooling; không chuyển sang utility static class chỉ để giảm warning."),
    "S3877": ("keep-cleanup-guard", "Dispose của thư mục test từ chối recursive cleanup nếu đường dẫn không còn nằm trong root tạm đã xác định. Giữ lỗi fail-closed để không xóa nhầm; đây không phải Dispose của tài nguyên production."),
    "S6667": ("reviewed-sensitive-logging", "Cache JSON conversion exception có thể chứa chính giá trị cache. Với lỗi recoverable này chỉ log exception type rồi loại entry; không log toàn bộ exception/message/payload chỉ để đáp ứng analyzer."),
    "S4136": ("defer-member-order", "Hai overload DeleteRangeAsync nằm ở phần thao tác event và phần forward repository. Đổi vị trí chỉ là debt về thứ tự thành viên, chưa cần sửa trong lượt bảo mật/logic."),
    "S4581": ("keep-empty-id-defaults", "Guid mặc định trên request biểu thị ID chưa được cung cấp, không được sinh ID ngẫu nhiên để làm request có vẻ hợp lệ. Guid.Empty có thể dùng khi chỉnh hợp đồng riêng."),
    "S1144": ("keep-reflection-fixtures", "Các member ở ExportMaintenanceTests cố ý chỉ được reflection quan sát: enum nullable, getter private và indexer. Xóa chúng sẽ bỏ tình huống kiểm thử export; không phải dead code production."),
}


def decision(rule):
    if rule in DECISIONS:
        return DECISIONS[rule]
    if rule.startswith(("SA", "IDE")):
        return "defer-presentation", "Presentation/style debt được ghi lại; chỉ làm rõ những đoạn có thay đổi logic. Không bulk rewrite source hoặc generated migrations để đạt số 0."
    if rule.startswith("xUnit"):
        return "defer-test-style", "Gợi ý cách trình bày assertion, không thay đổi tiêu chí nghiệm thu; ưu tiên kiểm thử hành vi và lỗi thực tế."
    if rule in {"CA1848", "CA1873", "CA1860", "CA1861", "CA1859", "CA1825", "S6605"}:
        return "defer-performance", "Gợi ý allocation/logging/concrete type; chưa có bằng chứng bottleneck. Structured logging được giữ, không viết lại hàng loạt chỉ để giảm warning."
    if rule == "CA2234":
        return "keep-uri-call-contract", "Các string được sử dụng là known relative HTTP paths hoặc endpoint do adapter sở hữu; overload Uri không bổ sung kiểm tra quyền/host trong các call site này."
    if rule == "S3358":
        return "defer-readability", "Nested conditional là debt về cách đọc; các nhánh cần sửa logic đã được mở thành guard clauses."
    return "needs-review", "Cần đánh giá từng call site; chưa tự coi là false positive."


def read_diagnostics(log, root):
    # Messages can contain brackets (for example optional method parameters).
    # Match the final project suffix so the full message participates in deduplication.
    pattern = re.compile(r"(.+?)\((\d+),(\d+)\): warning ([A-Za-z0-9]+): (.*) \[[^\]\r\n]+\]\s*$")
    unique = {}
    for line in log.read_text(encoding="utf-8-sig", errors="replace").splitlines():
        match = pattern.match(line)
        if not match:
            continue
        filename, row, column, rule, message = match.groups()
        path = Path(filename)
        try:
            filename = path.relative_to(root).as_posix()
        except ValueError:
            filename = path.as_posix()
        status, rationale = decision(rule)
        if rule == "S101":
            if path.name == "SMTPEmailSettings.cs":
                status = "keep-configuration-contract"
                rationale = "Tên SMTPEmailSettings tham gia section name/config binding hiện có. Đổi casing đơn thuần cần migration cấu hình và không sửa hành vi."
            elif path.name == "ClosedXMLWriter.cs":
                status = "defer-adapter-naming"
                rationale = "ClosedXMLWriter giữ casing của tên thư viện ClosedXML trong tên adapter. ClosedXmlWriter cũng hợp lệ; đây là đổi tên thẩm mỹ, không phải yêu cầu migration section cấu hình SMTP."
        entry = dict(file=filename, line=int(row), column=int(column), rule=rule, message=message, status=status, rationale=rationale)
        unique[(filename, int(row), int(column), rule, message)] = entry
    return sorted(unique.values(), key=lambda e: (e["rule"], e["file"], e["line"], e["column"], e["message"]))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("log", type=Path)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--baseline", type=Path)
    args = parser.parse_args()
    diagnostics = read_diagnostics(args.log, args.root.resolve())
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "diagnostics.json").write_text(json.dumps(diagnostics, indent=2, ensure_ascii=False), encoding="utf-8")
    counts = collections.Counter(e["rule"] for e in diagnostics)
    baseline = collections.Counter()
    if args.baseline:
        baseline.update(e["rule"] for e in json.loads(args.baseline.read_text(encoding="utf-8-sig")))
    lines = ["# Analyzer inventory", "", f"Unique diagnostics: {len(diagnostics)}. Duplicate MSBuild summary lines are counted once.", "", "| Rule | Baseline | Current | Decision | Rationale |", "|---|---:|---:|---|---|"]
    for rule in sorted(set(counts) | set(baseline)):
        status, rationale = decision(rule)
        if counts[rule] == 0:
            status, rationale = "no-current-diagnostic", "Không còn cảnh báo của rule này trong build hiện tại."
        lines.append(f"| {rule} | {baseline[rule]} | {counts[rule]} | {status} | {rationale} |")
    (args.output / "rules.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps(dict(total=len(diagnostics), status=dict(collections.Counter(e["status"] for e in diagnostics)), unreviewed_rules=sorted({e["rule"] for e in diagnostics if e["status"] == "needs-review"})), ensure_ascii=False))


if __name__ == "__main__":
    main()
