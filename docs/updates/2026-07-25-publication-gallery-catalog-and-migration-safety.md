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

当前数据库写入次数仍为 0。应用同时保存这 14 场已结束展览的只读页面元数据：读取详情时
先查数据库，真实行存在就使用真实行；缺行时才使用兼容元数据。因此 92001–92015 不再
依赖本地可写副本，也不会因为旧库缺行返回 404。

## 验证

- 精确路径目录：14 组、604 个路径、组内无重复；
- SQL 与目录契约：14 个 ID 和各自 `MaxImg` 一致；
- readiness 脚本由测试约束为只读；
- `tests/Szaipa.Data.Tests`：98/98；
- `tests/Szaipa.Web.Tests`：146/146；
- 全解决方案：244/244；
- .NET build：0 warning / 0 error。

## 可选的未来数据归一化

若未来要让 Staff 后台直接编辑这 14 场历史展览，可在确认隔离的本地可写副本后：

1. 先执行只读 readiness 脚本；
2. 如缺模板列，审核并执行 `2026-06-exhibition-template-columns.sql`；
3. 再执行事务化 slug migration；
4. 启动现代站逐页检查 14 个固定 ID 和旧 URL 的 301。

这不是公开页面上线或 UI 验收的前置条件。在本地可写目标未明确前，继续保持数据库零写入。
