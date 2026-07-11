#!/usr/bin/env python3
"""Audit a legacy Content tree without deleting anything.

The report combines static /Content references with conservative dynamic-path
protection. Optional database-path exports and HTTP access logs can promote
additional files into the used set before any cleanup is considered.
"""

from __future__ import annotations

import argparse
import re
from collections import Counter, defaultdict
from pathlib import Path
from urllib.parse import unquote, urlsplit


TEXT_SUFFIXES = {".cs", ".cshtml", ".css", ".js", ".json", ".md"}
SKIP_PARTS = {"bin", "obj", "node_modules", ".git"}
CONTENT_REFERENCE = re.compile(r"(?i)/content/[^\"'`\r\n<>]+")
LOG_REFERENCE = re.compile(r"(?i)(?:GET|HEAD)\s+(/content/[^\s?]+)")

# These roots contain database-supplied names, generated gallery names, or
# admin uploads. Static-source absence is never sufficient to delete them.
DYNAMIC_PROTECTED_ROOTS = {
    "newsimg": "新闻封面、正文图片和后台富文本上传使用数据库动态路径",
    "artimg": "会员、作品、艺术新闻、拍卖及企业图片使用数据库动态路径",
    "images": "展览 FolderName/CoverPath/编号画廊及后台展览上传使用动态路径",
    "tongou": "Tongou 后台数据和上传路径仍保留",
}

# These roots are small shared runtime libraries/assets. A single explicit
# dependency may have transitive references that source scanning cannot see.
RUNTIME_PROTECTED_ROOTS = {
    "123": "favicon 和共享品牌资源",
    "model": "当前页面使用的 Bootstrap CSS、jQuery、Swiper、Magnify 等",
    "icon": "共享导航、社交和交互图标",
    "fonts": "原字体字形兜底",
    "css": "legacy publication.css 等当前明确样式依赖",
}


def human_size(size: int) -> str:
    value = float(size)
    for unit in ("B", "KB", "MB", "GB"):
        if value < 1024 or unit == "GB":
            return f"{value:.1f}{unit}" if unit != "B" else f"{int(value)}B"
        value /= 1024
    raise AssertionError("unreachable")


def normalize_reference(value: str) -> str | None:
    value = value.strip().replace("\\", "/")
    if value.startswith("http://") or value.startswith("https://"):
        value = urlsplit(value).path
    lower = value.lower()
    marker = lower.find("/content/")
    if marker < 0:
        return None
    value = unquote(value[marker + len("/content/") :]).split("?", 1)[0].split("#", 1)[0]
    value = value.lstrip("/").rstrip(" \t);,")
    if (
        not value
        or "/" not in value
        or not Path(value).suffix
        or any(token in value for token in ("@", "{", "}"))
    ):
        return None
    return value.casefold()


def collect_static_references(source_root: Path) -> tuple[set[str], Counter[str]]:
    references: set[str] = set()
    sources: Counter[str] = Counter()
    for path in source_root.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in TEXT_SUFFIXES:
            continue
        if any(part in SKIP_PARTS for part in path.parts):
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        for match in CONTENT_REFERENCE.findall(text):
            normalized = normalize_reference(match)
            if normalized:
                references.add(normalized)
                sources[normalized] += 1
    return references, sources


def collect_path_file(path: Path | None) -> set[str]:
    if path is None or not path.exists():
        return set()
    result: set[str] = set()
    for line in path.read_text(encoding="utf-8", errors="ignore").splitlines():
        normalized = normalize_reference(line)
        if normalized:
            result.add(normalized)
    return result


