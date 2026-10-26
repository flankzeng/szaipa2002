# 艺术家「出版著作」数据化

## 改动

- **新表 `dbo.ArtistBook` / 实体 `ArtistBook`**（`IArtistScopedRecord`）：`ArtistId`、`Title`（必填）、`Publisher`、`PublishYear`（自由文本，如 2023）、`CoverPath`（只存文件名，目录 `/Content/ArtImg/Artist/Book/`）、`Description`、`Sort`（小的在前）、`EditRecord`。表映射集中在 `ArtistBookModelConfiguration`，读上下文 `SzaipaLegacyReadContext` 与写上下文 `SzaipaAdminContext` 共用，避免两处漂移（本地镜像 `mirror-sync` 随 admin 模型自动出现空表）。
- **后台**：`ArtistBookAdminRepository`（泛型基类 `ArtistScopedAdminRepository<T>`，封面空值保留原图）+ `ArtistBookController` + `Areas/Staff/Views/ArtistBook/{Index,_Form,Create,Edit}`。列表复用 `_ArtistScopedList`，封面用 `upload-field`，简介 textarea，排序数字框。导航在「内容管理」下拉末尾追加「出版著作」。写操作走 `IOperationRecorder`、`[Authorize]`、防伪。缺表时控制器 `OnActionExecutionAsync` 捕获缺表/缺列，显示 `_SchemaMissing`「需先执行 SQL」而不是 500。
- **艺术家官网总览** `/Staff/Artist/{id}/Site`：汇总卡片新增「出版著作」数量与入口（`/Staff/ArtistBook?artistId=`，网格改为 md 六列）；缺表时数量为 0 并显示黄色提示，不报错。
- **前台**：
  - `ArtistReadRepository.GetArtistProfileAsync` 的 `LatestPublications` 改为读 `ArtistBook`（按 `Sort`、`Id`，取 1 条，副标题放出版社），**不再读 ArtNews**。
  - `GetArtistArchiveAsync` 新增 `Books`（全部书目，`Sort`、`Id`），`ArtArchive.cshtml`「出版著作」段渲染书目卡片（封面走 `ImagePreviewResolver` 的现有回退规则，书名《…》、出版社、出版年份、简介）；无数据维持「暂无出版著作资料」。
  - 样式沿用 `.artist-record-grid`（馆藏/拍卖同一套卡片与 `dl`），`public-pages.css` 仅补 `.artist-record-grid--books`（封面 4:5 `contain`，简介灰色小字），已写中文注释。
  - 缺表（SQL Server 错误号 208 / SQLite no such table，用 `MissingSchema.IsMissingTable`，不依赖中文报错文字）或无数据：Art 侧栏出版著作为空，档案页为空状态，绝不 500。
- **SQL**：`docs/sql/2026-10-26-artist-books.sql`（幂等 `IF OBJECT_ID(...) IS NULL CREATE TABLE`，含 `IX_ArtistBook_Artist_Sort`）；已加入 `scripts/build-automation-sql.py` 来源清单最后一项并重新生成 `2026-10-26-automation-deploy.sql`；`2026-10-26-automation-rollback.sql` 的盘点表清单与拆除逻辑追加 `ArtistBook`（默认只读盘点，有数据拒绝拆除）。`--check` 现在提示「七份来源一致」。

## 修复的旧 bug

旧站 Art 页侧栏「出版著作」把 ArtNews 的资讯标题当书名显示（占位 bug）；ArtArchive 出版段永远是空状态且后台无录入入口。另：无书时侧栏不再输出空的《》。

## 验证

- `~/.dotnet/dotnet build Szaipa.Modernization.slnx`：0 warning / 0 error。
- `~/.dotnet/dotnet test Szaipa.Modernization.slnx`：全部通过，Data 376 + Web 272 = 648（基线 643，新增 `ArtistBookTests` 5 项：仓储 CRUD/封面保留/审计/列表联艺术家名与筛选、侧栏按 Sort 取首本且绝不回退 ArtNews、档案页按 Sort/Id 全量投影与空串列、DROP TABLE 后读侧不抛且其余分区不受影响、总览数量与缺表标记；并更新 `ArtistReadRepositoryTests` 中依赖旧占位行为的断言）。
- `python3 scripts/build-automation-sql.py --check`：通过。`cd src/Szaipa.Web && npm run build`：成功（`admin.css` 重新生成，含 `md:grid-cols-6`）。
- 本地镜像端到端（`mirror-sync` 在 worktree 建 `.local-db`，`LocalMirror__Enabled=true`、`LegacyAssets__ContentRoot` 指向 worktree 内临时目录，端口 5081，debug/debug 登录）：给艺术家 1000 录入一本书并上传封面 → 文件落在临时目录 `ArtImg/Artist/Book/{guid}.png`（未写任何 web24.05/Content）→ `/Home/Art/1000` 侧栏显示《书名》、`/Home/Art/1000/Archive#publications` 显示封面/出版社/年份/简介；390 与 1440 宽度 `scrollWidth == clientWidth`，无横向溢出；后台编辑（保留封面）、删除后前台恢复空状态；再 `DROP TABLE ArtistBook` 后 Art、Archive 仍 200 且为空状态，`/Staff/ArtistBook` 显示「需先执行 SQL」，`/Staff/Artist/1000/Site` 显示缺表提示。测试数据已删除，临时目录与镜像库已清理。

## 用户手动操作

生产发布前由用户在读写共用库执行 `docs/sql/2026-10-26-artist-books.sql`（或整份 `2026-10-26-automation-deploy.sql`，其中已含本表）；执行前前台一切照常，仅出版著作为空。agent 未对生产库做任何写操作。
