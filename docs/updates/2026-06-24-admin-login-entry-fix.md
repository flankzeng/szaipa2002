# 2026-06-24 修复前台「后台登录」入口失效

## 问题
用户反馈：本地调试时**进不去后台**，前台 NewIndex 上原来的「后台登陆」入口消失了，也找不到登录页。

## 根因
两类历史遗留：
1. **NewIndex/`_newLayout` 的入口被删**：迁移到 ASP.NET Core 时，`_newLayout.cshtml` 的 footer 把旧版「后台登陆」链接整体删掉了（见记忆 `migration-state` 记录的 "footer dropped 后台登陆(`/Staff/Index`)"，当时新后台尚未建好，属阶段性删除）。结果前台没有任何可点的后台入口。
2. **其余页面残留旧死链 `/Staff/Index`**：`_Artist.cshtml`（NewArt 布局）、`NewNewsRead.cshtml`（新闻详情）、三个硬编码展览页（`chunyu3`/`tonggou2`/`tonggou2024`）的 footer 仍指向旧 MVC5 路由 `/Staff/Index`——该路由在新站不存在（404）。

新后台登录页其实一直正常（`/Admin/Account/Login` 返回 200，表单渲染正常），只是**前台没有入口、且旧入口指向 404**。

## 修复
全站统一把后台入口指向新登录页 `/Admin/Account/Login`，文案统一为「后台登录」：
- `_newLayout.cshtml`：在 PC footer「联系我们」列 + 手机端 footer 各加回一处入口（此前是缺失，新增）。
- `_Artist.cshtml`：PC + 手机端两处 `/Staff/Index` → `/Admin/Account/Login`；顺手移除指向自身的无效「切换旧版」死链（`/home/index` 在新站只会再次渲染 NewIndex，是迁移残留的自链接）。
- `NewNewsRead.cshtml`、`Publication/{chunyu3,tonggou2,tonggou2024}.cshtml`：各 PC + 手机端两处 `/Staff/Index` → `/Admin/Account/Login`。

共 6 个文件、12 处入口，全部指向新登录页。全站再无 `/Staff/Index` / `~/Staff` 残留。

## 验证
- 重启后冒烟：首页 `/` 200 且 footer 含 2 处 `/Admin/Account/Login`；`/Publication/chunyu3` 200 且含 2 处；`/Admin/Account/Login` 200（登录表单正常）；`/Admin`、`/Admin/Dashboard` 未登录 302 跳登录。
- 纯视图链接替换，无 `.cs` 改动；`dotnet build` 不受影响。
- 端到端登录（输账号密码进后台）需用户本地配 `AdminWrite` 可写库后自行点测。

## 备注
- 「切换旧版」入口已移除（它在新站指向自身、无意义）。若用户希望保留某种「旧版/新版」切换，需要另行设计——当前新站只有一套前台。
