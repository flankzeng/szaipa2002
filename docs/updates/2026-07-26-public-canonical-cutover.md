# 公开站 canonical 切换与艺术家旧内容迁移

日期：2026-07-26

## 目标

把现代公开站从迁移期的 `new*` 命名正式切换到稳定 URL 和文件名，同时遵守“先迁内容、后退役旧入口”：任何仍由旧控制器提供的数据库内容，都必须先进入现代只读仓储和 Razor 页面。

## 主要改动

- 正式路由为 `/`、`/Home/News`、`/Home/NewsRead/{id}`、`/Home/Vip`、`/Home/About`、`/Home/Art/{id}`。
- 现代视图、布局和页面资源同步改为 `Index/News/NewsRead/Vip/About/Art`、`_PublicLayout/_ArtistLayout`、`index.css`/`art.js` 等 canonical 名称。
- 原 `new*` URL 仅保留永久重定向，不再出现在公开页内部链接中。
- 首页不再在数据库关闭时暴露迁移仪表盘；正式列表使用空状态，必须依赖正文数据的详情返回 503。
- 删除公开迁移仪表盘、路由预览、页面 Skeleton 的 Razor、模型和服务，共移除约 1,400 行运行时迁移展示代码。

## 艺术家内容解耦

旧 `ArtNews` 与 `ArtNewsRead` 并非空壳：它们仍承载完整艺术家资讯列表、正文以及馆藏、拍卖字段。切换前新增：

- `IArtistReadRepository.GetArtistArchiveAsync`
- `IArtistReadRepository.GetArtistArticleAsync`
- `/Home/Art/{id}/Archive`
- `/Home/Art/News/{id}`

读取保持 `.AsNoTracking()` 和只读投影，不修改旧库、不增加表结构。旧 `/Home/ArtNews/{id}` 与 `/Home/ArtNewsRead/{id}` 仅在现代内容可用后改为永久重定向。“出版著作”在旧页本来没有数据源，因此保留栏目和诚实空状态，不把 `ArtNews` 重复冒充出版数据。

## 验证

- `Szaipa.Web` build：0 warning / 0 error。
- Data 测试：100/100。
- Web 测试：151/151。
- `npm run build`：Tailwind、编辑器、仪表盘、公共 Swiper 全部通过。
- 390×844 真实页面：8 个 canonical 路由均无横向溢出，首页 navbar 横排且单行。
- 艺术家 1000 归档读取 3 条资讯及馆藏/拍卖内容；资讯 1004 正文与富文本图片正常。
- 8 个兼容入口均落到对应 canonical URL。

服务器、IIS、旧发布版、外接 Content、数据库和 `szaipa2026` 均未修改。
