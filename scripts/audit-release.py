#!/usr/bin/env python3
"""只读检查现代站发布目录；可在目录外生成逐文件 SHA-256 清单。绝不部署。"""

import argparse
import hashlib
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


REQUIRED = (
    "Szaipa.Web.dll", "Szaipa.Data.dll", "Szaipa.Web.deps.json",
    "Szaipa.Web.runtimeconfig.json", "web.config", "appsettings.json",
    "wwwroot/admin/admin.css", "wwwroot/admin/editor.js",
    "wwwroot/admin/image-cropper.js", "wwwroot/admin/image-cropper.css",
    "wwwroot/admin/block-manager.js", "wwwroot/admin/artist-site.js",
    "wwwroot/css/exhibition-topic.css",
    "wwwroot/css/public-core.css", "wwwroot/css/font-subsets-public.css",
    "wwwroot/css/font-subsets-staff.css",
)


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def audit(root):
    errors, files = [], []
    for required in REQUIRED:
        if not (root / required).is_file():
            errors.append(f"缺少运行文件: {required}")
    for path in sorted(root.rglob("*")):
        relative = path.relative_to(root).as_posix()
        parts = {part.lower() for part in path.relative_to(root).parts}
        name = path.name.lower()
        if path.is_symlink():
            errors.append(f"不允许符号链接: {relative}")
            continue
        forbidden = (
            bool(parts & {"content", "app_data", ".local-db", "node_modules", ".git", "securitysampledata"})
            or name.startswith(("appsettings.local", "appsettings.production"))
            or (name.startswith("package") and name.endswith(".json"))
            or name in {"admin.input.css", "qqwry.dat", ".gitkeep"}
            or path.suffix.lower() in {".db", ".sqlite", ".sqlite3", ".br", ".gz", ".pfx", ".pem"}
            or relative.lower().startswith(("wwwroot/admin/src/", "wwwroot/public/src/"))
        )
        if forbidden:
            errors.append(f"不应发布: {relative}")
        if path.is_file():
            files.append({"path": relative, "bytes": path.stat().st_size, "sha256": digest(path)})

    # 保留默认空配置，拒绝将机器连接串/调试开关夹进产物；错误仅报告键，不输出敏感值。
    for path in root.glob("appsettings*.json"):
        try:
            settings = json.loads(path.read_text(encoding="utf-8-sig"))
            def check(node, prefix=""):
                if not isinstance(node, dict):
                    return
                for key, value in node.items():
                    full = f"{prefix}:{key}" if prefix else key
                    if isinstance(value, dict):
                        check(value, full)
                    elif value and (key.lower() in {"connectionstring", "appsecret", "password", "token", "accesstoken", "apikey"}
                                    or prefix == "ConnectionStrings"):
                        errors.append(f"配置含非空凭据: {path.name} {full}")
                    elif value is True and (full in {"LocalMirror:Enabled", "WeChat:Enabled", "WeChat:Callback:Enabled", "RuntimeSafety:AllowLiveDatabase"}
                                            or key in {"EnableWrites", "UseReadOnlyConnectionForDebug"}):
                        errors.append(f"发布默认配置不应启用: {path.name} {full}")
            check(settings)
        except (ValueError, OSError):
            errors.append(f"无法解析配置: {path.name}")
    if (root / "web.config").is_file():
        try:
            node = ET.parse(root / "web.config").find(".//aspNetCore")
            if node is None or "Szaipa.Web" not in (node.get("arguments", "") + node.get("processPath", "")):
                errors.append("web.config 未配置 Szaipa.Web 的 IIS 启动入口")
        except ET.ParseError:
            errors.append("web.config XML 无效")
    return errors, files


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--manifest", type=Path, help="目录外的 JSON 清单路径（不覆盖已有文件）")
    parser.add_argument("--revision", default="unspecified", help="已验证的源码 commit SHA")
    args = parser.parse_args()
    root = args.directory.resolve()
    if not root.is_dir():
        parser.error("发布目录不存在")
    if args.manifest and args.manifest.resolve().is_relative_to(root):
        parser.error("清单必须放在发布目录之外，避免自引用")
    errors, files = audit(root)
    if errors:
        print("\n".join(errors), file=sys.stderr)
        return 1
    total = sum(item["bytes"] for item in files)
    native = sum(item["bytes"] for item in files if "sqlite" in item["path"].lower())
    print(f"通过: {len(files)} 文件，{total:,} B；SQLite 相关 {native:,} B")
    if args.manifest:
        with args.manifest.open("x", encoding="utf-8") as stream:
            json.dump({"revision": args.revision, "totalBytes": total, "files": files},
                      stream, ensure_ascii=False, indent=2)
            stream.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
