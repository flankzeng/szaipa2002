# 2026-10-28 首页和新闻隔离测试站

## 用户需求
用户要求正式站显示旧版新闻条目，并通过本机 SSH 隧道访问独立的新版首页/新闻测试环境，确认后再考虑发布后续修改。正式站新闻恢复另见 `2026-10-28-restore-old-news.md`。

## 测试站
- IIS `szaipa-preview`，物理目录 `C:\SzaipaData\preview-20261028\release`，独立池 `SzaipaPreview`，唯一绑定 `127.0.0.1:18089`。创建前 IIS 备份 `pre-preview-20261028`；正式站 `szaipa` / `SzaipaProd` 不变。
- 同一台 Mac 上已创建仅监听 `127.0.0.1:18089` 的 SSH 隧道；测试首页 `http://127.0.0.1:18089/`，新闻 `http://127.0.0.1:18089/Home/News`。隧道断开后可在本机运行：

```bash
ssh -f -N -o ExitOnForwardFailure=yes -L 127.0.0.1:18089:127.0.0.1:18089 szaipa
```

- 测试程序初始为 `269d7b8`；文件、Content 副本、缓存、日志、Data Protection 密钥、公众号配置路径均在 `C:\SzaipaData\preview-20261028` 内。生产模式要求 Data Protection 应用名 `Szaipa.Web`，但密钥目录独立，不共享正式站 cookie。
- 为使新闻与正式站一致，使用 SQL Server `COPY_ONLY, CHECKSUM` 对生产 `Szaipa`/`Tongou` 制作并 `RESTORE VERIFYONLY` 校验快照，备份放在该目录下的受限 `current-snapshot`。只读生产数据、未修改生产数据库；还原为 `Szaipa_preview_current` 和 `Tongou_preview_current`，图片复制到独立 Content。测试数据是 2026-10-28 01:05 前后的快照，后续生产变化不会自动同步。
- 测试池身份 `IIS APPPOOL\SzaipaPreview` 仅为两个测试库 `db_datareader`；实测对生产两库访问为 0、非 `db_datawriter`、非 sysadmin。后台写入和公众号自动同步均关闭，测试不触发抓取，也不写生产库。
- 曾从旧 staging 备份建立临时测试副本，但与正式新闻有差异；已切换到当前快照，并删除此次创建的过时副本库。配置修改前已备份。启动测试过程中应用名不符曾导致 500，修正为固定应用名且保留独立密钥目录后恢复。无凭据的临时服务器脚本已删除。

## 验证与限制
- 通过隧道首页、新闻、健康检查、原图和 WebP 均 200；测试与正式新闻均为 86 条、ID 顺序一致。01:07 测试站最新日志无 `fail:`，显示公众号同步未启用；正式站最新日志无 `fail:`。
- 测试站仅供本机隧道访问，未配置为公网站点；测试数据库为只读副本。未来如需在测试站尝试导入或编辑，须先单独设计仅写副本的权限与流程，不可指向生产数据库。
- 后续代码或数据更新需先部署到测试目录并重新验证；经用户确认后才动正式站。未进行生产后台写入测试。
- 公众号后台仍被浏览器站点安全策略拦截；用户需提供仍公开文章的完整链接，保留 `chksm`，过滤已删除和隐藏文章。
