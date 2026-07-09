# 2026-07-09 展览 slug 页退役（迁成数据驱动 Publication 行）

## 背景

`PublicationController` 里 19 个手写的展览详情 action 中，除 3 个特大自定义页
（`chunyu3` 2883 行、`tonggou2` 1864 行、`tonggou2024` 1159 行，结构特殊/视觉复杂，
未抽象出模板）外，其余大多是同一套普通型模板（标题 + 中英文副标题 + 开幕/结束时间 +
swiper 双画廊）。这批已随「展览模块数据驱动」（`Publication` 实体 + `/Home/Publication/{id}`
+ `Views/Shared/_ExhibitionGallery.cshtml`）具备了迁移条件，本次把它们迁成
`Publication` 数据行，退役硬编码视图，旧 URL 用 301 永久重定向保留（SEO/收藏夹友好）。

## 范围调整：`zengfeng` 未迁移

原计划的 15 个 slug 里，`zengfeng.cshtml` 读取后发现**不是画廊页**——`Layout = null`，
整页是一个独立的 HTML5 翻页书（flipbook）迷你站，嵌入
`/Content/publication/zengfeng/mobile/...` 下的一整套 CSS/JS/资源，没有
`_ExhibitionGallery` 依赖的标题/日期/图片列表结构。`Publication` 实体无法表达这种内容，
强行迁移只会丢失内容。**因此 `zengfeng` 的 controller action 和视图原样保留**，与
`chunyu3`/`tonggou2`/`tonggou2024` 一样不动。另外确认过 `NewIndex.cshtml`、
`NewArt.cshtml`、`Publication/index.cshtml` 里都没有指向 `/Publication/zengfeng` 的
链接（本来就是孤立页面），所以不影响任何前台入口。

**实际迁移 14 个页面**（预留 ID 区间 92001-92015，92004 留空对应 zengfeng，未插入）。

## 迁移表：slug → 固定 Id → FolderName

| Id | slug | 标题 | FolderName | MaxImg | 图片目录是否已符合 10001+ 约定 |
|----|------|------|-----------|--------|-------------------------------|
| 92001 | tonggouEurope | 同构——欧洲行 当代艺术展 | tonggouEurope | 29 | ❌ 实际文件是 100000 起 6 位数，需重编号 |
| 92002 | shuimai | 2023龙游水脉艺术节 | shuimai | 29 | ⚠️ 10001 起但有缺号(10007/10009/10010/10019)+部分 .png，需补齐/重导出 |
| 92003 | chunyu | 春语·当代艺术名家邀请展 | chunyu | 11 | ❌ 文件名是 01.jpg..012.jpg，完全不符合，需重导出 |
| 92004 | (zengfeng，未迁移) | — | — | — | — |
| 92005 | zhongyi | 中意艺术名画展 | zhongyi | 23 | ⚠️ 从 10000 起（比标准约定少 1），基本可用 |
| 92006 | tonggou | 同构——当代艺术作品邀请展 | tonggou | 22 | ✅ 干净匹配 |
| 92007 | chunyu2 | 春语第二季——国际视觉艺术邀请展 | chunyu2 | 16 | ❌ 10010 起写成 100010，需重命名尾部 |
| 92008 | trio | 三人行——鸥洋/雷双/张岚芊艺术展 | trio | 17 | ❌ 同上，10010 起写成 100010，需重命名尾部 |
| 92009 | man | 漫MAN-艺术时尚先锋展 | man | 24 | ✅ 干净匹配 |
| 92010 | yijia | 艺+科技新潮流展 | yijia | 69 | ✅ 干净匹配 |
| 92011 | zhongri | 同构——中日艺术交流展 | zhongri | 56 | ✅ 干净匹配 |
| 92012 | shuyuyi | "数"与"艺"——新文艺群体创作成果展 | shuyuyi | 130 | ❌ 100000 起 6 位数，需重编号 |
| 92013 | chunyu4 | 春语第四季——当代艺术作品邀请展 | chunyu4 | 95 | ❌ 100000 起 6 位数，需重编号 |
| 92014 | zhongfa | 深圳-法国国际当代艺术展2025 | zhongfa | 44 | ❌ 100000 起 6 位数，需重编号 |
| 92015 | tangqishan | 入骨相知——唐岐山当代艺术展 | tangqishan | 25 | ❌ 100000 起 6 位数，需重编号 |

（`Type` 全部设为 0，即普通型模板；`Status` 全部设为 0/已结束——用当前会话日期
2026-07-09 核对，这 14 场展览的结束时间均已过去。）

