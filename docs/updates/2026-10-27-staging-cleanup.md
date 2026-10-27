# 2026-10-27 staging 清理

## 操作与备份
用户确认后，先盘点：`szaipa-new` 站点和 `SzaipaNew` 池均已停止，两个 staging 库无活动会话；服务器可用空间约 6.41 GiB。

备份保存在 `C:\SzaipaData\backup\staging-cleanup-20261027`，ACL 仅 SYSTEM、Administrators 和 SQL Server 服务可访问：
- 两个 staging 库使用 COPY_ONLY + CHECKSUM 备份，均通过 `RESTORE VERIFYONLY ... WITH CHECKSUM`。该校验不等于完整还原演练。
- 三个 staging 文件目录复制到备份位置；`staging\db` 的运行中数据库文件不复制，使用上述数据库备份替代。其余 4,812 个文件逐个比对 SHA-256 全部一致，未发现目录跳转。
- IIS 全局配置备份 `pre-staging-cleanup-20261027`。备份可能包含敏感应用池配置，须继续限制访问；不要为恢复 staging 直接覆盖当前全局 IIS 配置。

随后删除 `szaipa-new` 站点、`SzaipaNew` 池、`Szaipa_staging`/`Tongou_staging` 库、`szaipa_staging_rw` 登录，以及 `C:\SzaipaData\staging`、`staging-content`、`staging-image-cache` 目录。仅删除 staging 库和登录，未写生产数据库；未改正式站 IIS 配置。

## 验证与后续
- 清理后核实上述对象不存在；`szaipa`、`cjt` 与重定向站运行，正式站仍指向 `031f211` / `SzaipaProd`，生产 `Szaipa` 和 `Tongou` 为 ONLINE。
- 首页、新闻、健康检查、原图和 WebP 抽样均返回 200；三个生产 stdout 日志截至检查时无 `fail:`。
- 保留的备份占用空间，服务器可用空间从 6,886,166,528 增至 6,973,169,664 字节，净增约 83 MiB。备份是否长期保留须另行决定，不要擅自删除。
- 如需恢复 staging，应先另行制定还原计划：从备份还原两个副本库到独立路径、重建专用登录和映射、恢复测试文件，再依据 IIS 备份选择性重建测试站和池；不得直接全局覆盖生产 IIS。未做还原演练。
- 本次为运维操作，无应用代码变动，未运行构建或测试。
