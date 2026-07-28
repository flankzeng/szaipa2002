# 公开页面迁移完整性审计

日期：2026-07-28

## 结论

新版并非只迁移了 `Home`。当前实际运行面已经包括：

- 公开站：`src/Szaipa.Web/Views/Home`
- 特殊展览：`src/Szaipa.Web/Views/Publication`
- 后台：`src/Szaipa.Web/Areas/Staff`
- 同构项目后台：`Staff/TongouAtrist`、`Staff/TongouWorks`
- 旧同构项目前台的历史 URL：统一由轻量 410 端点承接，不再访问旧数据库

本轮根据新版导航、线上主要页面内部链接、旧控制器实现和生产 IIS 审计重新核对公开页。
发现的实际遗漏是完整的协会章程页；该页及其静态资源引用已迁到
`Views/Home/Constitution.cshtml`，正式入口为 `/Home/Constitution`。

## 已迁移且继续提供的公开内容

| 内容 | 新版入口 | 处理方式 |
|---|---|---|
| 首页 | `/` | 新版视图与只读首页聚合 |
| 新闻列表、详情 | `/Home/News`、`/Home/NewsRead/{id}` | 只读新闻仓储 |
| 会员列表、艺术家主页 | `/Home/Vip`、`/Home/Art/{id}` | 只读艺术家仓储 |
| 艺术家资讯、馆藏、拍卖 | `/Home/Art/{id}/Archive`、`/Home/Art/News/{id}` | 完整档案读模型 |
| 关于我们 | `/Home/About` | 新版静态页面 |
| 协会章程 | `/Home/Constitution` | 本轮从旧站完整迁移 |
| 展览列表、数据展览 | `/Home/PublicationList`、`/Home/Publication/{id}` | 数据库优先、只读 fallback |
| 三个特殊展览 | `/Publication/chunyu3`、`tonggou2`、`tonggou2024` | 保留独立视图 |
| 后台所有现行模块 | `/Staff/...` | ASP.NET Core Area |

旧 `new*` URL、`ArtNews*`、`link/newlink` 和 `newConstitution` 只保留永久重定向，
新版页面不再链接这些名字。

## 没有复制进新版的旧页面

以下条目不是迁移遗漏：

- `Home/All`、`Search/Index`、`Artist/index`：空视图或无实际内容。
- `Home/membership`、`Home/company`：只有开发期占位标题，没有可迁移的正式页面。
- `Home/listworks`：控制器读取后没有返回数据，且没有对应可用视图。
- `Home/work`、`Home/works`、`Home/Tags`、`Home/company/{id}`：线上已返回 500，
  新版艺术家页直接展示作品，没有现行前端入口。
- `Award`：独立旧微站；生产 IIS 90 天审计没有访问，也不在新版或线上主站导航中。
- `Project_Tongou` 前台、`zengfeng`：已按既定退役决定返回 410；后台和数据层不受影响。
- `vip_debi`：只存在于被新版会员页替换的旧会员模板中，新版导航没有入口。
- 旧 `Index`、`news`、`vip`、`about` 与对应 `new*` 的重复视图：由同一个 canonical
  新版页面替代，不再保留两套耦合模板。

这些文件继续留在 `szaipa2022/` 只作为迁移取证材料，不是新版项目的编译、运行或发布依赖。

## 资源边界

新版源码不复制约 1.2GB 的图片和字体资源。运行时通过
`LegacyAssets:ContentRoot` 把发布基线的 `Content` 目录只读映射到 `/Content`。
因此新仓库明显变小不代表图片被丢弃；源码与内容卷被有意拆开。

最终独立发布仓库只导出 `src/`、`tests/`、`scripts/`、`docs/` 和现代解决方案/配置文件。
`szaipa2022/` 不进入新仓库。