## 重要：图片目录命名不匹配问题

新数据驱动的展览画廊模板（`Views/Shared/_ExhibitionGallery.cshtml`）和后台画廊管理器
（`Szaipa.Data/Services/Admin/ExhibitionGalleryFolder.cs`）都**硬性要求**图片是
`/Content/images/{FolderName}/` 下从 `10001.jpg` 起连续编号的文件（可选 `10000.jpg`
封面）。逐个打开这 14 个旧页面的源码后发现，只有 **tonggou / man / yijia / zhongri**
四个（加上基本可用的 zhongyi）已经是这个约定；其余 9 个的图片实际编号方式各不相同
（六位数 100000 起、10001 起但有缺号/混扩展名、或完全不同的文件名如 `01.jpg`）——
这是从旧页面源码里如实读出来的，不是我编的。

`MaxImg` 字段在 SQL 里已经按「渲染需要的图片张数（= 现有图片数 - 1）」存好，也就是说
**只要图片文件被重新编号成 10001+ 连续 .jpg，数据不用再改，直接就能正确渲染**。但在
重编号完成前，这 9 个页面的画廊会有图裂/404。这是磁盘上的图片文件重命名操作，不是
数据库问题——agent 没有生产内容目录的写权限，也被要求不碰生产内容，所以只能在这里
标注出来，交给用户后续处理（本机 `LegacyAssets:ContentRoot` 若配了可写的本地副本，
可以先在本地试跑）。

## 改了什么

1. **新增** `docs/sql/2026-07-09-publication-slug-migration.sql`——只写出来，**未在本机
   执行**，也没有连接任何数据库。执行前请先确认 92001-92015 这个 ID 区间在生产库里
   没有被占用（脚本开头附了确认用的 SELECT）。
2. **`src/Szaipa.Web/Controllers/PublicationController.cs`**：14 个 action 从
   `View()` 改成 `RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = ... })`
   （301 永久重定向到新的数据驱动详情页）。`index`、`zengfeng`、`chunyu3`、`tonggou2`、
   `tonggou2024` 五个 action 原样不动。
3. **删除** 14 个对应的 `Views/Publication/*.cshtml`（保留 `index.cshtml`、
   `zengfeng.cshtml`、`chunyu3.cshtml`、`tonggou2.cshtml`、`tonggou2024.cshtml`）。
4. **更新硬编码链接**：`grep -rn "/Publication/" src/Szaipa.Web/Views` 确认的三处—
   `Views/Home/NewIndex.cshtml`、`Views/Home/NewArt.cshtml`、
   `Views/Publication/index.cshtml`——把指向这 14 个已迁移 slug 的 `<a href>` 全部改成
   `/Home/Publication/{对应固定id}`；指向 `chunyu3`/`tonggou2`/`tonggou2024` 的链接
   保持不动。

## 验证

- `~/.dotnet/dotnet build Szaipa.Modernization.slnx` → 0 Warning / 0 Error。
- `~/.dotnet/dotnet test Szaipa.Modernization.slnx` → 82/82 全绿（基线不变，本次改动
  是路由级别机械迁移，没有新业务逻辑，未新增测试）。
- 未新增 Web 路由冒烟测试：项目目前只有 `Szaipa.Data.Tests`（SQLite 数据层测试），没有
  现成的 Web 控制器测试项目/模式可抄；`RedirectToActionPermanent` 是 ASP.NET Core
  `Controller` 基类标准的三参跨 controller 重定向重载，build 通过已确认
  `nameof(HomeController.Publication)` 解析正确。

## 用户仍需做的事

1. 核对生产库 `dbo.Publication` 表 `Id BETWEEN 92001 AND 92015` 当前为空，再执行
   `docs/sql/2026-07-09-publication-slug-migration.sql`（本地可写副本或生产写库，
   agent 不会代为执行）。
2. 视优先级安排上面 9 个「图片目录不匹配」页面的图片重编号/重导出（重命名为
   `10001.jpg` 起连续 .jpg，缺号的要补齐或删减 `MaxImg`）。
3. 执行完 SQL 后，本地起 `~/.dotnet/dotnet run --project src/Szaipa.Web/Szaipa.Web.csproj`
   点击验证：旧 slug（如 `/Publication/tonggou`）应 301 到
   `/Home/Publication/{id}` 并正确渲染标题/日期；`man`/`yijia`/`zhongri`/`tonggou`
   四个图片目录已合规，应该立即能看到完整画廊。
