# 2026-07-25 退役展览精确图库目录与迁移安全收口

## 结果

14 个已退役的手写展览页不再要求整理或重编号旧图片。现代站按固定 Publication ID
读取 `LegacyPublicationGalleryCatalog` 中的精确路径顺序，普通型和重要型两套画廊的
主 swiper、缩略 swiper 都使用同一份列表。非这 14 个固定 ID 的展览继续沿用现有数字
编号约定，不受影响。

目录共覆盖 604 张旧图。本地只读参考
`~/Project/GitClone/web24.05/Content/images` 已逐项核对，604/604 路径存在。过程中发现
zhongfa 旧视图写成 `100016.jpg`，磁盘实际文件是 `1000016.jpg`；新目录使用实际文件名，
消除了旧页面原有的一处 404。

本轮没有复制、重压、重命名或删除任何外置 Content 文件，也没有修改旧发布版、服务器、
IIS 或 `szaipa2026` 发布仓库。

## 数据库迁移保护

`docs/sql/2026-07-09-publication-slug-migration.sql` 已改为 fail-closed：

- 先检查 `dbo.Publication`、`Type/Preface/Signature` 列和 92001–92015 保留 ID；
- `XACT_ABORT ON` + 显式事务；
- 插入行数不是 14 或迁移后 ID 集合不完整时立即回滚；
- 92004 继续保留给已退役的 zengfeng，不插入。

另新增 `docs/sql/2026-07-25-publication-migration-readiness.sql`。它只执行 schema、表和
保留 ID 查询，不含 DDL/DML，可以先在目标数据库上生成检查结果。

当前数据库写入次数仍为 0。由于现有连接是只读库、且没有确认的本地可写数据库副本，
92001–92015 仍返回 404；这不是渲染回退，而是数据行尚未写入。

## 验证

- 精确路径目录：14 组、604 个路径、组内无重复；
- SQL 与目录契约：14 个 ID 和各自 `MaxImg` 一致；
- readiness 脚本由测试约束为只读；
- `tests/Szaipa.Data.Tests`：98/98；
- `tests/Szaipa.Web.Tests`：146/146；
- 全解决方案：244/244；
- .NET build：0 warning / 0 error。

## 下一步人工门槛

用户确认一个与生产/共享库隔离的本地可写 Szaipa 数据库副本后：

1. 先执行只读 readiness 脚本；
2. 如缺模板列，审核并执行 `2026-06-exhibition-template-columns.sql`；
3. 再执行事务化 slug migration；
4. 启动现代站逐页检查 14 个固定 ID 和旧 URL 的 301。

在本地可写目标未明确前，不执行任何数据库写入。
