# 全量操作记录分页页（2026-07-12）

## 完成

- 新增受 Staff 策略保护的 `/Staff/Operations`。
- 按有操作记录的 Diary 日期倒序分页，每页 15 天；页码小于 1 或超出末页时自动收敛。
- 每日记录继续沿用 legacy `/` 分隔格式解析，不改表结构、不写数据库。
- 数据库未配置或不可连接时显示友好降级，不返回 500。
- Staff 顶部导航新增“操作记录”，仪表盘近期 feed 新增“查看全部”。
- 新增分页顺序/总数和越界页测试，测试基线 94 → 96。

## 验证

- `.NET build`：0 warning / 0 error。
- tests：96/96。
- npm build：通过，Tailwind 已包含新页面使用的样式。
- 未登录访问 `/Staff/Operations` 返回 302，并携带 ReturnUrl 跳转 `/Staff/Account/Login`。
