# 公共滚动逻辑缓存化（2026-07-26）

本轮把两段每次随 HTML 重复传输的静态滚动逻辑原样外提为内容版本资源，不修改动态数据、CSS、图片、外接 Content、服务器或数据库。

## 修改

- `_newLayout.cshtml` 的 navbar 向下收起/向上恢复逻辑改为 `public-layout-main.js`。
- `NewNewsRead.cshtml` 原本嵌套在返回顶部 button 内的脚本改为 `newnewsread.js`，同时让 HTML 结构恢复为 button 只包含图标。
- 两个引用都使用 `asp-append-version="true"` 与 `defer`，可进入现有一年 immutable 缓存；滚动监听继续使用 passive，navbar 继续经 `requestAnimationFrame` 合并更新。
- NewArt 的动态 banner JSON/CSS 仍保留内联，没有把页面数据错误外提为静态文件。

## 体积边界

- 公共布局视图减少 1,151B；外部脚本 1,086B，`gzip -9 -n` 为 381B。
- 新闻详情视图减少 741B；外部脚本 439B，`gzip -9 -n` 为 260B。
- 首次访问会新增一条小型脚本请求，因此不宣称单页首访必然节省；确定收益是脚本可跨公共页面或多篇新闻复用缓存，后续 HTML 不再反复携带同一逻辑。

## 验证

- `/Home/NewAbout` 1440px：外提前后均为 top=`none`，向下滚动后 `translateY(-100%)`，向上后 `translateY(0)`，transition 均为 250ms。
- `/Home/NewNewsRead/1124` 1440px：前后均在顶部隐藏、滚过 500px 后显示；唯一按钮点击后平滑回到 `scrollY=0`。
- 两个真实页面均确认加载带内容哈希的外部脚本。
- 新增契约测试，锁定外部引用、defer、passive、阈值与平滑滚动，且 Razor 不得重新内联静态逻辑。
- `~/.dotnet/dotnet build Szaipa.Modernization.slnx --no-restore --disable-build-servers -m:1`：0 warning / 0 error。
- `~/.dotnet/dotnet test Szaipa.Modernization.slnx --no-build --no-restore --disable-build-servers -m:1`：Data 98/98、Web 149/149，合计 247/247。
