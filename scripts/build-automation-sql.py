#!/usr/bin/env python3
"""串联后台自动化 SQL（仅生成本地文件，绝不连接或执行数据库）。"""
import argparse
import hashlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOURCES = (
    "2026-06-exhibition-template-columns.sql",
    "2026-07-09-exhibition-works-catalog.sql",
    "2026-10-26-wechat-sync.sql",
    "2026-10-26-org-and-home-blocks.sql",
    "2026-10-26-exhibition-automation.sql",
    "2026-10-26-artist-site.sql",
    "2026-10-26-artist-books.sql",
    "2026-10-26-vip-order.sql",
)
OUTPUT = ROOT / "docs/sql/2026-10-26-automation-deploy.sql"


def render():
    parts = ["""-- 后台自动化上线汇总。由 scripts/build-automation-sql.py 生成，不直接编辑。
-- 用户执行前备份数据库、停止后台写入；agent 永不执行生产 SQL。
-- 必须使用 SQLCMD 模式（SSMS: 查询 → SQLCMD 模式，或 sqlcmd -b）。
-- 目标库默认 Szaipa；如是隔离测试库，只修改下一行的库名并核对连接目标。
-- 不包含历史 slug 数据迁移，不改 News 旧数据，不清理 Content。
:setvar TargetDatabase "Szaipa"
:On Error exit
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'$(TargetDatabase)'
    THROW 51000, N'目标数据库不匹配，已停止。请核对连接与 TargetDatabase。', 1;
IF OBJECT_ID(N'dbo.News', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Artist', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Works', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Publication', N'U') IS NULL
    THROW 51001, N'缺少 legacy 基础表，不能应用增量脚本。', 1;
BEGIN TRANSACTION;
GO
"""]
    for name in SOURCES:
        path = ROOT / "docs/sql" / name
        source = path.read_text(encoding="utf-8-sig").replace("\r\n", "\n").strip()
        checksum = hashlib.sha256(source.encode()).hexdigest()
        parts.append(f"\n-- ===== 来源: {name}；SHA256: {checksum} =====\n{source}\nGO\n")
    parts.append("""
IF XACT_STATE() <> 1
    THROW 51002, N'升级事务不可提交，停止并回滚后核对错误。', 1;
COMMIT TRANSACTION;
PRINT N'后台自动化增量结构与种子已提交。请继续应用切换验证。';
GO
""")
    return "".join(parts)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="只验证汇总与来源一致")
    args = parser.parse_args()
    rendered = render()
    if args.check:
        if not OUTPUT.exists() or OUTPUT.read_text(encoding="utf-8") != rendered:
            parser.exit(1, "SQL 汇总过期，请运行 python3 scripts/build-automation-sql.py\n")
        print("SQL 汇总与八份来源一致")
    else:
        OUTPUT.write_text(rendered, encoding="utf-8")
        print(OUTPUT.relative_to(ROOT))


if __name__ == "__main__":
    main()