def collect_access_log(path: Path | None) -> set[str]:
    if path is None or not path.exists():
        return set()
    result: set[str] = set()
    for line in path.read_text(encoding="utf-8", errors="ignore").splitlines():
        match = LOG_REFERENCE.search(line)
        if match:
            normalized = normalize_reference(match.group(1))
            if normalized:
                result.add(normalized)
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--content-root", type=Path, required=True)
    parser.add_argument("--source-root", type=Path, default=Path("src/Szaipa.Web"))
    parser.add_argument("--db-paths", type=Path, help="Optional UTF-8 file containing one DB resource path per line")
    parser.add_argument("--access-log", type=Path, help="Optional HTTP access log containing GET/HEAD /Content requests")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    root = args.content_root.resolve()
    if not root.is_dir():
        raise SystemExit(f"Content root does not exist: {root}")

    static_refs, static_counts = collect_static_references(args.source_root.resolve())
    db_refs = collect_path_file(args.db_paths)
    log_refs = collect_access_log(args.access_log)
    exact_used = static_refs | db_refs | log_refs

    files: list[tuple[Path, str, str, int]] = []
    totals: dict[str, Counter[str]] = defaultdict(Counter)
    disk_entries = {
        path.relative_to(root).as_posix().casefold()
        for path in root.rglob("*")
    }
    missing_refs = set(exact_used) - disk_entries

    for path in root.rglob("*"):
        if not path.is_file():
            continue
        relative = path.relative_to(root).as_posix()
        normalized = relative.casefold()
        top = relative.split("/", 1)[0]
        top_key = top.casefold()
        size = path.stat().st_size
        if top_key in DYNAMIC_PROTECTED_ROOTS:
            classification = "protected-dynamic"
        elif top_key in RUNTIME_PROTECTED_ROOTS:
            classification = "protected-runtime"
        elif normalized in exact_used:
            classification = "referenced-exact"
        else:
            classification = "review-candidate"

        files.append((path, relative, classification, size))
        totals[top]["files"] += 1
        totals[top]["bytes"] += size
        totals[top][classification] += size

    candidates = sorted(
        (item for item in files if item[2] == "review-candidate"),
        key=lambda item: item[3],
        reverse=True,
    )
    large_protected = sorted(
        (item for item in files if item[2].startswith("protected") and item[3] >= 5 * 1024 * 1024),
        key=lambda item: item[3],
        reverse=True,
    )

    total_bytes = sum(item[3] for item in files)
    candidate_bytes = sum(item[3] for item in candidates)
    lines = [
        "# Legacy Content 自动审计",
        "",
        f"- Content root: `{root}`",
        f"- 文件数：{len(files)}",
        f"- 总体积：{human_size(total_bytes)}",
        f"- 静态精确引用：{len(static_refs)}",
        f"- 数据库导入引用：{len(db_refs)}",
        f"- 访问日志引用：{len(log_refs)}",
        f"- 待复核候选：{len(candidates)} 个文件 / {human_size(candidate_bytes)}",
        "",
        "> `review-candidate` 不等于可删除。必须再结合生产日志、数据库路径导出和隔离回归。",
        "",
        "## 顶层目录",
        "",
        "| 目录 | 文件数 | 体积 | 分类 | 原因 |",
        "|---|---:|---:|---|---|",
    ]

    for top, counts in sorted(totals.items(), key=lambda item: item[1]["bytes"], reverse=True):
        key = top.casefold()
        if key in DYNAMIC_PROTECTED_ROOTS:
            category = "protected-dynamic"
            reason = DYNAMIC_PROTECTED_ROOTS[key]
        elif key in RUNTIME_PROTECTED_ROOTS:
            category = "protected-runtime"
            reason = RUNTIME_PROTECTED_ROOTS[key]
        elif counts["referenced-exact"]:
            category = "mixed/referenced"
            reason = "存在源码、数据库导出或访问日志精确引用；其余文件仍需逐项复核"
        else:
            category = "review-candidate"
            reason = "未发现当前输入中的精确引用，也不属于动态保护目录"
        lines.append(f"| `{top}` | {counts['files']} | {human_size(counts['bytes'])} | {category} | {reason} |")

    lines.extend(["", "## 最大待复核文件（前 50）", "", "| 文件 | 体积 |", "|---|---:|"])
    for _, relative, _, size in candidates[:50]:
        lines.append(f"| `{relative}` | {human_size(size)} |")

    lines.extend(["", "## 大型受保护文件（>=5MB）", "", "| 文件 | 体积 |", "|---|---:|"])
    for _, relative, _, size in large_protected[:50]:
        lines.append(f"| `{relative}` | {human_size(size)} |")

    if missing_refs:
        lines.extend(["", "## 白名单中未在磁盘找到的路径（前 100）", ""])
        for reference in sorted(missing_refs)[:100]:
            lines.append(f"- `{reference}`（源码引用次数：{static_counts[reference]}）")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"files={len(files)} total={total_bytes} candidates={len(candidates)} candidate_bytes={candidate_bytes}")
    print(f"output={args.output}")


if __name__ == "__main__":
    main()
