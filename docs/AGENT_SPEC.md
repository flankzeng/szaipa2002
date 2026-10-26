# Agent 协作规范（AGENT_SPEC）

> 适用于所有接手本仓库的 AI agent（Claude Code / Codex / 其他）和人类开发者。
> **开工前必读顺序**：本文件 → [PROJECT_MAP.md](PROJECT_MAP.md) → [TASKS.md](TASKS.md) → [HANDOFF.md](HANDOFF.md)。
> 不要全仓库盲扫；按 PROJECT_MAP 的路径直接定位。

---

## 1. 项目一句话

深圳市艺术产业促进会官网。旧站 `szaipa2022/`（ASP.NET MVC5，只读参考，本机跑不起来，**不要改**）→ 新站 `src/`（ASP.NET Core net10.0）。
当前阶段目标：**后台自动化**——把首页/展览/艺术家官网/新闻/组织架构从"手打模板"改为"后台录入、页面自动渲染"，新闻改为**微信公众号自动同步**。

## 2. 环境与命令

| 用途 | 命令 |
|---|---|
| 构建（须 0 error） | `~/.dotnet/dotnet build Szaipa.Modernization.slnx` |
| 测试（须全绿） | `~/.dotnet/dotnet test Szaipa.Modernization.slnx` |
| 前端产物 | `cd src/Szaipa.Web && npm run build`（Tailwind admin.css + esbuild 各 bundle） |
| 本地运行 | `~/.dotnet/dotnet run --project src/Szaipa.Web/Szaipa.Web.csproj --urls http://127.0.0.1:5057` |
| 冒烟 | `/healthz` 200；未登录 `/Staff/*` → 302 到 `/Staff/Account/Login` |

- **必须用 `~/.dotnet/dotnet`（SDK 10.0.301）**，PATH 上的 `dotnet` 是旧版。
- NU1900 告警（离线拉不到漏洞库）可忽略，其他告警不得新增。
- 本地配置在 gitignore 的 `src/Szaipa.Web/appsettings.Local.json`（含只读账号）。在 git worktree 里工作时，从主检出目录 `/Users/arthur/Project/GitClone/szaipa2002/src/Szaipa.Web/` 复制它和 `node_modules`（或软链），**绝不提交**。

## 3. 数据库规则（硬约束，违反即回滚）

1. **生产库永不写。** 只读账号 `szaipa_ro` / `tongou_ro`（db_datareader）可以读。禁止使用 `web24.05/Web.config`、`szaipa2022/Web.config` 里的任何生产凭据。
2. **写操作只落在本地可写副本**（见 TASKS.md T0「本地镜像库」）。本地镜像 = 用只读账号 SELECT 生产数据 → 写入本机 SQLite 文件。端到端点测（后台新增→前台显示）都在镜像上做。
3. **Schema 变更**（新表/新列）：
   - 实体 + 两个上下文（读 `SzaipaLegacyReadContext`、写 `SzaipaAdminContext`）的 `OnModelCreating` 同步；NOT NULL 列给默认值。
   - 同时写 SQL Server 脚本到 `docs/sql/YYYY-MM-DD-<slug>.sql`（幂等：`IF NOT EXISTS`），**由用户自己在生产执行**，agent 不执行。
   - 代码必须**容忍新表/新列尚不存在**（参照 `Publication` 的 Type/Preface 与 `ExhibitionWork` 的能力探测降级）：缺表时前台回退旧渲染或空状态，**绝不 500**。
4. 单元测试只用内存 SQLite（`EnsureCreated()`），不连任何真实库。
5. 任何凭据（AppSecret、密码、连接串）只进 `appsettings.Local.json` / user-secrets / 环境变量，**不进代码、文档、commit、聊天输出**。

## 4. 代码约定

- 一个后台模块 = 4 处（照 News 范本）：写仓储 `src/Szaipa.Data/Services/Admin/` + 控制器 `src/Szaipa.Web/Areas/Staff/Controllers/` + 视图 `Areas/Staff/Views/{Name}/{Index,_Form,Create,Edit}.cshtml` + 导航 `Areas/Staff/Views/Shared/_StaffLayout.cshtml`；DI 在 `SzaipaDataServiceCollectionExtensions.cs`。
- 写操作走 `IOperationRecorder` 记审计；控制器 `[Authorize(Policy = AdminAuthorization.StaffPolicy)]` + `[ValidateAntiForgeryToken]`。
- 前台读侧一律 `.AsNoTracking().Select(投影)`；仓储在控制器里**懒解析**（`HttpContext.RequestServices`），DB 未配置时降级不 500。
- 图片上传走 `IAdminAssetStorage`（GUID 命名）；不要热链第三方 CDN 图片（微信图片必须落盘）。
- 富文本：TipTap（`_RichTextEditor` partial）；输出到前台的外部 HTML 必须先做白名单清洗。
- 匹配周边代码风格：命名、注释密度、文件组织都跟随已有文件。
- **CSS**：用户是设计师，会手改 CSS。大改动写**中文注释**，保持分层（`public-core` → `public-layout` → 页面 CSS），不留死代码。页面视觉以**像素级保持现状**为默认，数据化≠改版。

## 5. 分支、提交、文档

- 工作分支：`codex-refactor-dotnet-modernization`（集成分支）。并行任务各开 worktree 分支 `feat/<task-id>-<slug>`，完成后由集成者合并。
- **只提交自己任务涉及的文件**。工作区里 `szaipa2022/`、`.vs/`、`packages/` 等有用户未提交的改动，**不要 add、不要还原**。禁止 `git add -A` / `git add .`。
- Commit 信息**中文、写细**：做了什么、为什么、验证结果。结尾附 agent 署名行（按所用工具的约定）。
- 每个任务完成必须写更新文档 `docs/updates/YYYY-MM-DD-<slug>.md`（中文：改动、原因、修复的旧 bug、验证结果、需要用户手动做的事）。
- 架构/状态变化时同步更新 `PROJECT_MAP.md`；任务状态变化时更新 `TASKS.md`。
- 推送：集成分支允许推送；不开 PR、不动 `master`，除非用户要求。

## 6. 完成定义（Definition of Done）

一个任务只有同时满足以下全部才算完成：
1. build 0 error、全部测试绿；新增仓储/服务有单测（SQLite 内存库）。
2. 真正跑起来验证过：路由冒烟 + （有镜像库时）后台录入→前台展示的端到端点测；前台改动在 390px 和 1440px 两个宽度看过，无横向溢出。
3. 缺新表/新列时不 500（有测试或手测证明）。
4. `docs/sql/` 脚本、`docs/updates/` 文档、`TASKS.md` 状态均已更新。
5. 视觉与改动前一致（除非任务明确要求改版）。

## 7. 禁区

- 不部署、不登录/修改 Windows 服务器与 IIS、不移动/删除生产 Content（`web24.05/Content` 只读引用）。
- 不改旧站 `szaipa2022/`。
- 不删除 `/Content` 下的资源文件；不做二次有损压缩。
- 不引入新的前端框架（React/Vue 等）；后台用现有 Tailwind + 原生 JS + esbuild。
- 不要为了"重构"改动与当前任务无关的文件。

## 8. 交接格式

额度/上下文将尽时停下，把以下内容写进 `docs/HANDOFF.md` 顶部「最新交接」段：
- 完成了什么（含 commit hash）、进行到一半的任务（分支名、剩余步骤、已知问题）；
- 验证状态（哪些跑过、哪些没跑）；
- 需要用户手动做的事（执行 SQL、填凭据、上传文件等）；
- 一段可直接粘贴给新 agent 的 prompt。
