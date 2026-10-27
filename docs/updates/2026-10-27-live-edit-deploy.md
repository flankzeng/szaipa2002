# 2026-10-27 现场编辑扩展部署

## 授权与准备
用户同意将现场编辑候选包部署到正式站。功能源码 `269d7b8aa5982c2cdf65280c5ef675339a01300d`，Windows x64 包 SHA-256 `1ecd0e1fdd80bdd9ce6b8e6c1c7f7423ffe5ef8c8766e8087487ce6d26214343`；服务器上的包与本地哈希一致。

切换前校验现有站点指向 `C:\SzaipaData\releases\031f211` / `SzaipaProd`，备份 IIS `pre-liveedit-269d7b8`，生产 `appsettings.Production.json` 和 `web.config` 备份到受限目录 `C:\SzaipaData\backup\pre-liveedit-269d7b8` 并与原文件做哈希比对。在独立 `C:\SzaipaData\releases\269d7b8` 解压，复制原生产配置及 stdout 设置，授予 `IIS AppPool\SzaipaProd` 读取权限。未修改原发布目录。

## 执行与验证
- 2026-10-27 22:21:40（UTC+8）IIS 站点 `szaipa` 物理目录切至 `C:\SzaipaData\releases\269d7b8`，仍使用 `SzaipaProd`。未执行生产数据库写操作。
- 服务器端检查健康、首页、新闻、后台登录、原图、WebP 和新版编辑脚本均 200；健康状态为 `ok`，新脚本包含多行原值处理。
- 外部检查首页、新闻、会员风采、关于、展会列表、历史展览、后台登录、健康检查、脚本、原图和 WebP 均 200；公开页面无编辑标记，抽样 WebP 文件头正确。
- 截至 22:21:58，应用池运行，四个生产 stdout 文件均无 `fail:`。尚未用用户的正式后台账号做现场编辑保存或上传写入测试；功能此前仅在隔离镜像完成实写验证。
- 预热计划仍为 10 月 28 日 02:00，切换后该日志尚未出现，运行后需检查。

## 回退
现场编辑版本如需回退：先核对当前站点仍为 `269d7b8`，再将 `szaipa` 物理目录改回 `C:\SzaipaData\releases\031f211`，保持 `SzaipaProd`，然后检查健康、页面、图片和日志。两版使用相同生产配置；具体 IIS 备份供必要时恢复参考，避免不加判断地覆盖其他站点配置。回退到旧 MVC 站 `.NET v4.5` 则还须先恢复 Content 原 ACL，见 `docs/updates/2026-10-27-content-acl.md`。
