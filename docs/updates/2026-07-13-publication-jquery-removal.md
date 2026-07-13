# 展览页移除 jQuery 与移动端 viewport 修复（2026-07-13）

## 范围与边界

- 只修改本地 ASP.NET Core modernization 项目；未访问或修改生产服务器、发布目录和数据库。
- 本地浏览器回归进程显式使用 `legacyDataSources=False`、`liveDatabase=False`，并关闭 Szaipa/Tongou read-model flags。
- 普通 `/Home/Publication/{id}` 在禁用数据库时只能显示 migration skeleton，无法得到真实展览详情；因此该路径完成静态依赖、脚本语法和可达 skeleton 验证，真实数据页留待后续在获准的只读数据环境验证。

## 实施

### 去除展览页 jQuery

- `publication-gallery.js` 改为原生 DOM ready 与 Swiper 初始化，普通展览详情不再加载 jQuery。
- `publication-special.js` 将筛选、导航激活、返回顶部、作品详情切换和 Swiper 初始化改为原生 DOM API。
- `publication-tonggou2024.js` 将导航、返回顶部、画廊和滚动逻辑改为原生 DOM API，并清理只服务于不存在元素的旧代码。
- `chunyu3`、`tonggou2`、`tonggou2024` 三个特殊展览页均移除 jQuery 引用。

每个受影响页面减少 1 个 jQuery 请求：旧文件为 89,664 B 未压缩，按本地 `gzip -9` 估算约 30,977 B；同时不再解析和执行这份与 Swiper 本身无关的库。

### 解除脚本解析阻塞

- Swiper 与页面自有脚本都加上 `defer`，并保持“Swiper 在前、页面初始化脚本在后”的文档顺序。
- 三个特殊展览页原先位于 `<head>` 的同步 jQuery 与 Swiper 不再阻塞 HTML 解析；移除 jQuery 后，剩余 Swiper 也延后到文档解析完成后按顺序执行。
- 浏览器检查确认三页中的 Swiper 和页面自有脚本均为 `defer=true`，且页面 DOM 中不再存在 jQuery script URL。

### 修复手机按桌面宽度渲染

浏览器基线发现三个 `Layout = null` 的特殊展览页都缺少 viewport 声明。即使测试设备视口设为 390×844，浏览器仍使用 1440 CSS px 的布局视口，因此手机会呈现缩小后的桌面版。

三页 `<head>` 现统一加入：

```html
<meta name="viewport" content="width=device-width, initial-scale=1" />
```

改后同一手机测试的 CSS 布局视口由 1440 恢复为 390；三页均为 `scrollWidth = clientWidth = 390`，没有横向溢出。`tonggou2024` 的三项顶部自有导航位于同一纵坐标，`writing-mode: horizontal-tb`，保持一行横排。

## 浏览器回归

改前桌面基线为 1280×720，三页均为 `scrollWidth = clientWidth = 1280`；改后重点复测 390×844 手机视口。

| 页面 | Swiper 初始化（改前 → 改后） | 改后 CSS viewport | 改后横向溢出 | console warning/error |
|---|---:|---:|---:|---:|
| `/Publication/chunyu3` | 3 → 3 | 390×844 | 0 | 0 |
| `/Publication/tonggou2` | 3 → 3 | 390×844 | 0 | 0 |
| `/Publication/tonggou2024` | 2 → 2 | 390×844 | 0 | 0 |

额外交互检查：

- `tonggou2` 的“纸本水墨”筛选切换后保持 3 个匹配项、隐藏 6 个不匹配项，并正确更新粗体状态。
- 点击“序章”后 active nav 正确切换为“序章”。
- 改前画廊下一张从索引 0 切到 1，返回顶部平滑滚动回页面顶部；改后 Swiper 初始化数量和控制结构保持不变。
- 手机截图确认页面不再缩放成桌面画布；自有导航仍横排且没有页面级横向滚动。

## 已完成的静态与构建验证

- 三个修改后的 JavaScript 文件通过 `node --check`。
- `dotnet build Szaipa.Modernization.slnx --disable-build-servers -m:1`：0 warning / 0 error。
- 普通 Publication 在禁 DB 模式下的 migration skeleton 返回正常；真实数据详情未声称完成浏览器行为验证。
- `git diff --check`：通过。
