# 前台清理 Phase 6：旧发布版只读对照（2026-07-11）

## 参考范围

- 旧发布版：`~/Project/GitClone/web24.05`
- 实际资源根：`~/Project/GitClone/web24.05/Content`
- 整体约 2.5GB，`Content` 约 1.2GB。
- 本轮仅做只读扫描，没有修改或删除旧发布版文件。

## 双基线审计

同一份 `Content` 分别使用现代源码和旧发布版源码扫描：

| 源码基线 | 文件数 | 总体积 | 待复核候选 |
|---|---:|---:|---:|
| 现代站 `src/Szaipa.Web` | 4523 | 1.1GB | 1423 / 95.9MB |
| 旧发布版 | 4523 | 1.1GB | 1351 / 87.8MB |

差出的约 8.1MB 表示“现代站不再引用，但旧发布版仍可能引用”。旧版仍是当前发布版本时，这部分不能按现代源码结论删除。

两份原始报告：

- `docs/updates/2026-07-11-legacy-content-audit-web24.05.md`
- `docs/updates/2026-07-11-legacy-content-audit-old-release.md`

## 仍缺的生产事实

- 参考副本中没有 IIS `.log` 文件。
- 已存在只读脚本 `.review/traffic-preview-audit/Analyze-IIS-Traffic.ps1`，默认从服务器 `C:\inetpub\logs\LogFiles\W3SVC*` 读取日志。
- 数据库动态资源路径尚未导出，因此 `newsImg`、`images`、`ArtImg`、`Tongou` 继续整类保护。

## 本地只读数据库验证

- Local 环境已确认 Szaipa、Tongou 均以 `ReadOnly` 方式启用，`AllowLiveDatabase=false`。
- `/healthz` 返回 200，真实 `/Home/newIndex` 从数据库读取新闻和 Publication 后返回 200。
- 首页实测约 0.79 秒完成服务端响应；数据库查询成功且没有写操作。
- 当前数据库尚无 `Publication` 92001–92015 的迁移行：渲染页面中这些链接的数量与源码硬编码数量完全一致，动态增量为 0；因此首页硬编码展会暂不能删除。

## 字体

- 旧发布版本地只有得意黑、DIN、图标字体等，没有 Alibaba 普惠体原文件。
- 已定位 Alibaba Fonts 官方站 `https://www.alibabafonts.com/` 及官方字体源 `https://fonts.alibabadesign.com/`。
- 官方源提供普惠体 2.0 的 Light、Regular、Medium 等 WOFF2；与旧有字体 L/R/M 配置相符。
- 字体文件必须完整下载并经 FontTools 校验后才进入仓库；临时下载或校验失败的文件不得提交。

## 安全提醒

旧发布版 `Web.config` 含明文数据库连接凭据。审计没有使用这些凭据，也不会在文档中记录。应尽快轮换相关账号密码，并将生产密钥迁出版本库和发布文件。
