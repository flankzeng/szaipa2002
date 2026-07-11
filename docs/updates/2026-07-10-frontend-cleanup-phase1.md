# 前台清理与性能优化 Phase 1（2026-07-10）

## 本轮目标

在保持现有视觉、路由和只读数据行为不变的前提下，先清理公共布局的无效依赖，降低首页流量，并修复手机端导航和视口问题。旧 `szaipa2022/` 继续作为迁移参考，本轮实现均落在 `src/Szaipa.Web`。

## 已完成

- 公共前台布局移除未使用的 Layui、Bootstrap JS；艺术家布局同时移除不存在且会 404 的 `Site.js`。
- jQuery、Swiper、Magnify 改为首页、艺术家、展览详情按页加载；新闻列表不再为无关交互加载这些库。
- 新闻详情的三段面包屑改成原生 HTML，删除仅为它加载的 Vue、Element Plus，并把页面 CSS 正确放回 `<head>`。
- 首页、新闻、艺术家和展览图片加入原生 `loading="lazy"` + `decoding="async"`；首屏品牌图保留高优先级。
- 启用 Brotli/Gzip；生产环境新站静态资源缓存 7 天，旧 `/Content` 缓存 1 天；开发环境使用 `no-cache`，避免调试命中旧文件。
- 手机 viewport 从 `initial-scale=0.4` 修正为 1；导航在 320–1279px 使用紧凑横排，强制每项单行、禁止逐字断行，内容与会员列表保持 100% 视口宽度。
- 手机端隐藏的首屏 Swiper 不再初始化，避免 `swiperSlideSize` 异常中断后续脚本；桌面轮播保持正常。
- 保留原字体字形并做核心字符子集：
  - `SmilelySans`：约 1.3MB → 172KB；原字体逐字兜底生僻字。
  - `NotoSansSC Light/Medium`：原发布目录中的完整字体为 404；现各使用约 200KB 的同字体子集。
  - 可复现脚本：`scripts/build-font-subsets.py`。
- 删除新闻详情中约 370 行已注释、从未执行的全国城市坐标遗留。

## 验证

- `~/.dotnet/dotnet build Szaipa.Modernization.slnx`：0 warning / 0 error。
- `~/.dotnet/dotnet test Szaipa.Modernization.slnx`：94/94 通过。
- 浏览器回归：首页、新闻列表、新闻详情、艺术家页均正常，无当前版本控制台错误。
- 浏览器宽度 320、390、768、1024、1199、1200、1280、1366、1440：导航四项均为 `nowrap` + `horizontal-tb`，纵坐标一致且无页面横向溢出。
- 手机 390×844：首页 116 张图片中 114 张为懒加载；桌面 1440×900 首屏 Swiper 正常初始化。
- HTTP：CSS/JS Brotli 生效；缓存头与 ETag/304 生效。

## 已知外部前提

- 本地只读库尚缺 `Publication.Type/Preface/Signature`，展览详情查询会报 SQL 207。需执行既有 `docs/sql/2026-06-exhibition-template-columns.sql`；agent 不写数据库。
- ProjectTongou 公开页已退役为无数据库访问的 410 兼容响应；后台、实体和上传资源仍保留。
- 旧资源第一轮只完成分级，未执行删除，见 `docs/updates/2026-07-10-legacy-resource-inventory.md`。
